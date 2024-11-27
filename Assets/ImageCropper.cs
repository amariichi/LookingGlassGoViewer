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
    // Set reference to ImageSplitter script from inspector, ImageSplitterスクリプトへの参照をインスペクターから設定
    [SerializeField]
    private ImageSplitter imageSplitter;

    // Set reference to ImageManipulator script from inspector, ImageManipulatorスクリプトへの参照をインスペクターから設定
    [SerializeField]
    private ImageManipulator imageManipulator;

    // Keep parameters of last displayed, 前回の表示パラメータを保持
    [SerializeField]
    private float lastDisplayedPositionX;
    private float lastDisplayedPositionY;
    private float lastDisplayedScale;

    // Original image texture on the left and scaled texture, 左の元画像テクスチャ及び拡大縮小されたテクスチャ
    private Texture2D leftOriginalTexture;
    private Texture2D scaledTexture;

    // Variable to store final cropped image data, 最終的な画像データを格納する変数
    public Texture2D finalLeftImage;

    // original frame size and initial scale, 本来のフレームのサイズ
    private int frameWidth;
    private int frameHeight;
    private int originalWidth;
    private int originalHeight;
    private float initialScale;

    //Max mesh size, メッシュの最大サイズ
    private int meshMaxWidth;
    private int meshMaxHeight;

    //Mesh size, メッシュのサイズ
    [SerializeField]
    private int meshWidth;
    [SerializeField]
    private int meshHeight;

    //For temporarily passing information about texture cropping, テクスチャのクロップに関する情報の一時受け渡し用pasteX, pasteY
    private int _cropPositionX; //原点は左下（中央からシフト済み）
    private int _cropPositionY; //原点は左下（中央からシフト済み）
    private int _cropWidth;
    private int _cropHeight;
    private int _pasteX;
    private int _pasteY;

    private float[] _zValues;

    /// <summary>
    /// 他のスクリプトから参照可能なzValuesプロパティ（読み取り専用）。
    /// meshの各頂点のZ値を格納します。
    /// </summary>
    public float[] zValues
    {
        get { return _zValues; }
    }

    // Set reference to Material from Inspector, Materialへの参照をインスペクターから設定
        [SerializeField]
    private Material materialL;

    /// <summary>
    /// Textureが分割作成された際に発生するUnityEvent。
    /// 引数として新しく作成された１つのTexture、frameWidth, frameHeightを渡します。
    /// </summary>
    [SerializeField]
    public ImageCroppedEvent OnImageCropped;

    private void Start()
    {
        if (imageManipulator != null)
        {
            // Set initial value, 初期値を設定
            lastDisplayedPositionX = imageManipulator.displayedPositionX;
            lastDisplayedPositionY = imageManipulator.displayedPositionY;
            lastDisplayedScale = imageManipulator.displayedScale;
        }
        else
        {
            //Debug.LogError("ImageManipulatorが割り当てられていません。インスペクターで設定してください。");
        }
    }

    /// <summary>
    /// イベントハンドラー：新しい画像が作成されたときに呼び出される
    /// </summary>
    /// <param name="imageObject">画像のGameObject</param>
    /// <param name="sprite">生成されたSprite</param>
    public void OnImageCreatedHandler(GameObject imageObject, Sprite sprite)
    {
        if (imageObject == null || sprite == null)
        {
            //Debug.LogWarning("imageObject または sprite が null です。処理を中止します。");
            return;
        }

        // Set left image texture, 左側の元画像テクスチャを設定
        leftOriginalTexture = sprite.texture;

        //Set the frame to match the original size of the image, 画像の本来のサイズにあわせてフレームを設定
        originalWidth = imageSplitter.OriginalWidth;
        originalHeight = imageSplitter.OriginalHeight;
        initialScale = imageSplitter.InitialScale;
        if (360 / originalWidth > 640 / originalHeight)
        {
            frameHeight = originalHeight;
            frameWidth = (int)(originalHeight * 9 / 16);
        
        } else
        {
            frameWidth = originalWidth;
            frameHeight = (int)(originalWidth * 16 / 9);
        }

        // Calculate the size of the mesh to create, 作成するメッシュのサイズを計算
        meshMaxWidth = imageSplitter.meshX;
        meshMaxHeight = imageSplitter.meshY;
        meshWidth = Mathf.Min(frameWidth, meshMaxWidth);
        meshHeight = Mathf.Min(frameHeight, meshMaxHeight);

        // Execute the first crop process, 初回のクロップ処理を実行
        StartCoroutine(ProcessImages());

        // Invoke events to notify other objects, イベントを発火して他のオブジェクトに通知
        OnImageCropped?.Invoke(meshWidth, meshHeight, true);
    }

    private void Update()
    {
        if (imageManipulator == null)
            return;

        // Check if display parameters have changed, 表示パラメータに変更があったか確認
        if (lastDisplayedPositionX != imageManipulator.displayedPositionX ||
            lastDisplayedPositionY != imageManipulator.displayedPositionY ||
            lastDisplayedScale != imageManipulator.displayedScale)
        {
            // update parameters, パラメータを更新
            lastDisplayedPositionX = imageManipulator.displayedPositionX;
            lastDisplayedPositionY = imageManipulator.displayedPositionY;
            lastDisplayedScale = imageManipulator.displayedScale;

            // Retry cropping, クロップ処理を再実行
            StartCoroutine(ProcessImages());

            // Invoke events to notify other objects, イベントを発火して他のオブジェクトに通知
            meshMaxWidth = imageSplitter.meshX;
            meshMaxHeight = imageSplitter.meshY;
            meshWidth = Mathf.Min(frameWidth, meshMaxWidth);
            meshHeight = Mathf.Min(frameHeight, meshMaxHeight);
            OnImageCropped?.Invoke(meshWidth, meshHeight, false);
        }
    }

    /// <summary>
    /// Coroutine for cropping and compositing images, 画像のクロップと合成を行うコルーチン
    /// </summary>
    /// <returns></returns>
    private IEnumerator ProcessImages()
    {
        if (leftOriginalTexture == null)
        {
            yield break;
        }

        // 左側の画像をクロップ
        finalLeftImage = CropAndCreateTexture(leftOriginalTexture, frameWidth, frameHeight);

        // Materialにテクスチャを割り当てる
        Destroy(materialL.mainTexture);
        AssignTextureToMaterial(finalLeftImage, materialL);
        
        //メッシュの各頂点のZ値を計算してプロパティに格納
        _zValues = calculateZValues();

        yield return null;
    }

    /// <summary>
    /// スプライトをクロップし、新しいテクスチャを作成するメソッド
    /// </summary>
    /// <param name="originalTexture">元のテクスチャ</param>
    /// <param name="frameWidth">フレームの幅（ピクセル単位）</param>
    /// <param name="frameHeight">フレームの高さ（ピクセル単位）</param>
    /// <param name="isLeft">左側の画像かどうか</param>
    /// <returns>新しく作成されたテクスチャ</returns>
    private Texture2D CropAndCreateTexture(Texture2D originalTexture, int frameWidth, int frameHeight)
    {
        // calculate crop position, クロップする場所を計算する
        Vector2 frameLT = new Vector2(-frameWidth / 2, frameHeight / 2);
        Vector2 frameRB = new Vector2(frameWidth / 2, -frameHeight / 2);
        //Debug.Log("frameLT: " + frameLT);
        //Debug.Log("frameRB: " + frameRB);

        // Calculate where to crop in frame (5% increase from image's original resolution equivalent)
        // フレームでクロップする場所を計算する（画像のオリジナル解像度相当から5%増し）
        Vector2 outerFrameLT = new Vector2(-189 / initialScale, 336 / initialScale);
        Vector2 outerFrameRB = new Vector2(189 / initialScale, -336 / initialScale);

        Vector2 imageLT = new Vector2((-originalWidth / 2 * lastDisplayedScale + lastDisplayedPositionX / initialScale), (originalHeight / 2 * lastDisplayedScale + lastDisplayedPositionY / initialScale));
        Vector2 imageRB = new Vector2((originalWidth / 2 * lastDisplayedScale + lastDisplayedPositionX / initialScale) , (-originalHeight / 2 * lastDisplayedScale + lastDisplayedPositionY / initialScale));
        //Debug.Log("imageLT: " + imageLT);
        //Debug.Log("imgaeRB: " + imageRB);

        Vector2 cropPositionFrameLT = new Vector2(0f, 0f);
        Vector2 cropPositionFrameRB = new Vector2(0f, 0f);
        Vector2 cropPositionLT = new Vector2(0f, 0f);
        Vector2 cropPositionRB = new Vector2(0f, 0f);

        // Create a new texture, 新しいテクスチャを作成
        Texture2D newTexture = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false);

        // fill everything black, 全体を真っ黒に塗りつぶし
        Color fillColor = new Color(0f, 0f, 0f, 0f);

        // Initialize all pixels with fill color, 全ピクセルを塗りつぶしカラーで初期化
        Color[] fillPixels = new Color[frameWidth * frameHeight];
        Array.Fill(fillPixels, fillColor);
        newTexture.SetPixels(fillPixels);

        if (imageRB.y < frameLT.y && imageRB.x > frameLT.x && imageLT.x < frameRB.x && imageLT.y > frameRB.y)
        {
            cropPositionFrameLT.x = Mathf.Max(imageLT.x, outerFrameLT.x);
            cropPositionFrameRB.x = Mathf.Min(imageRB.x, outerFrameRB.x);
            cropPositionFrameLT.y = Mathf.Min(imageLT.y, outerFrameLT.y);
            cropPositionFrameRB.y = Mathf.Max(imageRB.y, outerFrameRB.y);
            int cropPositionOuterFrameLBX = (int)((cropPositionFrameLT.x - imageLT.x) / lastDisplayedScale); //オリジナルサイズでの切り出し開始位置X
            int cropPositionOuterFrameLBY = (int)((cropPositionFrameRB.y - imageRB.y) / lastDisplayedScale); //オリジナルサイズでの切り出し開始位置Y
            int cropOuterFrameWidth = (int)((cropPositionFrameRB.x - cropPositionFrameLT.x) / lastDisplayedScale); //オリジナルサイズでの切り出し幅
            int cropOuterFrameHeight = (int)((cropPositionFrameLT.y - cropPositionFrameRB.y) / lastDisplayedScale); //オリジナルサイズでの切り出し高さ

            Vector2 LT = new Vector2(cropPositionFrameLT.x, cropPositionFrameLT.y);
            Vector2 RB = new Vector2(cropPositionFrameRB.x, cropPositionFrameRB.y);


            // Get cropped pixel data, クロップされたピクセルデータを取得
            Color[] croppedOuterPixels = originalTexture.GetPixels(cropPositionOuterFrameLBX, cropPositionOuterFrameLBY, cropOuterFrameWidth, cropOuterFrameHeight);

            // Create a new texture cropped, クロップされた新しいテクスチャを作成
            Texture2D outerFrameCroppedTexture = new Texture2D(cropOuterFrameWidth, cropOuterFrameHeight, TextureFormat.RGBA32, false);

            // Paste cropped pixels into new texture, クロップされたピクセルを新しいテクスチャに貼り付け
            outerFrameCroppedTexture.SetPixels(0, 0, cropOuterFrameWidth, cropOuterFrameHeight, croppedOuterPixels);
            outerFrameCroppedTexture.Apply();

            // Scaling texture, テクスチャをスケーリング
            scaledTexture = ScaleTexture(outerFrameCroppedTexture, lastDisplayedScale);
            //Debug.Log("outerFrameScaledTexture.width: " + outerFrameCroppedTexture.width + ", height: " + outerFrameCroppedTexture.height);

            // Calculate position information and size for cropping in frame, フレームでクロップする位置情報やサイズを計算
            cropPositionLT.x = Mathf.Max(LT.x, frameLT.x);
            cropPositionRB.x = Mathf.Min(RB.x, frameRB.x);
            cropPositionLT.y = Mathf.Min(LT.y, frameLT.y);
            cropPositionRB.y = Mathf.Max(RB.y, frameRB.y);
            int cropPositionLBX = (int)(cropPositionLT.x - LT.x);
            int cropPositionLBY = (int)(cropPositionRB.y - RB.y);
            int cropWidth = (int)(cropPositionRB.x - cropPositionLT.x);
            int cropHeight = (int)(cropPositionLT.y - cropPositionRB.y);

            // Correcting errors due to rounding, 丸めによる誤差の補正
            if ((cropPositionLBX + cropWidth) > scaledTexture.width)
            {
                cropWidth = scaledTexture.width - cropPositionLBX;
            }
            if ((cropPositionLBY + cropHeight) > scaledTexture.height)
            {
                cropHeight = scaledTexture.height - cropPositionLBY;
            }

            // Get cropped pixel data, クロップされたピクセルデータを取得
            int _x = Mathf.Min(Mathf.Max(cropPositionLBX, 0), scaledTexture.width);
            int _y = Mathf.Min(Mathf.Max(cropPositionLBY, 0), scaledTexture.height);
            int _w = Mathf.Min(Mathf.Max(cropWidth, 0), Mathf.Max(scaledTexture.width - cropPositionLBX,0));
            int _h = Mathf.Min(Mathf.Max(cropHeight, 0), Mathf.Max(scaledTexture.height - cropPositionLBY,0));
            Color[] croppedPixels = scaledTexture.GetPixels(_x, _y, _w, _h);

            // Calculate where to paste cropped pixels into new texture, クロップされたピクセルを新しいテクスチャに貼り付ける位置を計算
            int pasteX = (int)cropPositionLT.x + (int)(frameWidth / 2);
            int pasteY = (int)cropPositionRB.y + (int)(frameHeight / 2);

            // Adjusted pasting to fit within new texture range, 貼り付けが新しいテクスチャの範囲内に収まるように調整
            cropWidth = Mathf.Min(cropWidth, frameWidth - pasteX);
            cropHeight = Mathf.Min(cropHeight, frameHeight - pasteY);

            // Paste cropped pixels into new texture, クロップされたピクセルを新しいテクスチャに貼り付け
            newTexture.SetPixels(pasteX, pasteY, cropWidth, cropHeight, croppedPixels);

            // Passing variables for vertex calculations, 頂点計算用に変数受け渡し
            _cropPositionX = (int)(cropPositionLBX + (LT.x - imageLT.x));
            _cropPositionY = (int)(cropPositionLBY + (RB.y - imageRB.y));
            _cropWidth = cropWidth;
            _cropHeight = cropHeight;
            _pasteX = pasteX;
            _pasteY = pasteY;

            Destroy(scaledTexture);
            Destroy(outerFrameCroppedTexture);
        }
        newTexture.Apply();
        return newTexture;
    }

    /// <summary>
    /// テクスチャを指定されたマテリアルに割り当てるメソッド
    /// </summary>
    /// <param name="texture">割り当てるテクスチャ</param>
    /// <param name="material">対象のマテリアル</param>
    private void AssignTextureToMaterial(Texture2D texture, Material material)
    {
        if (material == null)
        {
            //Debug.LogWarning("Materialが割り当てられていません。インスペクターで設定してください。");
            return;
        }

        if (texture == null)
        {
            //Debug.LogWarning("割り当てるTextureがnullです。");
            return;
        }

        // Set texture to material's main texture, マテリアルのメインテクスチャに設定
        material.mainTexture = texture;
    }

    /// <summary>
    /// 指定倍率でTexture2Dをスケーリングし、新しいTexture2Dを作成します。
    /// </summary>
    /// <param name="source">元のTexture2D</param>
    /// <param name="scale">スケール倍率（例：2.0f は2倍、0.5f は半分）</param>
    /// <returns>スケーリングされた新しいTexture2D</returns>
    private static Texture2D ScaleTexture(Texture2D source, float scale)
    {
        if (scale <= 0)
        {
            //Debug.LogError("スケール倍率は0より大きくなければなりません。");
            return null;
        }

        int newWidth = Mathf.RoundToInt(source.width * scale);
        int newHeight = Mathf.RoundToInt(source.height * scale);

        // Create new RenderTexture, 新しいRenderTextureを作成
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Point; // フィルターモードを設定（必要に応じて変更可能）

        // Blit original texture to RenderTexture, 元のテクスチャをRenderTextureにBlit
        Graphics.Blit(source, rt);

        // Save current RenderTexture, 現在のRenderTextureを保存しておく
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        // Create a new Texture2D in RGBA32 format and load pixel data, 新しいTexture2DをRGBA32形式で作成してピクセルデータを読み込む
        Texture2D scaled = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        scaled.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        scaled.Apply();

        // Restore original RenderTexture, 元のRenderTextureを復元
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return scaled;
    }

    private float[] calculateZValues()
    {
        float[] zValues = new float[(meshWidth + 1) * (meshHeight + 1)];
        int[,] quotedRow = new int[meshHeight, meshWidth];
        int[,] quotedColumn = new int[meshHeight, meshWidth];
        float[,] pixelZMatrix = imageSplitter.PixelZMatrix;
        float zValueMax;

        // Fill with Z of the farthest pixel, 一番奥にあるピクセルのZでFillする
        zValueMax = imageSplitter.PixelZMax;
        Array.Fill(zValues, zValueMax);

        // Calculate image size and mesh ratio, 画像サイズとメッシュの比率を計算
        float meshScale = (float)meshWidth / (float)frameWidth;

        //Assign Z to the mesh from the Z data extracted from the right image, 右画像から取り出したZデータからメッシュにZを割り当てる
        for (int j = 0; j < meshHeight + 1; j++)
        {
            for (int i = 0; i < meshWidth + 1; i++)
            {
                int matrixRow = 0;
                int matrixColumn = 0;
                if ((i >= _pasteX * (float)meshScale) && (i <= (_pasteX + _cropWidth) * (float)meshScale) && (j >= _pasteY * (float)meshScale) && (j <= (_pasteY + _cropHeight) * (float)meshScale))
                {
                    matrixColumn = Mathf.Min(Mathf.Max((int)(((Mathf.Min(i - _pasteX * (float)meshScale, meshWidth - 1) / meshScale) + _cropPositionX) / lastDisplayedScale),0),originalWidth - 1); //エラー対策で最大最小を無理やり設定してみた。
                    matrixRow = Mathf.Min(Mathf.Max((int)(((Mathf.Min(j - _pasteY * (float)meshScale, meshHeight - 1) / meshScale) + _cropPositionY) / lastDisplayedScale),0),originalHeight - 1);

                    zValues[Mathf.Min((meshWidth + 1) * j + i,(meshWidth + 1)*(meshHeight + 1) - 1)] = pixelZMatrix[matrixRow, matrixColumn];
                }
                if (i < meshWidth && j < meshHeight) { quotedRow[j, i] = matrixRow; quotedColumn[j, i] = matrixColumn; }
            }
        }

        // The top column copies the value of the column directly below it, 最上列は直下の列の値をコピー
        for (int i = 0; i < meshWidth + 1; i++)
        {
            zValues[(meshWidth + 1) * meshHeight + i] = zValues[(meshWidth + 1) * (meshHeight - 1) + i];
        }

        // The rightmost column copies the value on the left, 最右列は左隣の値をコピー
        for (int j = 0; j < meshHeight + 1; j++)
        {
            zValues[(meshWidth + 1) * j + meshWidth] = zValues[(meshWidth + 1) * j + meshWidth - 1];
        }

        /*/ Filtering (test) No need to use!, 誤差の修正（テスト）必要なし！
        System.Random rnd = new System.Random();
        for (int j = 1; j < meshHeight - 1; j++)
        {
            for (int i = 1; i < meshWidth - 1; i++)
            {
                if ((i >= _pasteX * (float)meshScale) && (i <= (_pasteX + _cropWidth) * (float)meshScale) && (j >= _pasteY * (float)meshScale) && (j <= (_pasteY + _cropHeight) * (float)meshScale))
                {
                    // Reduce grid pattern (horizontal), 格子状の誤差をごまかす（横）
                    if (quotedColumn[j,i] == quotedColumn[j,i - 1])
                    {
                        float random = (float)rnd.NextDouble() * 1.0f + 0.0f / 2;
                        //random = 1f;
                        zValues[Mathf.Min((meshWidth + 1) * j + i, (meshWidth + 1) * (meshHeight + 1) - 1)] = random * pixelZMatrix[quotedRow[j, i], quotedColumn[j, i]] + (1f - random) * pixelZMatrix[Mathf.Max(Mathf.Min(originalHeight - 1, quotedRow[j, i] + 1), 0), quotedColumn[j, i]];
                    }
                    // Reduce grid pattern (vertical), 格子状の誤差をごまかす（縦）
                    if (quotedRow[j, i] == quotedRow[j - 1, i])
                    {
                        float random = (float)rnd.NextDouble() * 1.0f + 0.0f / 2;
                        //random = 1f;
                        zValues[Mathf.Min((meshWidth + 1) * j + i, (meshWidth + 1) * (meshHeight + 1) - 1)] = random * pixelZMatrix[quotedRow[j, i], quotedColumn[j, i]] + (1f - random) * pixelZMatrix[quotedRow[j, i], Mathf.Max(Mathf.Min(originalWidth - 1, quotedColumn[j, i] + 1), 0)];
                    }
                }
            }
        }
        */
        return zValues;
    }
}
