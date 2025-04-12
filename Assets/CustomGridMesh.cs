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
    [Range(1f, 99.9f)] public float cropDistance = 50f;
    private Slider sliderCompressNearest;
    [Range(0.0f, 10f)] public float compressNearest = 0.1f;
    private Slider sliderCompressFarthest;
    [Range(1.0f, 99.9f)] public float compressFarthest = 2.0f;
    private Slider sliderCompressDistance;
    [Range(0.1f, 99.9f)] public float compressDistance = 1.9f;

    private TextMeshProUGUI vMR;
    private TextMeshProUGUI vCropD;
    private TextMeshProUGUI vCompN;
    private TextMeshProUGUI vCompF;
    private TextMeshProUGUI vCompD;

    private float[] zValues;
    private float zValueMin;
    private bool isMeshCreated = false;

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

        vMR = valueMR.GetComponent<TextMeshProUGUI>();
        vCropD = valueCropD.GetComponent<TextMeshProUGUI>();
        vCompN = valueCompN.GetComponent<TextMeshProUGUI>();
        vCompF = valueCompF.GetComponent<TextMeshProUGUI>();
        vCompD = valueCompD.GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// スライダー初期値設定
    /// </summary>
    private void Start()
    {
        magnificationRatio = 1.0f;
        cropDistance = 50f;
        compressNearest = 0.1f;
        compressFarthest = 2.0f;
        compressDistance = 1.9f;

        sliderMagnificationRatio.value = magnificationRatio;
        sliderCropDistance.value = cropDistance;
        sliderCompressNearest.value = compressNearest;
        sliderCompressFarthest.value = compressFarthest;
        sliderCompressDistance.value = compressDistance;

        vMR.text = sliderMagnificationRatio.value.ToString("f1");
        vCropD.text = sliderCropDistance.value.ToString("f1");
        vCompN.text = sliderCompressNearest.value.ToString("f1");
        vCompF.text = sliderCompressFarthest.value.ToString("f1");
        vCompD.text = sliderCompressDistance.value.ToString("f1");
    }

    /// <summary>
    /// スライダー値変更時の処理
    /// </summary>
    public void SliderValueChanged()
    {
        float _compressNearest = sliderCompressNearest.value;
        float _compressFarthest = sliderCompressFarthest.value;
        float _compressDistance = sliderCompressDistance.value;
        if (_compressFarthest < _compressNearest) { _compressFarthest = _compressNearest + 0.1f; }
        if (_compressDistance > (_compressFarthest - _compressNearest)) { _compressDistance = _compressFarthest - _compressNearest - 0.1f; }
        sliderCompressFarthest.value = _compressFarthest;
        sliderCompressDistance.value = _compressDistance;

        vMR.text = sliderMagnificationRatio.value.ToString("f1");
        vCropD.text = sliderCropDistance.value.ToString("f1");
        vCompN.text = sliderCompressNearest.value.ToString("f1");
        vCompF.text = sliderCompressFarthest.value.ToString("f1");
        vCompD.text = sliderCompressDistance.value.ToString("f1");
    }

    private void Update()
    {
        if (!isMeshCreated)
            return;

        // Z座標の更新
        UpdateVertexZPositions(i =>
        {
            magnificationRatio = sliderMagnificationRatio.value;
            cropDistance = sliderCropDistance.value;
            compressNearest = sliderCompressNearest.value;
            compressFarthest = sliderCompressFarthest.value;
            compressDistance = sliderCompressDistance.value;

            float zValue = Mathf.Min((zValues[i] - zValueMin) * imageManipulator.displayedScale, cropDistance);
            zValue = Mathf.Log(1 + zValue) * Mathf.Pow(magnificationRatio, 1.0f);

            // 深度圧縮調整
            if (compressFarthest < compressNearest) { compressFarthest = compressNearest + 0.1f; }
            if (compressDistance > (compressFarthest - compressNearest)) { compressDistance = compressFarthest - compressNearest - 0.1f; }
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
        mesh.RecalculateNormals();
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
        mesh.RecalculateNormals();

        // MeshFilterにセット
        MeshFilter mf = GetComponent<MeshFilter>();
        mf.mesh = mesh;
    }
}
