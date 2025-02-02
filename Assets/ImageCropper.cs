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
    [SerializeField]
    private ImageSplitter imageSplitter;

    [SerializeField]
    private ImageManipulator imageManipulator;

    [SerializeField]
    private float lastDisplayedPositionX;
    private float lastDisplayedPositionY;
    private float lastDisplayedScale;

    private Texture2D leftOriginalTexture;
    private Texture2D scaledTexture;

    public Texture2D finalLeftImage;

    private int frameWidth;
    private int frameHeight;
    private int originalWidth;
    private int originalHeight;
    private float initialScale;

    private int meshMaxWidth;
    private int meshMaxHeight;

    [SerializeField]
    private int meshWidth;
    [SerializeField]
    private int meshHeight;

    private int _cropPositionX;
    private int _cropPositionY;
    private int _cropWidth;
    private int _cropHeight;
    private int _pasteX;
    private int _pasteY;

    private float[] _zValues;

    public float[] zValues => _zValues;

    [SerializeField]
    private Material materialL;

    [SerializeField]
    public ImageCroppedEvent OnImageCropped;

    private void Start()
    {
        if (imageManipulator != null)
        {
            lastDisplayedPositionX = imageManipulator.displayedPositionX;
            lastDisplayedPositionY = imageManipulator.displayedPositionY;
            lastDisplayedScale = imageManipulator.displayedScale;
        }
    }

    public void OnImageCreatedHandler(GameObject imageObject, Sprite sprite)
    {
        if (imageObject == null || sprite == null)
        {
            return;
        }

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
        if (imageManipulator == null)
            return;

        if (HasDisplayParametersChanged())
        {
            UpdateDisplayParameters();
            StartCoroutine(ProcessImages());
            OnImageCropped?.Invoke(meshWidth, meshHeight, false);
        }
    }

    private bool HasDisplayParametersChanged()
    {
        return lastDisplayedPositionX != imageManipulator.displayedPositionX ||
               lastDisplayedPositionY != imageManipulator.displayedPositionY ||
               lastDisplayedScale != imageManipulator.displayedScale;
    }

    private void UpdateDisplayParameters()
    {
        lastDisplayedPositionX = imageManipulator.displayedPositionX;
        lastDisplayedPositionY = imageManipulator.displayedPositionY;
        lastDisplayedScale = imageManipulator.displayedScale;
        SetMeshSize();
    }

    private void SetFrameSize()
    {
        if (360 / originalWidth > 640 / originalHeight)
        {
            frameHeight = originalHeight;
            frameWidth = (int)(originalHeight * 9 / 16);
        }
        else
        {
            frameWidth = originalWidth;
            frameHeight = (int)(originalWidth * 16 / 9);
        }
    }

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

    private Texture2D CropAndCreateTexture(Texture2D originalTexture, int frameWidth, int frameHeight)
    {
        Vector2 frameLT = new Vector2(-frameWidth / 2, frameHeight / 2);
        Vector2 frameRB = new Vector2(frameWidth / 2, -frameHeight / 2);

        Vector2 outerFrameLT = new Vector2(-189 / initialScale, 336 / initialScale);
        Vector2 outerFrameRB = new Vector2(189 / initialScale, -336 / initialScale);

        Vector2 imageLT = new Vector2((-originalWidth / 2 * lastDisplayedScale + lastDisplayedPositionX / initialScale), (originalHeight / 2 * lastDisplayedScale + lastDisplayedPositionY / initialScale));
        Vector2 imageRB = new Vector2((originalWidth / 2 * lastDisplayedScale + lastDisplayedPositionX / initialScale), (-originalHeight / 2 * lastDisplayedScale + lastDisplayedPositionY / initialScale));

        Vector2 cropPositionFrameLT = new Vector2(0f, 0f);
        Vector2 cropPositionFrameRB = new Vector2(0f, 0f);
        Vector2 cropPositionLT = new Vector2(0f, 0f);
        Vector2 cropPositionRB = new Vector2(0f, 0f);

        Texture2D newTexture = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false);
        Color fillColor = new Color(0f, 0f, 0f, 0f);
        Color[] fillPixels = new Color[frameWidth * frameHeight];
        Array.Fill(fillPixels, fillColor);
        newTexture.SetPixels(fillPixels);

        if (imageRB.y < frameLT.y && imageRB.x > frameLT.x && imageLT.x < frameRB.x && imageLT.y > frameRB.y)
        {
            cropPositionFrameLT.x = Mathf.Max(imageLT.x, outerFrameLT.x);
            cropPositionFrameRB.x = Mathf.Min(imageRB.x, outerFrameRB.x);
            cropPositionFrameLT.y = Mathf.Min(imageLT.y, outerFrameLT.y);
            cropPositionFrameRB.y = Mathf.Max(imageRB.y, outerFrameRB.y);

            int cropPositionOuterFrameLBX = (int)((cropPositionFrameLT.x - imageLT.x) / lastDisplayedScale);
            int cropPositionOuterFrameLBY = (int)((cropPositionFrameRB.y - imageRB.y) / lastDisplayedScale);
            int cropOuterFrameWidth = (int)((cropPositionFrameRB.x - cropPositionFrameLT.x) / lastDisplayedScale);
            int cropOuterFrameHeight = (int)((cropPositionFrameLT.y - cropPositionFrameRB.y) / lastDisplayedScale);

            Vector2 LT = new Vector2(cropPositionFrameLT.x, cropPositionFrameLT.y);
            Vector2 RB = new Vector2(cropPositionFrameRB.x, cropPositionFrameRB.y);

            Color[] croppedOuterPixels = originalTexture.GetPixels(cropPositionOuterFrameLBX, cropPositionOuterFrameLBY, cropOuterFrameWidth, cropOuterFrameHeight);
            Texture2D outerFrameCroppedTexture = new Texture2D(cropOuterFrameWidth, cropOuterFrameHeight, TextureFormat.RGBA32, false);
            outerFrameCroppedTexture.SetPixels(0, 0, cropOuterFrameWidth, cropOuterFrameHeight, croppedOuterPixels);
            outerFrameCroppedTexture.Apply();

            scaledTexture = ScaleTexture(outerFrameCroppedTexture, lastDisplayedScale);

            cropPositionLT.x = Mathf.Max(LT.x, frameLT.x);
            cropPositionRB.x = Mathf.Min(RB.x, frameRB.x);
            cropPositionLT.y = Mathf.Min(LT.y, frameLT.y);
            cropPositionRB.y = Mathf.Max(RB.y, frameRB.y);

            int cropPositionLBX = (int)(cropPositionLT.x - LT.x);
            int cropPositionLBY = (int)(cropPositionRB.y - RB.y);
            int cropWidth = (int)(cropPositionRB.x - cropPositionLT.x);
            int cropHeight = (int)(cropPositionLT.y - cropPositionRB.y);

            if ((cropPositionLBX + cropWidth) > scaledTexture.width)
            {
                cropWidth = scaledTexture.width - cropPositionLBX;
            }
            if ((cropPositionLBY + cropHeight) > scaledTexture.height)
            {
                cropHeight = scaledTexture.height - cropPositionLBY;
            }

            int _x = Mathf.Min(Mathf.Max(cropPositionLBX, 0), scaledTexture.width);
            int _y = Mathf.Min(Mathf.Max(cropPositionLBY, 0), scaledTexture.height);
            int _w = Mathf.Min(Mathf.Max(cropWidth, 0), Mathf.Max(scaledTexture.width - cropPositionLBX, 0));
            int _h = Mathf.Min(Mathf.Max(cropHeight, 0), Mathf.Max(scaledTexture.height - cropPositionLBY, 0));
            Color[] croppedPixels = scaledTexture.GetPixels(_x, _y, _w, _h);

            int pasteX = (int)cropPositionLT.x + (int)(frameWidth / 2);
            int pasteY = (int)cropPositionRB.y + (int)(frameHeight / 2);

            cropWidth = Mathf.Min(cropWidth, frameWidth - pasteX);
            cropHeight = Mathf.Min(cropHeight, frameHeight - pasteY);

            newTexture.SetPixels(pasteX, pasteY, cropWidth, cropHeight, croppedPixels);

            _cropPositionX = (int)(cropPositionLBX + (LT.x - imageLT.x));
            _cropPositionY = (int)(cropPositionLBY + (RB.y - imageRB.y));
            _cropWidth = cropWidth;
            _cropHeight = cropHeight;
            _pasteX = pasteX;
            _pasteY = pasteY;

            Destroy(outerFrameCroppedTexture);
        }
        newTexture.Apply();
        return newTexture;
    }

    private void AssignTextureToMaterial(Texture2D texture, Material material)
    {
        if (material == null || texture == null)
        {
            return;
        }

        material.mainTexture = texture;
    }

    private static Texture2D ScaleTexture(Texture2D source, float scale)
    {
        if (scale <= 0)
        {
            return null;
        }

        int newWidth = Mathf.RoundToInt(source.width * scale);
        int newHeight = Mathf.RoundToInt(source.height * scale);

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

    private float[] CalculateZValues()
    {
        float[] zValues = new float[(meshWidth + 1) * (meshHeight + 1)];
        int[,] quotedRow = new int[meshHeight, meshWidth];
        int[,] quotedColumn = new int[meshHeight, meshWidth];
        float[,] pixelZMatrix = imageSplitter.PixelZMatrix;
        float zValueMax = imageSplitter.PixelZMax;

        Array.Fill(zValues, zValueMax);

        float meshScale = (float)meshWidth / (float)frameWidth;

        for (int j = 0; j < meshHeight + 1; j++)
        {
            for (int i = 0; i < meshWidth + 1; i++)
            {
                int matrixRow = 0;
                int matrixColumn = 0;
                if ((i >= _pasteX * meshScale) && (i <= (_pasteX + _cropWidth) * meshScale) && (j >= _pasteY * meshScale) && (j <= (_pasteY + _cropHeight) * meshScale))
                {
                    matrixColumn = Mathf.Min(Mathf.Max((int)(((Mathf.Min(i - _pasteX * meshScale, meshWidth - 1) / meshScale) + _cropPositionX) / lastDisplayedScale), 0), originalWidth - 1);
                    matrixRow = Mathf.Min(Mathf.Max((int)(((Mathf.Min(j - _pasteY * meshScale, meshHeight - 1) / meshScale) + _cropPositionY) / lastDisplayedScale), 0), originalHeight - 1);

                    zValues[Mathf.Min((meshWidth + 1) * j + i, (meshWidth + 1) * (meshHeight + 1) - 1)] = pixelZMatrix[matrixRow, matrixColumn];
                }
                if (i < meshWidth && j < meshHeight) { quotedRow[j, i] = matrixRow; quotedColumn[j, i] = matrixColumn; }
            }
        }

        for (int i = 0; i < meshWidth + 1; i++)
        {
            zValues[(meshWidth + 1) * meshHeight + i] = zValues[(meshWidth + 1) * (meshHeight - 1) + i];
        }

        for (int j = 0; j < meshHeight + 1; j++)
        {
            zValues[(meshWidth + 1) * j + meshWidth] = zValues[(meshWidth + 1) * j + meshWidth - 1];
        }

        return zValues;
    }
}
