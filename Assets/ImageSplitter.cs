using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.IO;
//using System.Windows.Forms; // Import System.Windows.Forms 名前空間をインポート
//using System;
//using System.Linq; // for basic functions, 基本的な機能用
using SFB;
using System;

// Definition of custom UnityEvent, UnityEvent のカスタム定義
[System.Serializable]
public class ImageCreatedEvent : UnityEvent<GameObject, Sprite> { }

public class ImageSplitter : MonoBehaviour
{
    // Target frame size (in pixels), 目標の枠サイズ（ピクセル単位）
    private const int TARGET_WIDTH = 360;
    private const int TARGET_HEIGHT = 640;

    // Variable that holds a reference to the last displayed image, 前回表示した画像の参照を保持する変数
    private GameObject previousImageObject;

    // Internal fields of OFFSET (configurable in Inspector), OFFSETの内部フィールド（インスペクターで設定可能）
    [SerializeField]
    private float _offset = 90f;

    /// <summary>
    /// 他のスクリプトから参照可能なOFFSETプロパティ（読み取り専用）。
    /// 画像のx座標を右にずらすためのオフセット値。
    /// </summary>
    public float OFFSET
    {
        get { return _offset; }
    }

    // originalWidthの内部フィールド
    [SerializeField]
    private int _originalWidth = 0;

    /// <summary>
    /// 他のスクリプトから参照可能なOriginalWidthプロパティ（読み取り専用）。
    /// Width after separated, 分割後のleftImage.pngの幅を示します。
    /// </summary>
    public int OriginalWidth
    {
        get { return _originalWidth; }
    }

    // originalHeightの内部フィールド
    [SerializeField]
    private int _originalHeight = 0;

    /// <summary>
    /// 他のスクリプトから参照可能なOriginalHeightプロパティ（読み取り専用）。
    /// Height after separeted, 分割後のleftImage.pngの高さを示します。
    /// </summary>
    public int OriginalHeight
    {
        get { return _originalHeight; }
    }

    // scaleの内部フィールド（基準スケール）
    [SerializeField]
    private float _scale = 1f;

    /// <summary>
    /// 他のスクリプトから参照可能なScaleプロパティ（読み取り専用）。
    /// 画像の基準スケーリング係数。
    /// </summary>
    public float InitialScale
    {
        get { return _scale; }
    }

    // pixelZdataの内部フィールド
    private float[,] _pixelZMatrix;

    /// <summary>
    /// Right side depth data (UInt32 stored in RGBA32) float[] (read-only) that can be referenced from other scripts.
    /// 他のスクリプトから参照可能な右側デプスデータ（RGBA32にUInt32が格納）float[]（読み取り専用）。
    /// 奥行き参照の元データ
    /// </summary>
    public float[,] PixelZMatrix
    {
        get { return _pixelZMatrix; }
    }

    // pixelZdataの最大値の内部フィールド
    private float _pixelZMax;
    public float PixelZMax
    {
        get { return _pixelZMax; }
    }

    // メッシュサイズのDropdownリスト
    [SerializeField]
    public GameObject dropdownItem;
    Dropdown dropdown;
    private int dropdownValue;

    // Mesh Width の内部フィールド
    [SerializeField]
    private int _meshX;

    /// <summary>
    /// Choosed mesh width from dropdown list, ドロップダウンの選択結果によるメッシュの幅の数
    /// </summary>
    public int meshX
    {
        get { return _meshX; }
    }

    // Mesh Height の内部フィールド
    [SerializeField]
    private int _meshY;

    /// <summary>
    /// Choosed mesh height from dropdown list, ドロップダウンの選択結果によるメッシュの縦の数
    /// </summary>
    public int meshY
    {
        get { return _meshY; }
    }

    /// <summary>
    /// Imageが作成された際に発生するUnityEvent。
    /// 引数として新しく作成されたImageのGameObjectとSpriteを渡します。
    /// </summary>
    [SerializeField]
    public ImageCreatedEvent OnImageCreated;

    private void Start()
    {
        dropdown = dropdownItem.GetComponent<Dropdown>();
        //dropdown = GameObject.Find("Dropdown").GetComponent<Dropdown>();
    }

    public void OnDropdownSelected(int value)
    {
        dropdownValue = value;
    }


    /// <summary>
    /// Attached to "Load" button, ボタンの OnClick イベントにアタッチするメソッド。
    /// ファイルダイアログを開き、選択された画像を処理します。
    /// </summary>
    public void OpenAndProcessImage()
    {
        //Debug.Log("OpenAndProcessImage called.");
        /*
                // OpenFileDialog の設定
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "PNG Files|*.png",
                    Title = "Select a PNG Image",
                    RestoreDirectory = true
                };

                // ファイルダイアログを表示
                DialogResult result = openFileDialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(openFileDialog.FileName))
                {
                    string filePath = openFileDialog.FileName;
                    StartCoroutine(SplitAndSaveImage(filePath));
                }
                else
                {
                    //Debug.LogWarning("No file selected or dialog was canceled.");
                }
        */
        var paths = StandaloneFileBrowser.OpenFilePanel("Select RGBDE PNG Image", "", "png", false);
        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            SetMeshSize(dropdownValue);
            string filePath = paths[0].ToString();
            StartCoroutine(SplitImage(filePath));
            //Debug.Log(filePath);
        }
    }

    /// <summary>
    /// List of dropdown for mesh resolutions, Dropdown リストに基づくメッシュ解像度のプロパティ設定
    /// </summary>
    private void SetMeshSize(int value)
    {
        if (value == 0)
        {
            _meshX = 360;
            _meshY = 640;
        }
        else if (value == 1)
        {
            _meshX = 720;
            _meshY = 1280;
        }
        else
        {
            _meshX = 1440;
            _meshY = 2560;
        }

    }

    /// <summary>
    /// 画像を分割し、保存するコルーチン。
    /// </summary>
    /// <param name="filePath">選択された画像のパス</param>
    /// <returns>IEnumerator</returns>
    private IEnumerator SplitImage(string filePath)
    {
        //Debug.Log($"Splitting and saving image: {filePath}");

        // Load an image as Texture2D, 画像をTexture2Dとしてロード
        Texture2D originalTexture = LoadTexture(filePath);
        if (originalTexture == null)
        {
            //Debug.LogError("Failed to load texture.");
            yield break;
        }

        // Set image width and height, 分割する幅と高さを設定
        _originalWidth = originalTexture.width / 2;
        _originalHeight = originalTexture.height;

        // Create left texture, 左側のテクスチャを作成
        Texture2D leftTexture = new Texture2D(_originalWidth, _originalHeight, originalTexture.format, false);
        // Create right texture, 右側のテクスチャを作成（リニアカラー空間として扱う）
        Texture2D rightTexture = new Texture2D(originalTexture.width - _originalWidth, _originalHeight, originalTexture.format, false, true); // true for linear

        // Get pixel data, ピクセルデータを取得
        Color32[] originalPixels = originalTexture.GetPixels32();
        Color32[] leftPixels = new Color32[_originalWidth * _originalHeight];
        Color32[] rightPixels = new Color32[(originalTexture.width - _originalWidth) * _originalHeight];

        // Copy left image pixels, 左側のピクセルをコピー
        for (int y = 0; y < _originalHeight; y++)
        {
            for (int x = 0; x < _originalWidth; x++)
            {
                leftPixels[y * _originalWidth + x] = originalPixels[y * originalTexture.width + x];
            }
        }

        // Copy right image pixels,右側のピクセルをコピー
        for (int y = 0; y < _originalHeight; y++)
        {
            for (int x = _originalWidth; x < originalTexture.width; x++)
            {
                rightPixels[y * (originalTexture.width - _originalWidth) + (x - _originalWidth)] = originalPixels[y * originalTexture.width + x];
            }
        }

        // Calculate the depth information from the right half of the image and make it into a two-dimensional array and set it in the property.
        // 右半分の画像から深度情報を計算し２次元配列にしてプロパティにセット
        float[] pixelZData = new float[rightPixels.GetLength(0)];

        for (int i = 0; i < pixelZData.Length; i++)
        {
            pixelZData[i] = (rightPixels[i].a * 16777216f + rightPixels[i].b * 65536f + rightPixels[i].g * 256f + rightPixels[i].r) / 10000f;
        }

        _pixelZMatrix = new float[(int)(pixelZData.Length / _originalWidth), (int)(pixelZData.Length / _originalHeight)];
        for (int j = 0; j < _originalHeight; j++)
        {
            for (int i = 0; i < _originalWidth; i++)
            {
                _pixelZMatrix[j, i] = pixelZData[j * _originalWidth + i];
            }
        }

        // Set max depth, プロパティに最深値をセット
        _pixelZMax = Mathf.Max(pixelZData);

        // Set pixel data, ピクセルデータを設定
        leftTexture.SetPixels32(leftPixels);
        leftTexture.Apply();

        // Load the left image as a sprite and place it on Canvas, 左側の画像をスプライトとしてロードし、Canvas に配置
        StartCoroutine(LoadSpriteFromTexture(leftTexture));

        // Release memory, メモリ解放
        Destroy(originalTexture);
        Destroy(rightTexture);

        yield return null;
    }

    /// <summary>
    /// 指定されたパスからスプライトをロードし、UI の Image コンポーネントで表示します。
    /// 前回表示した画像があれば削除します。
    /// </summary>
    /// <param name="path">画像ファイルのパス</param>
    /// <returns>IEnumerator</returns>
    private IEnumerator LoadSpriteFromTexture(Texture2D loadedTexture)
    {
        // Delete any previously displayed images, 前回表示した画像があれば削除
        if (previousImageObject != null)
        {
            //Debug.Log("Destroying previous image.");
            Destroy(previousImageObject);
            previousImageObject = null;
        }

        if (loadedTexture)
        {
            // Create Sprite, スプライトを作成
            Sprite loadedSprite = Sprite.Create(loadedTexture, new Rect(0, 0, loadedTexture.width, loadedTexture.height),
                new Vector2(0.5f, 0.5f));

            // Create GameObject for left image on Canvas, Canvas上にImage用のGameObjectを作成
            GameObject imageObject = new GameObject("LeftImageSprite");
            imageObject.transform.SetParent(this.transform, false); // ImageSplitterManager を親に設定

            // Add image component, Imageコンポーネントを追加
            UnityEngine.UI.Image uiImage = imageObject.AddComponent<UnityEngine.UI.Image>();
            uiImage.sprite = loadedSprite;

            // Get RectTransform of the image, ImageのRectTransformを取得
            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // Shift to the right by OFFSET, OFFSET だけ右にずらす
            Vector2 offsetPosition = new Vector2(_offset, 0f);
            rectTransform.anchoredPosition = offsetPosition;

            // Calculate Scaling factor, スケーリング係数の計算
            float scaleFactor = CalculateScaleFactor(_originalWidth, _originalHeight, TARGET_WIDTH, TARGET_HEIGHT);
            _scale = scaleFactor;

            Vector2 newSize = CalculateScaledSize(_originalWidth, _originalHeight, TARGET_WIDTH, TARGET_HEIGHT, scaleFactor);
            rectTransform.sizeDelta = newSize;

            // Set sortingOrder of CanvasRenderer of the image, ImageのCanvasRendererのsortingOrderを設定
            CanvasRenderer canvasRenderer = imageObject.GetComponent<CanvasRenderer>();
            canvasRenderer.SetAlpha(1f); // 透明度を設定（必要に応じて）

            // Adjust the sort order in Canvas (order in Hierarchy determines display order), Canvas内のソート順を調整（Hierarchyでの順序が表示順を決定）
            imageObject.transform.SetAsLastSibling();

            // Keep previous image as reference, 前回の画像を参照として保持
            previousImageObject = imageObject;

            // Invoke event to notify other objects, イベントを発火して他のオブジェクトに通知
            OnImageCreated?.Invoke(imageObject, loadedSprite);
        }
        else
        {
            //Debug.LogError("Failed to load image as Texture2D");
        }

        yield return null;
    }

    /// <summary>
    /// Texture2D をロードします。
    /// </summary>
    /// <param name="filePath">画像ファイルのパス</param>
    /// <returns>ロードされた Texture2D オブジェクト</returns>
    private Texture2D LoadTexture(string filePath)
    {
        if (!File.Exists(filePath))
        {
            //Debug.LogError($"File not found: {filePath}");
            return null;
        }

        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false); // false for linear by default
        if (tex.LoadImage(fileData))
        {
            return tex;
        }
        else
        {
            //Debug.LogError("Failed to load image data into Texture2D.");
            return null;
        }
    }

    /// <summary>
    /// スケーリング係数を計算します（フィールドを更新しません）。
    /// </summary>
    /// <param name="originalWidth">元の画像の幅</param>
    /// <param name="originalHeight">元の画像の高さ</param>
    /// <param name="maxWidth">最大幅</param>
    /// <param name="maxHeight">最大高さ</param>
    /// <returns>スケーリング係数</returns>
    private float CalculateScaleFactor(int originalWidth, int originalHeight, float maxWidth, float maxHeight)
    {
        float widthRatio = maxWidth / originalWidth;
        float heightRatio = maxHeight / originalHeight;
        float scaleFactor = Mathf.Min(widthRatio, heightRatio);
        return scaleFactor;
    }

    /// <summary>
    /// 指定された枠に収まるように縦横比を維持した新しいサイズを計算します。
    /// </summary>
    /// <param name="originalWidth">元の画像の幅</param>
    /// <param name="originalHeight">元の画像の高さ</param>
    /// <param name="maxWidth">最大幅</param>
    /// <param name="maxHeight">最大高さ</param>
    /// <param name="scaleFactor">スケーリング係数</param>
    /// <returns>新しいサイズ (幅, 高さ)</returns>
    private Vector2 CalculateScaledSize(int originalWidth, int originalHeight, float maxWidth, float maxHeight, float scaleFactor)
    {
        float newWidth = originalWidth * scaleFactor;
        float newHeight = originalHeight * scaleFactor;
        return new Vector2(newWidth, newHeight);
    }
}
