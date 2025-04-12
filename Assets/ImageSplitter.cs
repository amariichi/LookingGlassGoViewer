using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.IO;
using SFB;
using System;

// Definition of custom UnityEvent
[System.Serializable]
public class ImageCreatedEvent : UnityEvent<GameObject, Sprite> { }

public class ImageSplitter : MonoBehaviour
{
    // --- 定数 ---
    private const int TARGET_WIDTH = 360;   // 左画像のターゲット幅（ピクセル）
    private const int TARGET_HEIGHT = 640;  // 左画像のターゲット高さ（ピクセル）

    // --- Inspectorで設定可能なフィールド ---
    [SerializeField] private float _offset = 90f; // 左画像の表示位置オフセット（右方向）
    [SerializeField] private int _meshX;          // メッシュの横分割数
    [SerializeField] private int _meshY;          // メッシュの縦分割数
    [SerializeField] public GameObject dropdownItem; // メッシュ解像度選択用ドロップダウン
    [SerializeField] public ImageCreatedEvent OnImageCreated; // 画像生成時イベント

    // --- 内部状態 ---
    private GameObject previousImageObject; // 前回表示した画像オブジェクト
    private Dropdown dropdown;              // ドロップダウンコンポーネント
    private int dropdownValue;              // ドロップダウンの選択値

    [SerializeField] private int _originalWidth = 0;   // 元画像の幅（左画像）
    [SerializeField] private int _originalHeight = 0;  // 元画像の高さ（左画像）
    [SerializeField] private float _scale = 1f;        // 左画像のスケール

    private float[,] _pixelZMatrix; // 右画像から得た深度情報
    private float _pixelZMax;       // 深度の最大値

    // --- プロパティ ---
    public float OFFSET => _offset;                 // オフセット値
    public int OriginalWidth => _originalWidth;     // 左画像の幅
    public int OriginalHeight => _originalHeight;   // 左画像の高さ
    public float InitialScale => _scale;            // 左画像のスケール
    public float[,] PixelZMatrix => _pixelZMatrix;  // 深度マトリクス
    public float PixelZMax => _pixelZMax;           // 深度の最大値
    public int meshX => _meshX;                     // メッシュ横分割数
    public int meshY => _meshY;                     // メッシュ縦分割数

    private void Start()
    {
        dropdown = dropdownItem.GetComponent<Dropdown>();
    }

    /// <summary>
    /// ドロップダウン選択時のコールバック
    /// </summary>
    public void OnDropdownSelected(int value)
    {
        dropdownValue = value;
    }

    /// <summary>
    /// 「Load」ボタンにアタッチするメソッド。画像ファイルを選択し、分割処理を開始する。
    /// </summary>
    public void OpenAndProcessImage()
    {
        var paths = StandaloneFileBrowser.OpenFilePanel("RGBDE PNG画像を選択", "", "png", false);
        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            SetMeshSize(dropdownValue);
            string filePath = paths[0];
            StartCoroutine(SplitImage(filePath));
        }
    }

    /// <summary>
    /// ドロップダウンの選択値に応じてメッシュ解像度を設定
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
    /// 画像を分割し、深度情報を抽出してプロパティにセットする
    /// </summary>
    /// <param name="filePath">選択された画像ファイルのパス</param>
    /// <returns>IEnumerator</returns>
    private IEnumerator SplitImage(string filePath)
    {
        // 画像をTexture2Dとして読み込み
        Texture2D originalTexture = LoadTexture(filePath);
        if (originalTexture == null)
        {
            yield break;
        }

        // 左右画像の幅・高さを設定
        _originalWidth = originalTexture.width / 2;
        _originalHeight = originalTexture.height;

        // 左画像テクスチャ生成
        Texture2D leftTexture = new Texture2D(_originalWidth, _originalHeight, originalTexture.format, false);
        // 右画像テクスチャ生成（深度情報用）
        Texture2D rightTexture = new Texture2D(originalTexture.width - _originalWidth, _originalHeight, originalTexture.format, false, true);

        // ピクセルデータ取得
        Color32[] originalPixels = originalTexture.GetPixels32();
        Color32[] leftPixels = new Color32[_originalWidth * _originalHeight];
        Color32[] rightPixels = new Color32[(originalTexture.width - _originalWidth) * _originalHeight];

        // 左画像ピクセルコピー
        for (int y = 0; y < _originalHeight; y++)
        {
            for (int x = 0; x < _originalWidth; x++)
            {
                leftPixels[y * _originalWidth + x] = originalPixels[y * originalTexture.width + x];
            }
        }

        // 右画像ピクセルコピー
        for (int y = 0; y < _originalHeight; y++)
        {
            for (int x = _originalWidth; x < originalTexture.width; x++)
            {
                rightPixels[y * (originalTexture.width - _originalWidth) + (x - _originalWidth)] = originalPixels[y * originalTexture.width + x];
            }
        }

        // 右画像から深度情報を抽出し一次元配列に格納
        float[] pixelZData = new float[rightPixels.Length];
        for (int i = 0; i < pixelZData.Length; i++)
        {
            pixelZData[i] = (rightPixels[i].a * 16777216f + rightPixels[i].b * 65536f + rightPixels[i].g * 256f + rightPixels[i].r) / 10000f;
        }

        // 深度情報を二次元配列に変換
        _pixelZMatrix = new float[_originalHeight, _originalWidth];
        for (int j = 0; j < _originalHeight; j++)
        {
            for (int i = 0; i < _originalWidth; i++)
            {
                _pixelZMatrix[j, i] = pixelZData[j * _originalWidth + i];
            }
        }

        // 深度の最大値を計算
        _pixelZMax = Mathf.Max(pixelZData);

        // 左画像のピクセルデータをセット
        leftTexture.SetPixels32(leftPixels);
        leftTexture.Apply();

        // 左画像をSprite化してCanvasに配置
        StartCoroutine(LoadSpriteFromTexture(leftTexture));

        // メモリ解放
        Destroy(originalTexture);
        Destroy(rightTexture);

        yield return null;
    }

    /// <summary>
    /// 指定したTexture2DをUIのImageコンポーネントとしてCanvasに表示する
    /// 既存の画像があれば削除する
    /// </summary>
    /// <param name="loadedTexture">表示するテクスチャ</param>
    /// <returns>IEnumerator</returns>
    private IEnumerator LoadSpriteFromTexture(Texture2D loadedTexture)
    {
        // 既存の画像を削除
        if (previousImageObject != null)
        {
            Destroy(previousImageObject);
            previousImageObject = null;
        }

        if (loadedTexture)
        {
            // Sprite生成
            Sprite loadedSprite = Sprite.Create(loadedTexture, new Rect(0, 0, loadedTexture.width, loadedTexture.height),
                new Vector2(0.5f, 0.5f));

            // Canvas上にImage用GameObject生成
            GameObject imageObject = new GameObject("LeftImageSprite");
            imageObject.transform.SetParent(this.transform, false);

            // Imageコンポーネント追加
            UnityEngine.UI.Image uiImage = imageObject.AddComponent<UnityEngine.UI.Image>();
            uiImage.sprite = loadedSprite;

            // RectTransform設定
            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // 右方向にオフセット
            Vector2 offsetPosition = new Vector2(_offset, 0f);
            rectTransform.anchoredPosition = offsetPosition;

            // スケール計算
            float scaleFactor = CalculateScaleFactor(_originalWidth, _originalHeight, TARGET_WIDTH, TARGET_HEIGHT);
            _scale = scaleFactor;

            Vector2 newSize = CalculateScaledSize(_originalWidth, _originalHeight, scaleFactor);
            rectTransform.sizeDelta = newSize;

            // CanvasRendererの透明度設定
            CanvasRenderer canvasRenderer = imageObject.GetComponent<CanvasRenderer>();
            canvasRenderer.SetAlpha(1f);

            // Canvas内で一番手前に表示
            imageObject.transform.SetAsLastSibling();

            // 参照保持
            previousImageObject = imageObject;

            // イベント発火
            OnImageCreated?.Invoke(imageObject, loadedSprite);
        }

        yield return null;
    }

    /// <summary>
    /// 指定パスの画像ファイルをTexture2Dとして読み込む
    /// </summary>
    /// <param name="filePath">画像ファイルのパス</param>
    /// <returns>読み込んだTexture2D</returns>
    private Texture2D LoadTexture(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        if (tex.LoadImage(fileData))
        {
            return tex;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// 指定した最大サイズに収まるようなスケール係数を計算する
    /// </summary>
    /// <param name="originalWidth">元画像の幅</param>
    /// <param name="originalHeight">元画像の高さ</param>
    /// <param name="maxWidth">最大幅</param>
    /// <param name="maxHeight">最大高さ</param>
    /// <returns>スケール係数</returns>
    private float CalculateScaleFactor(int originalWidth, int originalHeight, float maxWidth, float maxHeight)
    {
        float widthRatio = maxWidth / originalWidth;
        float heightRatio = maxHeight / originalHeight;
        float scaleFactor = Mathf.Min(widthRatio, heightRatio);
        return scaleFactor;
    }

    /// <summary>
    /// 指定したスケール係数で拡大縮小した場合の新しいサイズを計算する
    /// </summary>
    /// <param name="originalWidth">元画像の幅</param>
    /// <param name="originalHeight">元画像の高さ</param>
    /// <param name="scaleFactor">スケール係数</param>
    /// <returns>新しいサイズ (幅, 高さ)</returns>
    private Vector2 CalculateScaledSize(int originalWidth, int originalHeight, float scaleFactor)
    {
        float newWidth = originalWidth * scaleFactor;
        float newHeight = originalHeight * scaleFactor;
        return new Vector2(newWidth, newHeight);
    }
}
