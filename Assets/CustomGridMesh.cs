using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class MeshGenerator : MonoBehaviour
{
    // Set object width in m, オブジェクトの横の長さ（メートル）を設定
    public float objectSize = 5f; // 5m

    // Set ImageSplitter, ImageSplitterスクリプトへの参照をインスペクターから設定
    [SerializeField]
    private ImageCropper imageCropper;

    // slider関係
    [SerializeField]
    public GameObject sliderMR;
    [SerializeField]
    public GameObject sliderCropD;
    [SerializeField]
    public GameObject sliderCompN;
    [SerializeField]
    public GameObject sliderCompF;
    [SerializeField]
    public GameObject sliderCompD;
    [SerializeField]
    public GameObject valueMR;
    [SerializeField]
    public GameObject valueCropD;
    [SerializeField]
    public GameObject valueCompN;
    [SerializeField]
    public GameObject valueCompF;
    [SerializeField]
    public GameObject valueCompD;

    private Mesh mesh;             // keep reference to mesh, メッシュへの参照を保持
    private Vector3[] vertices;    // keep vrtices, 頂点配列を保持
    public int counter = 0;

    Slider sliderMagnificationRatio;
    [Range(1f, 200f)]
    public float magnificationRatio = 7f;
    Slider sliderCropDistance;
    [Range(1f, 50f)]
    public float cropDistance = 25f;
    Slider sliderCompressNearest;
    [Range(0.0f,10f)]
    public float compressNearest = 0.1f;
    Slider sliderCompressFarthest;
    [Range(1.0f, 50f)]
    public float compressFarthest = 2.0f;
    Slider sliderCompressDistance;
    [Range(0.1f, 25.0f)]
    public float compressDistance = 1.9f;

    TextMeshProUGUI vMR;
    TextMeshProUGUI vCropD;
    TextMeshProUGUI vCompN;
    TextMeshProUGUI vCompF;
    TextMeshProUGUI vCompD;

    private float[] zValues;
    private float zValueMin;
    private bool isMeshCreated = false;

    // Mesh Creation, meshを作成
    public void OnImageCroppedHandler(int frameWidth, int frameHeight, bool isStart)

    {
        if (isStart)
        {
            CreateMesh(frameWidth, frameHeight);
            isMeshCreated = true;
        }
        zValues = imageCropper.zValues;
        float _zValueMin = zValues.Min();
        zValueMin = _zValueMin;

    }

    // Slider Settings, sliderの設定
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

    // Slider Adjustments, sliderの調整
    private void Start()
    {
        magnificationRatio = 7f;
        cropDistance = 25f;
        compressNearest = 0.1f;
        compressFarthest = 2.0f;
        compressDistance = 1.9f;

        sliderMagnificationRatio.value = magnificationRatio;
        sliderCropDistance.value = cropDistance;
        sliderCompressNearest.value = compressNearest;
        sliderCompressFarthest.value = compressFarthest;
        sliderCompressDistance.value = compressDistance;

        vMR.text = sliderMagnificationRatio.value.ToString("f0");
        vCropD.text = sliderCropDistance.value.ToString("f1");
        vCompN.text = sliderCompressNearest.value.ToString("f1");
        vCompF.text = sliderCompressFarthest.value.ToString("f1");
        vCompD.text = sliderCompressDistance.value.ToString("f1");        
    }

    // Set Slider Values, sliderの値が変わった時に値の大小を調整の上で表示
    public void SliderValueChanged()
    {
        float _compressNearest = sliderCompressNearest.value;
        float _compressFarthest = sliderCompressFarthest.value;
        float _compressDistance = sliderCompressDistance.value;
        if (_compressFarthest < _compressNearest) { _compressFarthest = _compressNearest + 0.1f; }
        if (_compressDistance > (_compressFarthest - _compressNearest)) { _compressDistance = _compressFarthest - _compressNearest - 0.1f; }
        sliderCompressFarthest.value = _compressFarthest;
        sliderCompressDistance.value = _compressDistance;

        vMR.text = sliderMagnificationRatio.value.ToString("f0");
        vCropD.text = sliderCropDistance.value.ToString("f1");
        vCompN.text = sliderCompressNearest.value.ToString("f1");
        vCompF.text = sliderCompressFarthest.value.ToString("f1");
        vCompD.text = sliderCompressDistance.value.ToString("f1");

    }
    private void Update()
    {
        if (isMeshCreated)
        {
            // Update z coordinates, Z座標を更新
            UpdateVertexZPositions(i =>
            {
                float zValue;
                magnificationRatio = sliderMagnificationRatio.value;
                cropDistance = sliderCropDistance.value;
                compressNearest = sliderCompressNearest.value;
                compressFarthest = sliderCompressFarthest.value;
                compressDistance = sliderCompressDistance.value;

                zValue = Mathf.Min((zValues[i] - zValueMin) * Mathf.Sqrt(magnificationRatio), cropDistance);

                // Adjustment of image depth, 画像の距離感の調整用
                if(compressFarthest < compressNearest) { compressFarthest = compressNearest + 0.1f; }
                if(compressDistance >(compressFarthest - compressNearest)) { compressDistance = compressFarthest - compressNearest - 0.1f; }
                if(zValue > compressNearest && zValue < compressFarthest)
                {
                    zValue = (compressNearest + (zValue - compressNearest) / (compressFarthest - compressNearest) * compressDistance);
                }
                else if(zValue >= compressFarthest)
                {
                    zValue = Mathf.Max(compressNearest, (zValue - compressFarthest) + compressDistance + compressNearest);
                }
                return zValue;
        //      float x = vertices[i].x;
        //      float y = vertices[i].y;
        //      return Mathf.Sin(Time.time + x + y) * 0.5f; // Sine wave, 振幅0.5のサイン波
            });
        }
    }

    // Update Vertex Z Position, 頂点のZ座標を変更するメソッド
    public void UpdateVertexZPositions(System.Func<int, float> zPositionFunc)
    {
         // Update each vertices, 各頂点のZ座標を更新
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].z = zPositionFunc(i);
        }

        // Set vertices to mesh, メッシュに頂点配列を再設定
        mesh.vertices = vertices;

        // Recalculate normals and bounding volumes, 必要に応じて法線とバウンディングボリュームを再計算
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    public void CreateMesh(int meshWidth, int meshHeight)
    {

        ////Start to create Mesh, Mesh の作成開始
        mesh = new Mesh();
        // Using UInt32 index, 頂点数が65535を超える場合は32ビットインデックスを使用
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

        // Create vertices and UV in XY plane, 頂点とUVの生成（XY平面上に配置）
        for (int y = 0; y < verticesPerColumn; y++)
        {
            for (int x = 0; x < verticesPerRow; x++)
            {
                int index = y * verticesPerRow + x;
                vertices[index] = new Vector3(x * cellSize, y * cellSize, 0); // Zを0に設定
                uvs[index] = new Vector2((float)x / meshWidth, (float)y / meshHeight);
            }
        }

        // Create triangles, 三角形の生成
        int triangleIndex = 0;
        for (int y = 0; y < meshHeight; y++)
        {
            for (int x = 0; x < meshWidth; x++)
            {
                int bottomLeft = y * verticesPerRow + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + verticesPerRow;
                int topRight = topLeft + 1;

                // First Triangle, 1つ目の三角形
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;

                // Second Triangle, 2つ目の三角形
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = bottomRight;
            }
        }

        // Apply data to mesh, メッシュにデータを適用
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        // Recalculate normals, 法線の再計算
        mesh.RecalculateNormals();

        // Set mesh to MeshFilter, MeshFilterにメッシュを設定
        MeshFilter mf = GetComponent<MeshFilter>();
        mf.mesh = mesh;

        //// Finish, Mesh の作成終了
        //Debug.Log("vertices.Length; " + vertices.Length);
    }
}
