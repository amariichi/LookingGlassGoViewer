using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LookingGlass;

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

    [Header("視差補正設定")]
    [Tooltip("HologramCamera の縦方向FOVを使用して視差計算を行うかどうか")]
    [SerializeField] private bool useHologramCameraFov = true;
    [Tooltip("HologramCamera が取得できない場合に使用する縦方向FOV(度)")]
    [SerializeField, Range(5f, 120f)] private float fallbackVerticalFov = 18f;
    [Tooltip("XZ方向の補正強度(0で従来通り、1でフル補正)")]
    [SerializeField, Range(0f, 2f)] private float perspectiveStrength = 1f;

    private Vector3[] baseVertices;
    private float[] normalizedXs;
    private float[] normalizedYs;
    private int currentMeshWidth;
    private int currentMeshHeight;

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

        currentMeshWidth = meshWidth;
        currentMeshHeight = meshHeight;

        if (baseVertices == null || baseVertices.Length != numVertices)
        {
            baseVertices = new Vector3[numVertices];
            normalizedXs = new float[numVertices];
            normalizedYs = new float[numVertices];
        }

        // 頂点・UV生成
        for (int y = 0; y < verticesPerColumn; y++)
        {
            for (int x = 0; x < verticesPerRow; x++)
            {
                int index = y * verticesPerRow + x;
                vertices[index] = new Vector3(x * cellSize, y * cellSize, 0);
                uvs[index] = new Vector2((float)x / meshWidth, (float)y / meshHeight);
                float normalizedX = meshWidth == 0 ? 0f : ((float)x / meshWidth - 0.5f) * 2f;
                float normalizedY = meshHeight == 0 ? 0f : ((float)y / meshHeight - 0.5f) * 2f;
                normalizedXs[index] = normalizedX;
                normalizedYs[index] = normalizedY;
            }
        }

        Array.Copy(vertices, baseVertices, numVertices);

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
        if (vertices == null || baseVertices == null || normalizedXs == null || normalizedYs == null || zValues == null)
            return;

        if (zValues.Length != vertices.Length)
            return;

        magnificationRatio = sliderMagnificationRatio.value;
        cropDistance = ConvertCropDistanceSliderToValue(sliderCropDistance.value);
        compressNearest = sliderCompressNearest.value;
        compressFarthest = ConvertCompressFarthestSliderToValue(sliderCompressFarthest.value);
        compressDistance = Mathf.Clamp(sliderCompressDistance.value, CompressDistanceMin, CompressDistanceMax);

        if (compressFarthest < compressNearest + 0.1f)
        {
            compressFarthest = compressNearest + 0.1f;
            if (sliderCompressFarthest != null)
            {
                sliderCompressFarthest.SetValueWithoutNotify(ConvertCompressFarthestValueToSlider(compressFarthest));
            }
            if (vCompF != null)
            {
                vCompF.text = compressFarthest.ToString("#####");
            }
        }

        float allowedDistance = Mathf.Max(CompressDistanceMin, Mathf.Min(CompressDistanceMax, compressFarthest - compressNearest - 0.1f));
        if (compressDistance > allowedDistance)
        {
            compressDistance = allowedDistance;
            if (sliderCompressDistance != null)
            {
                sliderCompressDistance.SetValueWithoutNotify(compressDistance);
            }
            if (vCompD != null)
            {
                vCompD.text = compressDistance.ToString("f1");
            }
        }

        float verticalFovDeg = Mathf.Clamp(GetEffectiveVerticalFov(), 5f, 120f);
        float tanHalfVertical = Mathf.Tan(verticalFovDeg * Mathf.Deg2Rad * 0.5f);
        int width = Mathf.Max(1, currentMeshWidth);
        int height = Mathf.Max(1, currentMeshHeight);
        float tanHalfHorizontal = tanHalfVertical * (width / (float)height);
        float displayScale = imageManipulator != null ? imageManipulator.displayedScale : 1f;

        for (int i = 0; i < vertices.Length; i++)
        {
            float zValue = Mathf.Min((zValues[i] - zValueMin) * displayScale, cropDistance);
            zValue = zValue / 2.5f * magnificationRatio;

            if (zValue > compressNearest && zValue < compressFarthest)
            {
                zValue = compressNearest + (zValue - compressNearest) / (compressFarthest - compressNearest) * compressDistance;
            }
            else if (zValue >= compressFarthest)
            {
                zValue = Mathf.Max(compressNearest, (zValue - compressFarthest) + compressDistance + compressNearest);
            }

            float lateralDepth = zValue * perspectiveStrength;
            float deltaX = normalizedXs[i] * tanHalfHorizontal * lateralDepth;
            float deltaY = normalizedYs[i] * tanHalfVertical * lateralDepth;

            vertices[i] = new Vector3(
                baseVertices[i].x + deltaX,
                baseVertices[i].y + deltaY,
                baseVertices[i].z + zValue
            );
        }

        mesh.vertices = vertices;
        mesh.RecalculateBounds();

        pendingMeshUpdate = false;
    }

    private void HandleDisplayParametersChanged()
    {
        pendingMeshUpdate = true;
    }

    private float GetEffectiveVerticalFov()
    {
        if (useHologramCameraFov)
        {
            HologramCamera instance = HologramCamera.Instance;
            if (instance != null)
            {
                return instance.CameraProperties.FieldOfView;
            }
        }

        return fallbackVerticalFov;
    }
}
