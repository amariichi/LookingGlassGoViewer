using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Events;
using Color = UnityEngine.Color;
using Graphics = UnityEngine.Graphics;

[System.Serializable]
public class ImageCroppedEvent : UnityEvent<int, int, bool> { }

public class ImageCropper : MonoBehaviour
{
    // --- Inspector fields ---
    [SerializeField] private ImageSplitter imageSplitter;
    [SerializeField] private ImageManipulator imageManipulator;
    [SerializeField] private Material materialL;
    [SerializeField] private int meshWidth;
    [SerializeField] private int meshHeight;
    [SerializeField] public ImageCroppedEvent OnImageCropped;

    // --- Internal state ---
    private Texture2D leftOriginalTexture;
    private Texture2D scaledTexture;
    public Texture2D finalLeftImage { get; private set; }

    private float lastDisplayedPositionX;
    private float lastDisplayedPositionY;
    private float lastDisplayedScale;

    private int frameWidth, frameHeight;
    private int originalWidth, originalHeight;
    private float initialScale;

    private int meshMaxWidth, meshMaxHeight;

    // Crop/paste parameters
    private int _cropPositionX, _cropPositionY, _cropWidth, _cropHeight, _pasteX, _pasteY;

    // Z値配列
    private float[] _zValues;
    public float[] zValues => _zValues;

    private void Start()
    {
        // imageManipulatorが設定されていれば初期表示パラメータを保存
        if (imageManipulator != null)
        {
            lastDisplayedPositionX = imageManipulator.displayedPositionX;
            lastDisplayedPositionY = imageManipulator.displayedPositionY;
            lastDisplayedScale = imageManipulator.displayedScale;
        }
    }

    /// <summary>
    /// 画像生成時の初期化ハンドラ
    /// </summary>
    public void OnImageCreatedHandler(GameObject imageObject, Sprite sprite)
    {
        if (imageObject == null || sprite == null) return;

        leftOriginalTexture = sprite.texture;
        originalWidth = imageSplitter.OriginalWidth;
        originalHeight = imageSplitter.OriginalHeight;
        initialScale = imageSplitter.InitialScale;

        SetFrameSize();
        SetMeshSize();

        StartCoroutine(ProcessImages());
        OnImageCropped?.Invoke(meshWidth, meshHeight, true);
    }

    private void Update()
    {
        if (imageManipulator == null) return;

        if (HasDisplayParametersChanged())
        {
            UpdateDisplayParameters();
            StartCoroutine(ProcessImages());
            OnImageCropped?.Invoke(meshWidth, meshHeight, false);
        }
    }

    /// <summary>
    /// 表示パラメータが変更されたか判定
    /// </summary>
    private bool HasDisplayParametersChanged()
    {
        return !Mathf.Approximately(lastDisplayedPositionX, imageManipulator.displayedPositionX)
            || !Mathf.Approximately(lastDisplayedPositionY, imageManipulator.displayedPositionY)
            || !Mathf.Approximately(lastDisplayedScale, imageManipulator.displayedScale);
    }

    /// <summary>
    /// 表示パラメータを更新
    /// </summary>
    private void UpdateDisplayParameters()
    {
        lastDisplayedPositionX = imageManipulator.displayedPositionX;
        lastDisplayedPositionY = imageManipulator.displayedPositionY;
        lastDisplayedScale = imageManipulator.displayedScale;
        SetMeshSize();
    }

    /// <summary>
    /// フレームサイズを計算
    /// </summary>
    private void SetFrameSize()
    {
        float aspect = (float)originalWidth / originalHeight;
        if (aspect <= 9f / 16f)
        {
            frameHeight = originalHeight;
            frameWidth = Mathf.RoundToInt(originalHeight * 9f / 16f);
        }
        else
        {
            frameWidth = originalWidth;
            frameHeight = Mathf.RoundToInt(originalWidth * 16f / 9f);
        }
    }

    /// <summary>
    /// メッシュサイズを計算
    /// </summary>
    private void SetMeshSize()
    {
        meshMaxWidth = imageSplitter.meshX;
        meshMaxHeight = imageSplitter.meshY;
        meshWidth = Mathf.Min(frameWidth, meshMaxWidth);
        meshHeight = Mathf.Min(frameHeight, meshMaxHeight);
    }

    private IEnumerator ProcessImages()
    {
        if (leftOriginalTexture == null)
        {
            yield break;
        }

        finalLeftImage = CropAndCreateTexture(leftOriginalTexture, frameWidth, frameHeight);

        if (materialL.mainTexture != null)
        {
            Destroy(materialL.mainTexture);
        }
        AssignTextureToMaterial(finalLeftImage, materialL);
        _zValues = CalculateZValues();

        yield return null;
    }

    /// <summary>
    /// テクスチャをクロップし新しいテクスチャを生成
    /// </summary>
    private Texture2D CropAndCreateTexture(Texture2D originalTexture, int frameWidth, int frameHeight)
    {
        // フレーム左上・右下座標
        Vector2 frameLT = new Vector2(-frameWidth / 2f, frameHeight / 2f);
        Vector2 frameRB = new Vector2(frameWidth / 2f, -frameHeight / 2f);

        // 画像の左上・右下（表示位置・スケール反映）
        Vector2 imageLT = new Vector2(
            -originalWidth / 2f * lastDisplayedScale + lastDisplayedPositionX / initialScale,
            originalHeight / 2f * lastDisplayedScale + lastDisplayedPositionY / initialScale
        );
        Vector2 imageRB = new Vector2(
            originalWidth / 2f * lastDisplayedScale + lastDisplayedPositionX / initialScale,
            -originalHeight / 2f * lastDisplayedScale + lastDisplayedPositionY / initialScale
        );

        // 透明で初期化した新規テクスチャ
        Texture2D newTexture = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false);
        Color[] fillPixels = new Color[frameWidth * frameHeight];
        Array.Fill(fillPixels, new Color(0f, 0f, 0f, 0f));
        newTexture.SetPixels(fillPixels);

        // 画像がフレーム内に存在する場合のみ処理
        int pasteW = 0, pasteH = 0, srcX = 0, srcY = 0;
        if (imageRB.y < frameLT.y && imageRB.x > frameLT.x && imageLT.x < frameRB.x && imageLT.y > frameRB.y)
        {
            // フレームと画像の重なり範囲を計算
            Vector2 cropLT = new Vector2(
                Mathf.Max(imageLT.x, frameLT.x),
                Mathf.Min(imageLT.y, frameLT.y)
            );
            Vector2 cropRB = new Vector2(
                Mathf.Min(imageRB.x, frameRB.x),
                Mathf.Max(imageRB.y, frameRB.y)
            );

            // 元画像からクロップする範囲（左下基準）
            int cropLBX = Mathf.RoundToInt((cropLT.x - imageLT.x) / lastDisplayedScale);
            int cropLBY = Mathf.RoundToInt((cropRB.y - imageRB.y) / lastDisplayedScale);
            int cropW = Mathf.RoundToInt((cropRB.x - cropLT.x) / lastDisplayedScale);
            int cropH = Mathf.RoundToInt((cropLT.y - cropRB.y) / lastDisplayedScale);

            Color[] croppedPixels = originalTexture.GetPixels(cropLBX, cropLBY, cropW, cropH);

            // 一時テクスチャを作成しスケーリング
            Texture2D croppedTex = new Texture2D(cropW, cropH, TextureFormat.RGBA32, false);
            croppedTex.SetPixels(0, 0, cropW, cropH, croppedPixels);
            croppedTex.Apply();

            // lastDisplayedScaleでリサイズ
            int scaledW = Mathf.Max(1, Mathf.RoundToInt(cropW * lastDisplayedScale));
            int scaledH = Mathf.Max(1, Mathf.RoundToInt(cropH * lastDisplayedScale));
            Texture2D scaledTex = ScaleTexture(croppedTex, lastDisplayedScale);

            // 貼り付け位置（フレーム内座標系→テクスチャ座標系）
            int pasteX = Mathf.RoundToInt(cropLT.x + frameWidth / 2f);
            int pasteY = Mathf.RoundToInt(cropRB.y + frameHeight / 2f);

            // フレーム外にはみ出さないよう厳密に調整
            pasteW = scaledTex.width;
            pasteH = scaledTex.height;
            srcX = 0; srcY = 0;
            if (pasteX < 0) { srcX = -pasteX; pasteW += pasteX; pasteX = 0; }
            if (pasteY < 0) { srcY = -pasteY; pasteH += pasteY; pasteY = 0; }
            if (pasteX + pasteW > frameWidth) { pasteW = frameWidth - pasteX; }
            if (pasteY + pasteH > frameHeight) { pasteH = frameHeight - pasteY; }

            if (pasteW > 0 && pasteH > 0)
            {
                Color[] scaledPixels = scaledTex.GetPixels(srcX, srcY, pasteW, pasteH);
                newTexture.SetPixels(pasteX, pasteY, pasteW, pasteH, scaledPixels);
            }

            // 内部状態保存
            _cropPositionX = cropLBX;
            _cropPositionY = cropLBY;
            _cropWidth = pasteW;
            _cropHeight = pasteH;
            _pasteX = pasteX;
            _pasteY = pasteY;

            UnityEngine.Object.Destroy(croppedTex);
            UnityEngine.Object.Destroy(scaledTex);
        }

        // 端の行・列を必ず透明で上書き
        for (int x = 0; x < frameWidth; x++)
        {
            newTexture.SetPixel(x, 0, new Color(0f, 0f, 0f, 0f));
            newTexture.SetPixel(x, frameHeight - 1, new Color(0f, 0f, 0f, 0f));
        }
        for (int y = 0; y < frameHeight; y++)
        {
            newTexture.SetPixel(0, y, new Color(0f, 0f, 0f, 0f));
            newTexture.SetPixel(frameWidth - 1, y, new Color(0f, 0f, 0f, 0f));
        }
        
        newTexture.Apply();
        return newTexture;
    }

    /// <summary>
    /// テクスチャをマテリアルに割り当て
    /// </summary>
    private void AssignTextureToMaterial(Texture2D texture, Material material)
    {
        if (material == null || texture == null) return;
        material.mainTexture = texture;
    }

    /// <summary>
    /// テクスチャを指定スケールでリサイズ
    /// </summary>
    private static Texture2D ScaleTexture(Texture2D source, float scale)
    {
        if (source == null || scale <= 0f) return null;

        int newWidth = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
        int newHeight = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Point;

        Graphics.Blit(source, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D scaled = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        scaled.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        scaled.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return scaled;
    }

    /// <summary>
    /// Z値配列を計算
    /// </summary>
    private float[] CalculateZValues()
    {
        float[] zValues = new float[(meshWidth + 1) * (meshHeight + 1)];
        float[,] pixelZMatrix = imageSplitter.PixelZMatrix;
        float zValueMax = imageSplitter.PixelZMax;

        Array.Fill(zValues, zValueMax);

        float meshScale = (float)meshWidth / frameWidth;

        // スケーリング後の貼り付け範囲
        int pasteX = _pasteX;
        int pasteY = _pasteY;
        int pasteW = _cropWidth;
        int pasteH = _cropHeight;
        float scale = lastDisplayedScale;

        for (int j = 0; j <= meshHeight; j++)
        {
            for (int i = 0; i <= meshWidth; i++)
            {
                // スケーリング後の貼り付け範囲内か判定
                bool inCrop =
                    i >= pasteX * meshScale && i <= (pasteX + pasteW) * meshScale &&
                    j >= pasteY * meshScale && j <= (pasteY + pasteH) * meshScale;

                if (inCrop)
                {
                    // mesh座標→スケーリング後テクスチャ座標→スケーリング前元画像座標
                    float texX = (i / meshScale) - pasteX; // スケーリング後テクスチャ内のX
                    float texY = (j / meshScale) - pasteY; // スケーリング後テクスチャ内のY
                    int matrixColumn = Mathf.Clamp(
                        _cropPositionX + Mathf.RoundToInt(texX / scale),
                        0, originalWidth - 1);
                    int matrixRow = Mathf.Clamp(
                        _cropPositionY + Mathf.RoundToInt(texY / scale),
                        0, originalHeight - 1);

                    int idx = (meshWidth + 1) * j + i;
                    zValues[idx] = pixelZMatrix[matrixRow, matrixColumn];
                }
            }
        }

        // 下端の行を上端の行で埋める
        for (int i = 0; i <= meshWidth; i++)
        {
            zValues[(meshWidth + 1) * meshHeight + i] = zValues[(meshWidth + 1) * (meshHeight - 1) + i];
        }
        // 右端の列を左端の列で埋める
        for (int j = 0; j <= meshHeight; j++)
        {
            zValues[(meshWidth + 1) * j + meshWidth] = zValues[(meshWidth + 1) * j + meshWidth - 1];
        }

        return zValues;
    }
}
