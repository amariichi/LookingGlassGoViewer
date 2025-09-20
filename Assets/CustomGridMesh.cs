using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// カスタムグリッドメッシュを生成・操作するコンポーネント
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CustomGridMesh : MonoBehaviour
{
    // --- Inspectorで設定可能なフィールド ---
    [Tooltip("オブジェクトの幅（メートル単位）")]
    public float objectSize = 5f;

    [SerializeField] private ImageManipulator imageManipulator;
    [SerializeField] private ImageCropper imageCropper;

    [SerializeField] public GameObject sliderMR;
    [SerializeField] public GameObject sliderCropD;
    [SerializeField] public GameObject sliderCompN;
    [SerializeField] public GameObject sliderCompF;
    [SerializeField] public GameObject sliderCompD;
    [SerializeField] public GameObject valueMR;
    [SerializeField] public GameObject valueCropD;
    [SerializeField] public GameObject valueCompN;
    [SerializeField] public GameObject valueCompF;
    [SerializeField] public GameObject valueCompD;

    // --- 内部状態 ---
    private Mesh mesh;
    private Vector3[] vertices;
    public int counter = 0;

    private Slider sliderMagnificationRatio;
    [Range(1f, 50f)] public float magnificationRatio = 1f;
    private Slider sliderCropDistance;
    [Range(1f, 10000f)] public float cropDistance = 50f;
    private Slider sliderCompressNearest;
    [Range(0.0f, 10f)] public float compressNearest = 0.1f;
    private Slider sliderCompressFarthest;
    [Range(1.0f, 10000f)] public float compressFarthest = 4.0f;
    private Slider sliderCompressDistance;
    [Range(0.1f, 20f)] public float compressDistance = 3.9f;

    private TextMeshProUGUI vMR;
    private TextMeshProUGUI vCropD;
    private TextMeshProUGUI vCompN;
    private TextMeshProUGUI vCompF;
    private TextMeshProUGUI vCompD;

    private float[] zValues;
    private float zValueMin;
    private bool isMeshCreated = false;
    private bool pendingMeshUpdate;

    public bool IsMeshCreated => isMeshCreated;

    private const float CropDistanceSliderMin = 1f;
    private const float CropDistanceSliderMax = 100f;
    private const float CropDistanceActualMin = 1f;
    private const float CropDistanceMidValue = 1000f;
    private const float CropDistanceActualMax = 10000f;
    private const float CompressFarthestSliderMin = 1f;
    private const float CompressFarthestSliderMax = 100f;
    private const float CompressFarthestActualMin = 1f;
    private const float CompressFarthestMidValue = 1000f;
    private const float CompressFarthestActualMax = 10000f;
    private const float SliderLowSegmentRatio = 2f / 3f;
    private const float CompressDistanceMin = 0.1f;
    private const float CompressDistanceMax = 20f;

    private static float PiecewiseSquareSliderToValue(float sliderValue, float sliderMin, float sliderMax, float minValue, float midValue, float maxValue)
    {
        float t = Mathf.InverseLerp(sliderMin, sliderMax, sliderValue);
        t = Mathf.Clamp01(t);

        if (t <= SliderLowSegmentRatio)
        {
            float denominator = Mathf.Max(Mathf.Epsilon, SliderLowSegmentRatio);
            float subT = t / denominator;
            float curved = subT * subT;
            return Mathf.Lerp(minValue, midValue, curved);
        }
        else
        {
            float denominator = Mathf.Max(Mathf.Epsilon, 1f - SliderLowSegmentRatio);
            float subT = (t - SliderLowSegmentRatio) / denominator;
            float curved = subT * subT;
            return Mathf.Lerp(midValue, maxValue, curved);
        }
    }

    private static float PiecewiseSquareValueToSlider(float value, float sliderMin, float sliderMax, float minValue, float midValue, float maxValue)
    {
        float clamped = Mathf.Clamp(value, minValue, maxValue);
        float sliderNormalized;

        if (clamped <= midValue)
        {
            float denominator = Mathf.Max(Mathf.Epsilon, midValue - minValue);
            float ratio = Mathf.Clamp01((clamped - minValue) / denominator);
            float subT = Mathf.Sqrt(ratio);
            sliderNormalized = subT * SliderLowSegmentRatio;
        }
        else
        {
            float denominator = Mathf.Max(Mathf.Epsilon, maxValue - midValue);
            float ratio = Mathf.Clamp01((clamped - midValue) / denominator);
            float subT = Mathf.Sqrt(ratio);
            sliderNormalized = SliderLowSegmentRatio + subT * (1f - SliderLowSegmentRatio);
        }

        return Mathf.Lerp(sliderMin, sliderMax, sliderNormalized);
    }

    private static float ConvertCompressFarthestSliderToValue(float sliderValue)
    {
        return PiecewiseSquareSliderToValue(sliderValue, CompressFarthestSliderMin, CompressFarthestSliderMax, CompressFarthestActualMin, CompressFarthestMidValue, CompressFarthestActualMax);
    }

    private static float ConvertCompressFarthestValueToSlider(float value)
    {
        return PiecewiseSquareValueToSlider(value, CompressFarthestSliderMin, CompressFarthestSliderMax, CompressFarthestActualMin, CompressFarthestMidValue, CompressFarthestActualMax);
    }

    private static float ConvertCropDistanceSliderToValue(float sliderValue)
    {
        return PiecewiseSquareSliderToValue(sliderValue, CropDistanceSliderMin, CropDistanceSliderMax, CropDistanceActualMin, CropDistanceMidValue, CropDistanceActualMax);
    }

    private static float ConvertCropDistanceValueToSlider(float value)
    {
        return PiecewiseSquareValueToSlider(value, CropDistanceSliderMin, CropDistanceSliderMax, CropDistanceActualMin, CropDistanceMidValue, CropDistanceActualMax);
    }

    /// <summary>
    /// 画像クロップ時のイベントハンドラ
    /// </summary>
    public void OnImageCroppedHandler(int frameWidth, int frameHeight, bool isStart)
    {
        if (isStart)
        {
            CreateMesh(frameWidth, frameHeight);
            isMeshCreated = true;
        }

        zValues = imageCropper.zValues;
        zValueMin = zValues.Min();
        pendingMeshUpdate = true;
    }

    /// <summary>
    /// スライダー・UI初期化
    /// </summary>
    private void Awake()
    {
        sliderMagnificationRatio = sliderMR.GetComponent<Slider>();
        sliderCropDistance = sliderCropD.GetComponent<Slider>();
        sliderCompressNearest = sliderCompN.GetComponent<Slider>();
        sliderCompressFarthest = sliderCompF.GetComponent<Slider>();
        sliderCompressDistance = sliderCompD.GetComponent<Slider>();

        if (sliderCropDistance != null)
        {
            sliderCropDistance.minValue = CropDistanceSliderMin;
            sliderCropDistance.maxValue = CropDistanceSliderMax;
            sliderCropDistance.wholeNumbers = false;
        }

        if (sliderCompressFarthest != null)
        {
            sliderCompressFarthest.minValue = CompressFarthestSliderMin;
            sliderCompressFarthest.maxValue = CompressFarthestSliderMax;
            sliderCompressFarthest.wholeNumbers = false;
        }

        if (sliderCompressDistance != null)
        {
            sliderCompressDistance.minValue = CompressDistanceMin;
            sliderCompressDistance.maxValue = CompressDistanceMax;
            sliderCompressDistance.wholeNumbers = false;
        }

        AttachSliderKeyboardBlocker(sliderMR);
        AttachSliderKeyboardBlocker(sliderCropD);
        AttachSliderKeyboardBlocker(sliderCompN);
        AttachSliderKeyboardBlocker(sliderCompF);
        AttachSliderKeyboardBlocker(sliderCompD);

        vMR = valueMR.GetComponent<TextMeshProUGUI>();
        vCropD = valueCropD.GetComponent<TextMeshProUGUI>();
        vCompN = valueCompN.GetComponent<TextMeshProUGUI>();
        vCompF = valueCompF.GetComponent<TextMeshProUGUI>();
        vCompD = valueCompD.GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        if (imageManipulator != null)
        {
            imageManipulator.OnDisplayParametersChanged += HandleDisplayParametersChanged;
        }
    }

    private void OnDisable()
    {
        if (imageManipulator != null)
        {
            imageManipulator.OnDisplayParametersChanged -= HandleDisplayParametersChanged;
        }
    }

    /// <summary>
    /// スライダー初期値設定
    /// </summary>
    private void Start()
    {
        magnificationRatio = 1.0f;
        cropDistance = Mathf.Clamp(cropDistance, CropDistanceActualMin, CropDistanceActualMax);
        compressNearest = 0.1f;
        compressFarthest = Mathf.Clamp(compressFarthest, CompressFarthestActualMin, CompressFarthestActualMax);
        compressDistance = Mathf.Clamp(compressFarthest - compressNearest, CompressDistanceMin, CompressDistanceMax);

        sliderMagnificationRatio.value = magnificationRatio;
        sliderCropDistance.value = ConvertCropDistanceValueToSlider(cropDistance);
        sliderCompressNearest.value = compressNearest;
        sliderCompressFarthest.value = ConvertCompressFarthestValueToSlider(compressFarthest);
        sliderCompressDistance.value = compressDistance;

        vMR.text = sliderMagnificationRatio.value.ToString("f1");
        vCropD.text = cropDistance.ToString("#####");
        vCompN.text = sliderCompressNearest.value.ToString("f1");
        vCompF.text = compressFarthest.ToString("#####");
        vCompD.text = compressDistance.ToString("f1");

        pendingMeshUpdate = true;
    }

    /// <summary>
    /// スライダー値変更時の処理
    /// </summary>
    public void SliderValueChanged()
    {
        float cropValue = ConvertCropDistanceSliderToValue(sliderCropDistance.value);
        float nearestValue = sliderCompressNearest.value;
        float farthestSliderValue = sliderCompressFarthest.value;
        float farthestValue = ConvertCompressFarthestSliderToValue(farthestSliderValue);
        float distanceValue = Mathf.Clamp(sliderCompressDistance.value, CompressDistanceMin, CompressDistanceMax);

        cropDistance = cropValue;

        if (farthestValue < nearestValue)
        {
            farthestValue = nearestValue + 0.1f;
            farthestSliderValue = ConvertCompressFarthestValueToSlider(farthestValue);
            sliderCompressFarthest.SetValueWithoutNotify(farthestSliderValue);
        }

        float maxDistance = Mathf.Max(CompressDistanceMin, farthestValue - nearestValue - 0.1f);
        maxDistance = Mathf.Min(maxDistance, CompressDistanceMax);
        if (distanceValue > maxDistance)
        {
            distanceValue = maxDistance;
            sliderCompressDistance.SetValueWithoutNotify(distanceValue);
        }

        vMR.text = sliderMagnificationRatio.value.ToString("f1");
        vCropD.text = cropDistance.ToString("#####");
        vCompN.text = sliderCompressNearest.value.ToString("f1");
        vCompF.text = farthestValue.ToString("#####");
        vCompD.text = distanceValue.ToString("f1");

        pendingMeshUpdate = true;
    }

    private void Update()
    {
        if (!isMeshCreated || !pendingMeshUpdate)
            return;

        ApplyDepthToMesh();
    }

    /// <summary>
    /// 各頂点のZ座標を更新
    /// </summary>
    public void UpdateVertexZPositions(Func<int, float> zPositionFunc)
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].z = zPositionFunc(i);
        }

        mesh.vertices = vertices;
        mesh.RecalculateBounds();
    }

    /// <summary>
    /// メッシュ生成
    /// </summary>
    public void CreateMesh(int meshWidth, int meshHeight)
    {
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        int verticesPerRow = meshWidth + 1;
        int verticesPerColumn = meshHeight + 1;
        int numVertices = verticesPerRow * verticesPerColumn;
        int numSquares = meshWidth * meshHeight;
        int numTriangles = numSquares * 2;
        int numIndices = numTriangles * 3;
        float cellSize = objectSize / meshWidth;

        vertices = new Vector3[numVertices];
        Vector2[] uvs = new Vector2[numVertices];
        int[] triangles = new int[numIndices];

        // 頂点・UV生成
        for (int y = 0; y < verticesPerColumn; y++)
        {
            for (int x = 0; x < verticesPerRow; x++)
            {
                int index = y * verticesPerRow + x;
                vertices[index] = new Vector3(x * cellSize, y * cellSize, 0);
                uvs[index] = new Vector2((float)x / meshWidth, (float)y / meshHeight);
            }
        }

        // 三角形生成
        int triangleIndex = 0;
        for (int y = 0; y < meshHeight; y++)
        {
            for (int x = 0; x < meshWidth; x++)
            {
                int bottomLeft = y * verticesPerRow + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + verticesPerRow;
                int topRight = topLeft + 1;

                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;

                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = bottomRight;
            }
        }

        // メッシュにデータ適用
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        
        // MeshFilterにセット
        MeshFilter mf = GetComponent<MeshFilter>();
        mf.mesh = mesh;
    }

    private void AttachSliderKeyboardBlocker(GameObject sliderObject)
    {
        if (sliderObject == null)
            return;

        if (!sliderObject.TryGetComponent(out DisableSliderKeyboardInput blocker))
        {
            sliderObject.AddComponent<DisableSliderKeyboardInput>();
        }
    }

    private void ApplyDepthToMesh()
    {
        magnificationRatio = sliderMagnificationRatio.value;
        cropDistance = ConvertCropDistanceSliderToValue(sliderCropDistance.value);
        compressNearest = sliderCompressNearest.value;
        compressFarthest = ConvertCompressFarthestSliderToValue(sliderCompressFarthest.value);
        compressDistance = Mathf.Clamp(sliderCompressDistance.value, CompressDistanceMin, CompressDistanceMax);

        UpdateVertexZPositions(i =>
        {
            float zValue = Mathf.Min((zValues[i] - zValueMin) * imageManipulator.displayedScale, cropDistance);
//            zValue = 2f / 3.1416f * Mathf.Atan(zValue /((1f / 50f)*(magnificationRatio - 1f) + 1f)) * magnificationRatio;
            zValue = zValue / 2.5f * magnificationRatio;

            if (compressFarthest < compressNearest) { compressFarthest = compressNearest + 0.1f; }
            float allowedDistance = Mathf.Max(CompressDistanceMin, Mathf.Min(CompressDistanceMax, compressFarthest - compressNearest - 0.1f));
            if (compressDistance > allowedDistance)
            {
                compressDistance = allowedDistance;
                if (sliderCompressDistance != null)
                {
                    sliderCompressDistance.SetValueWithoutNotify(compressDistance);
                }
            }
            if (zValue > compressNearest && zValue < compressFarthest)
            {
                zValue = (compressNearest + (zValue - compressNearest) / (compressFarthest - compressNearest) * compressDistance);
            }
            else if (zValue >= compressFarthest)
            {
                zValue = Mathf.Max(compressNearest, (zValue - compressFarthest) + compressDistance + compressNearest);
            }

            return zValue;
        });

        pendingMeshUpdate = false;
    }

    private void HandleDisplayParametersChanged()
    {
        pendingMeshUpdate = true;
    }
}
