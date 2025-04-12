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

        // 外枠（物理的な制約）の左上・右下
        Vector2 outerFrameLT = new Vector2(-189f / initialScale, 336f / initialScale);
        Vector2 outerFrameRB = new Vector2(189f / initialScale, -336f / initialScale);

        // 画像の左上・右下（表示位置・スケール反映）
        Vector2 imageLT = new Vector2(
            -originalWidth / 2f * lastDisplayedScale + lastDisplayedPositionX / initialScale,
            originalHeight / 2f * lastDisplayedScale + lastDisplayedPositionY / initialScale
        );
        Vector2 imageRB = new Vector2(
            originalWidth / 2f * lastDisplayedScale + lastDisplayedPositionX / initialScale,
            -originalHeight / 2f * lastDisplayedScale + lastDisplayedPositionY / initialScale
        );

        // クロップ範囲初期化
        Vector2 cropFrameLT = Vector2.zero, cropFrameRB = Vector2.zero;
        Vector2 cropLT = Vector2.zero, cropRB = Vector2.zero;

        // 透明で初期化した新規テクスチャ
        Texture2D newTexture = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false);
        Color[] fillPixels = new Color[frameWidth * frameHeight];
        Array.Fill(fillPixels, new Color(0f, 0f, 0f, 0f));
        newTexture.SetPixels(fillPixels);

        // 画像がフレーム内に存在する場合のみ処理
        if (imageRB.y < frameLT.y && imageRB.x > frameLT.x && imageLT.x < frameRB.x && imageLT.y > frameRB.y)
        {
            // 外枠と画像の重なり範囲を計算
            cropFrameLT.x = Mathf.Max(imageLT.x, outerFrameLT.x);
            cropFrameRB.x = Mathf.Min(imageRB.x, outerFrameRB.x);
            cropFrameLT.y = Mathf.Min(imageLT.y, outerFrameLT.y);
            cropFrameRB.y = Mathf.Max(imageRB.y, outerFrameRB.y);

            // 元画像からクロップする範囲（左下基準）
            int cropOuterLBX = Mathf.RoundToInt((cropFrameLT.x - imageLT.x) / lastDisplayedScale);
            int cropOuterLBY = Mathf.RoundToInt((cropFrameRB.y - imageRB.y) / lastDisplayedScale);
            int cropOuterW = Mathf.RoundToInt((cropFrameRB.x - cropFrameLT.x) / lastDisplayedScale);
            int cropOuterH = Mathf.RoundToInt((cropFrameLT.y - cropFrameRB.y) / lastDisplayedScale);

            Vector2 outerLT = cropFrameLT;
            Vector2 outerRB = cropFrameRB;

            // 範囲外アクセス防止
            cropOuterLBX = Mathf.Clamp(cropOuterLBX, 0, originalTexture.width - 1);
            cropOuterLBY = Mathf.Clamp(cropOuterLBY, 0, originalTexture.height - 1);
            cropOuterW = Mathf.Clamp(cropOuterW, 1, originalTexture.width - cropOuterLBX);
            cropOuterH = Mathf.Clamp(cropOuterH, 1, originalTexture.height - cropOuterLBY);

            // 元画像から外枠部分をクロップ
            Color[] croppedOuterPixels = originalTexture.GetPixels(cropOuterLBX, cropOuterLBY, cropOuterW, cropOuterH);
            Texture2D outerFrameCroppedTexture = new Texture2D(cropOuterW, cropOuterH, TextureFormat.RGBA32, false);
            outerFrameCroppedTexture.SetPixels(0, 0, cropOuterW, cropOuterH, croppedOuterPixels);
            outerFrameCroppedTexture.Apply();

            // スケール適用
            scaledTexture = ScaleTexture(outerFrameCroppedTexture, lastDisplayedScale);

            // フレームと外枠の重なり範囲
            cropLT.x = Mathf.Max(outerLT.x, frameLT.x);
            cropRB.x = Mathf.Min(outerRB.x, frameRB.x);
            cropLT.y = Mathf.Min(outerLT.y, frameLT.y);
            cropRB.y = Mathf.Max(outerRB.y, frameRB.y);

            int cropLBX = Mathf.RoundToInt(cropLT.x - outerLT.x);
            int cropLBY = Mathf.RoundToInt(cropRB.y - outerRB.y);
            int cropW = Mathf.RoundToInt(cropRB.x - cropLT.x);
            int cropH = Mathf.RoundToInt(cropLT.y - cropRB.y);

            // 範囲外アクセス防止
            cropLBX = Mathf.Clamp(cropLBX, 0, scaledTexture.width - 1);
            cropLBY = Mathf.Clamp(cropLBY, 0, scaledTexture.height - 1);
            cropW = Mathf.Clamp(cropW, 1, scaledTexture.width - cropLBX);
            cropH = Mathf.Clamp(cropH, 1, scaledTexture.height - cropLBY);

            Color[] croppedPixels = scaledTexture.GetPixels(cropLBX, cropLBY, cropW, cropH);

            // 貼り付け位置（フレーム内座標系→テクスチャ座標系）
            int pasteX = Mathf.RoundToInt(cropLT.x + frameWidth / 2f);
            int pasteY = Mathf.RoundToInt(cropRB.y + frameHeight / 2f);

            // フレーム外にはみ出さないよう調整
            cropW = Mathf.Min(cropW, frameWidth - pasteX);
            cropH = Mathf.Min(cropH, frameHeight - pasteY);

            if (cropW > 0 && cropH > 0)
            {
                newTexture.SetPixels(pasteX, pasteY, cropW, cropH, croppedPixels);
            }

            // 内部状態保存
            _cropPositionX = cropLBX + Mathf.RoundToInt(outerLT.x - imageLT.x);
            _cropPositionY = cropLBY + Mathf.RoundToInt(outerRB.y - imageRB.y);
            _cropWidth = cropW;
            _cropHeight = cropH;
            _pasteX = pasteX;
            _pasteY = pasteY;

            Destroy(outerFrameCroppedTexture);
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

        for (int j = 0; j <= meshHeight; j++)
        {
            for (int i = 0; i <= meshWidth; i++)
            {
                // クロップ範囲内か判定
                bool inCrop =
                    i >= _pasteX * meshScale && i <= (_pasteX + _cropWidth) * meshScale &&
                    j >= _pasteY * meshScale && j <= (_pasteY + _cropHeight) * meshScale;

                if (inCrop)
                {
                    int matrixColumn = Mathf.Clamp(
                        (int)((Mathf.Min(i - _pasteX * meshScale, meshWidth - 1) / meshScale + _cropPositionX) / lastDisplayedScale),
                        0, originalWidth - 1);
                    int matrixRow = Mathf.Clamp(
                        (int)((Mathf.Min(j - _pasteY * meshScale, meshHeight - 1) / meshScale + _cropPositionY) / lastDisplayedScale),
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
