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
    // Set reference to ImageSplitter script from inspector, ImageSplitter�X�N���v�g�ւ̎Q�Ƃ��C���X�y�N�^�[����ݒ�
    [SerializeField]
    private ImageSplitter imageSplitter;

    // Set reference to ImageManipulator script from inspector, ImageManipulator�X�N���v�g�ւ̎Q�Ƃ��C���X�y�N�^�[����ݒ�
    [SerializeField]
    private ImageManipulator imageManipulator;

    // Keep parameters of last displayed, �O��̕\���p�����[�^��ێ�
    [SerializeField]
    private float lastDisplayedPositionX;
    private float lastDisplayedPositionY;
    private float lastDisplayedScale;

    // Original image texture on the left and scaled texture, ���̌��摜�e�N�X�`���y�ъg��k�����ꂽ�e�N�X�`��
    private Texture2D leftOriginalTexture;
    private Texture2D scaledTexture;

    // Variable to store final cropped image data, �ŏI�I�ȉ摜�f�[�^���i�[����ϐ�
    public Texture2D finalLeftImage;

    // original frame size and initial scale, �{���̃t���[���̃T�C�Y
    private int frameWidth;
    private int frameHeight;
    private int originalWidth;
    private int originalHeight;
    private float initialScale;

    //Max mesh size, ���b�V���̍ő�T�C�Y
    private int meshMaxWidth;
    private int meshMaxHeight;

    //Mesh size, ���b�V���̃T�C�Y
    [SerializeField]
    private int meshWidth;
    [SerializeField]
    private int meshHeight;

    //For temporarily passing information about texture cropping, �e�N�X�`���̃N���b�v�Ɋւ�����̈ꎞ�󂯓n���ppasteX, pasteY
    private int _cropPositionX; //���_�͍����i��������V�t�g�ς݁j
    private int _cropPositionY; //���_�͍����i��������V�t�g�ς݁j
    private int _cropWidth;
    private int _cropHeight;
    private int _pasteX;
    private int _pasteY;

    private float[] _zValues;

    /// <summary>
    /// ���̃X�N���v�g����Q�Ɖ\��zValues�v���p�e�B�i�ǂݎ���p�j�B
    /// mesh�̊e���_��Z�l���i�[���܂��B
    /// </summary>
    public float[] zValues
    {
        get { return _zValues; }
    }

    // Set reference to Material from Inspector, Material�ւ̎Q�Ƃ��C���X�y�N�^�[����ݒ�
        [SerializeField]
    private Material materialL;

    /// <summary>
    /// Texture�������쐬���ꂽ�ۂɔ�������UnityEvent�B
    /// �����Ƃ��ĐV�����쐬���ꂽ�P��Texture�AframeWidth, frameHeight��n���܂��B
    /// </summary>
    [SerializeField]
    public ImageCroppedEvent OnImageCropped;

    private void Start()
    {
        if (imageManipulator != null)
        {
            // Set initial value, �����l��ݒ�
            lastDisplayedPositionX = imageManipulator.displayedPositionX;
            lastDisplayedPositionY = imageManipulator.displayedPositionY;
            lastDisplayedScale = imageManipulator.displayedScale;
        }
        else
        {
            //Debug.LogError("ImageManipulator�����蓖�Ă��Ă��܂���B�C���X�y�N�^�[�Őݒ肵�Ă��������B");
        }
    }

    /// <summary>
    /// �C�x���g�n���h���[�F�V�����摜���쐬���ꂽ�Ƃ��ɌĂяo�����
    /// </summary>
    /// <param name="imageObject">�摜��GameObject</param>
    /// <param name="sprite">�������ꂽSprite</param>
    public void OnImageCreatedHandler(GameObject imageObject, Sprite sprite)
    {
        if (imageObject == null || sprite == null)
        {
            //Debug.LogWarning("imageObject �܂��� sprite �� null �ł��B�����𒆎~���܂��B");
            return;
        }

        // Set left image texture, �����̌��摜�e�N�X�`����ݒ�
        leftOriginalTexture = sprite.texture;

        //Set the frame to match the original size of the image, �摜�̖{���̃T�C�Y�ɂ��킹�ăt���[����ݒ�
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

        // Calculate the size of the mesh to create, �쐬���郁�b�V���̃T�C�Y���v�Z
        meshMaxWidth = imageSplitter.meshX;
        meshMaxHeight = imageSplitter.meshY;
        meshWidth = Mathf.Min(frameWidth, meshMaxWidth);
        meshHeight = Mathf.Min(frameHeight, meshMaxHeight);

        // Execute the first crop process, ����̃N���b�v���������s
        StartCoroutine(ProcessImages());

        // Invoke events to notify other objects, �C�x���g�𔭉΂��đ��̃I�u�W�F�N�g�ɒʒm
        OnImageCropped?.Invoke(meshWidth, meshHeight, true);
    }

    private void Update()
    {
        if (imageManipulator == null)
            return;

        // Check if display parameters have changed, �\���p�����[�^�ɕύX�����������m�F
        if (lastDisplayedPositionX != imageManipulator.displayedPositionX ||
            lastDisplayedPositionY != imageManipulator.displayedPositionY ||
            lastDisplayedScale != imageManipulator.displayedScale)
        {
            // update parameters, �p�����[�^���X�V
            lastDisplayedPositionX = imageManipulator.displayedPositionX;
            lastDisplayedPositionY = imageManipulator.displayedPositionY;
            lastDisplayedScale = imageManipulator.displayedScale;

            // Retry cropping, �N���b�v�������Ď��s
            StartCoroutine(ProcessImages());

            // Invoke events to notify other objects, �C�x���g�𔭉΂��đ��̃I�u�W�F�N�g�ɒʒm
            meshMaxWidth = imageSplitter.meshX;
            meshMaxHeight = imageSplitter.meshY;
            meshWidth = Mathf.Min(frameWidth, meshMaxWidth);
            meshHeight = Mathf.Min(frameHeight, meshMaxHeight);
            OnImageCropped?.Invoke(meshWidth, meshHeight, false);
        }
    }

    /// <summary>
    /// Coroutine for cropping and compositing images, �摜�̃N���b�v�ƍ������s���R���[�`��
    /// </summary>
    /// <returns></returns>
    private IEnumerator ProcessImages()
    {
        if (leftOriginalTexture == null)
        {
            yield break;
        }

        // �����̉摜���N���b�v
        finalLeftImage = CropAndCreateTexture(leftOriginalTexture, frameWidth, frameHeight);

        // Material�Ƀe�N�X�`�������蓖�Ă�
        Destroy(materialL.mainTexture);
        AssignTextureToMaterial(finalLeftImage, materialL);
        
        //���b�V���̊e���_��Z�l���v�Z���ăv���p�e�B�Ɋi�[
        _zValues = calculateZValues();

        yield return null;
    }

    /// <summary>
    /// �X�v���C�g���N���b�v���A�V�����e�N�X�`�����쐬���郁�\�b�h
    /// </summary>
    /// <param name="originalTexture">���̃e�N�X�`��</param>
    /// <param name="frameWidth">�t���[���̕��i�s�N�Z���P�ʁj</param>
    /// <param name="frameHeight">�t���[���̍����i�s�N�Z���P�ʁj</param>
    /// <param name="isLeft">�����̉摜���ǂ���</param>
    /// <returns>�V�����쐬���ꂽ�e�N�X�`��</returns>
    private Texture2D CropAndCreateTexture(Texture2D originalTexture, int frameWidth, int frameHeight)
    {
        // calculate crop position, �N���b�v����ꏊ���v�Z����
        Vector2 frameLT = new Vector2(-frameWidth / 2, frameHeight / 2);
        Vector2 frameRB = new Vector2(frameWidth / 2, -frameHeight / 2);
        //Debug.Log("frameLT: " + frameLT);
        //Debug.Log("frameRB: " + frameRB);

        // Calculate where to crop in frame (5% increase from image's original resolution equivalent)
        // �t���[���ŃN���b�v����ꏊ���v�Z����i�摜�̃I���W�i���𑜓x��������5%�����j
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

        // Create a new texture, �V�����e�N�X�`�����쐬
        Texture2D newTexture = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false);

        // fill everything black, �S�̂�^�����ɓh��Ԃ�
        Color fillColor = new Color(0f, 0f, 0f, 0f);

        // Initialize all pixels with fill color, �S�s�N�Z����h��Ԃ��J���[�ŏ�����
        Color[] fillPixels = new Color[frameWidth * frameHeight];
        Array.Fill(fillPixels, fillColor);
        newTexture.SetPixels(fillPixels);

        if (imageRB.y < frameLT.y && imageRB.x > frameLT.x && imageLT.x < frameRB.x && imageLT.y > frameRB.y)
        {
            cropPositionFrameLT.x = Mathf.Max(imageLT.x, outerFrameLT.x);
            cropPositionFrameRB.x = Mathf.Min(imageRB.x, outerFrameRB.x);
            cropPositionFrameLT.y = Mathf.Min(imageLT.y, outerFrameLT.y);
            cropPositionFrameRB.y = Mathf.Max(imageRB.y, outerFrameRB.y);
            int cropPositionOuterFrameLBX = (int)((cropPositionFrameLT.x - imageLT.x) / lastDisplayedScale); //�I���W�i���T�C�Y�ł̐؂�o���J�n�ʒuX
            int cropPositionOuterFrameLBY = (int)((cropPositionFrameRB.y - imageRB.y) / lastDisplayedScale); //�I���W�i���T�C�Y�ł̐؂�o���J�n�ʒuY
            int cropOuterFrameWidth = (int)((cropPositionFrameRB.x - cropPositionFrameLT.x) / lastDisplayedScale); //�I���W�i���T�C�Y�ł̐؂�o����
            int cropOuterFrameHeight = (int)((cropPositionFrameLT.y - cropPositionFrameRB.y) / lastDisplayedScale); //�I���W�i���T�C�Y�ł̐؂�o������

            Vector2 LT = new Vector2(cropPositionFrameLT.x, cropPositionFrameLT.y);
            Vector2 RB = new Vector2(cropPositionFrameRB.x, cropPositionFrameRB.y);


            // Get cropped pixel data, �N���b�v���ꂽ�s�N�Z���f�[�^���擾
            Color[] croppedOuterPixels = originalTexture.GetPixels(cropPositionOuterFrameLBX, cropPositionOuterFrameLBY, cropOuterFrameWidth, cropOuterFrameHeight);

            // Create a new texture cropped, �N���b�v���ꂽ�V�����e�N�X�`�����쐬
            Texture2D outerFrameCroppedTexture = new Texture2D(cropOuterFrameWidth, cropOuterFrameHeight, TextureFormat.RGBA32, false);

            // Paste cropped pixels into new texture, �N���b�v���ꂽ�s�N�Z����V�����e�N�X�`���ɓ\��t��
            outerFrameCroppedTexture.SetPixels(0, 0, cropOuterFrameWidth, cropOuterFrameHeight, croppedOuterPixels);
            outerFrameCroppedTexture.Apply();

            // Scaling texture, �e�N�X�`�����X�P�[�����O
            scaledTexture = ScaleTexture(outerFrameCroppedTexture, lastDisplayedScale);
            //Debug.Log("outerFrameScaledTexture.width: " + outerFrameCroppedTexture.width + ", height: " + outerFrameCroppedTexture.height);

            // Calculate position information and size for cropping in frame, �t���[���ŃN���b�v����ʒu����T�C�Y���v�Z
            cropPositionLT.x = Mathf.Max(LT.x, frameLT.x);
            cropPositionRB.x = Mathf.Min(RB.x, frameRB.x);
            cropPositionLT.y = Mathf.Min(LT.y, frameLT.y);
            cropPositionRB.y = Mathf.Max(RB.y, frameRB.y);
            int cropPositionLBX = (int)(cropPositionLT.x - LT.x);
            int cropPositionLBY = (int)(cropPositionRB.y - RB.y);
            int cropWidth = (int)(cropPositionRB.x - cropPositionLT.x);
            int cropHeight = (int)(cropPositionLT.y - cropPositionRB.y);

            // Correcting errors due to rounding, �ۂ߂ɂ��덷�̕␳
            if ((cropPositionLBX + cropWidth) > scaledTexture.width)
            {
                cropWidth = scaledTexture.width - cropPositionLBX;
            }
            if ((cropPositionLBY + cropHeight) > scaledTexture.height)
            {
                cropHeight = scaledTexture.height - cropPositionLBY;
            }

            // Get cropped pixel data, �N���b�v���ꂽ�s�N�Z���f�[�^���擾
            int _x = Mathf.Min(Mathf.Max(cropPositionLBX, 0), scaledTexture.width);
            int _y = Mathf.Min(Mathf.Max(cropPositionLBY, 0), scaledTexture.height);
            int _w = Mathf.Min(Mathf.Max(cropWidth, 0), Mathf.Max(scaledTexture.width - cropPositionLBX,0));
            int _h = Mathf.Min(Mathf.Max(cropHeight, 0), Mathf.Max(scaledTexture.height - cropPositionLBY,0));
            Color[] croppedPixels = scaledTexture.GetPixels(_x, _y, _w, _h);

            // Calculate where to paste cropped pixels into new texture, �N���b�v���ꂽ�s�N�Z����V�����e�N�X�`���ɓ\��t����ʒu���v�Z
            int pasteX = (int)cropPositionLT.x + (int)(frameWidth / 2);
            int pasteY = (int)cropPositionRB.y + (int)(frameHeight / 2);

            // Adjusted pasting to fit within new texture range, �\��t�����V�����e�N�X�`���͈͓̔��Ɏ��܂�悤�ɒ���
            cropWidth = Mathf.Min(cropWidth, frameWidth - pasteX);
            cropHeight = Mathf.Min(cropHeight, frameHeight - pasteY);

            // Paste cropped pixels into new texture, �N���b�v���ꂽ�s�N�Z����V�����e�N�X�`���ɓ\��t��
            newTexture.SetPixels(pasteX, pasteY, cropWidth, cropHeight, croppedPixels);

            // Passing variables for vertex calculations, ���_�v�Z�p�ɕϐ��󂯓n��
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
    /// �e�N�X�`�����w�肳�ꂽ�}�e���A���Ɋ��蓖�Ă郁�\�b�h
    /// </summary>
    /// <param name="texture">���蓖�Ă�e�N�X�`��</param>
    /// <param name="material">�Ώۂ̃}�e���A��</param>
    private void AssignTextureToMaterial(Texture2D texture, Material material)
    {
        if (material == null)
        {
            //Debug.LogWarning("Material�����蓖�Ă��Ă��܂���B�C���X�y�N�^�[�Őݒ肵�Ă��������B");
            return;
        }

        if (texture == null)
        {
            //Debug.LogWarning("���蓖�Ă�Texture��null�ł��B");
            return;
        }

        // Set texture to material's main texture, �}�e���A���̃��C���e�N�X�`���ɐݒ�
        material.mainTexture = texture;
    }

    /// <summary>
    /// �w��{����Texture2D���X�P�[�����O���A�V����Texture2D���쐬���܂��B
    /// </summary>
    /// <param name="source">����Texture2D</param>
    /// <param name="scale">�X�P�[���{���i��F2.0f ��2�{�A0.5f �͔����j</param>
    /// <returns>�X�P�[�����O���ꂽ�V����Texture2D</returns>
    private static Texture2D ScaleTexture(Texture2D source, float scale)
    {
        if (scale <= 0)
        {
            //Debug.LogError("�X�P�[���{����0���傫���Ȃ���΂Ȃ�܂���B");
            return null;
        }

        int newWidth = Mathf.RoundToInt(source.width * scale);
        int newHeight = Mathf.RoundToInt(source.height * scale);

        // Create new RenderTexture, �V����RenderTexture���쐬
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Point; // �t�B���^�[���[�h��ݒ�i�K�v�ɉ����ĕύX�\�j

        // Blit original texture to RenderTexture, ���̃e�N�X�`����RenderTexture��Blit
        Graphics.Blit(source, rt);

        // Save current RenderTexture, ���݂�RenderTexture��ۑ����Ă���
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        // Create a new Texture2D in RGBA32 format and load pixel data, �V����Texture2D��RGBA32�`���ō쐬���ăs�N�Z���f�[�^��ǂݍ���
        Texture2D scaled = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        scaled.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        scaled.Apply();

        // Restore original RenderTexture, ����RenderTexture�𕜌�
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

        // Fill with Z of the farthest pixel, ��ԉ��ɂ���s�N�Z����Z��Fill����
        zValueMax = imageSplitter.PixelZMax;
        Array.Fill(zValues, zValueMax);

        // Calculate image size and mesh ratio, �摜�T�C�Y�ƃ��b�V���̔䗦���v�Z
        float meshScale = (float)meshWidth / (float)frameWidth;

        //Assign Z to the mesh from the Z data extracted from the right image, �E�摜������o����Z�f�[�^���烁�b�V����Z�����蓖�Ă�
        for (int j = 0; j < meshHeight + 1; j++)
        {
            for (int i = 0; i < meshWidth + 1; i++)
            {
                int matrixRow = 0;
                int matrixColumn = 0;
                if ((i >= _pasteX * (float)meshScale) && (i <= (_pasteX + _cropWidth) * (float)meshScale) && (j >= _pasteY * (float)meshScale) && (j <= (_pasteY + _cropHeight) * (float)meshScale))
                {
                    matrixColumn = Mathf.Min(Mathf.Max((int)(((Mathf.Min(i - _pasteX * (float)meshScale, meshWidth - 1) / meshScale) + _cropPositionX) / lastDisplayedScale),0),originalWidth - 1); //�G���[�΍�ōő�ŏ��𖳗����ݒ肵�Ă݂��B
                    matrixRow = Mathf.Min(Mathf.Max((int)(((Mathf.Min(j - _pasteY * (float)meshScale, meshHeight - 1) / meshScale) + _cropPositionY) / lastDisplayedScale),0),originalHeight - 1);

                    zValues[Mathf.Min((meshWidth + 1) * j + i,(meshWidth + 1)*(meshHeight + 1) - 1)] = pixelZMatrix[matrixRow, matrixColumn];
                }
                if (i < meshWidth && j < meshHeight) { quotedRow[j, i] = matrixRow; quotedColumn[j, i] = matrixColumn; }
            }
        }

        // The top column copies the value of the column directly below it, �ŏ��͒����̗�̒l���R�s�[
        for (int i = 0; i < meshWidth + 1; i++)
        {
            zValues[(meshWidth + 1) * meshHeight + i] = zValues[(meshWidth + 1) * (meshHeight - 1) + i];
        }

        // The rightmost column copies the value on the left, �ŉE��͍��ׂ̒l���R�s�[
        for (int j = 0; j < meshHeight + 1; j++)
        {
            zValues[(meshWidth + 1) * j + meshWidth] = zValues[(meshWidth + 1) * j + meshWidth - 1];
        }

        /*/ Filtering (test) No need to use!, �덷�̏C���i�e�X�g�j�K�v�Ȃ��I
        System.Random rnd = new System.Random();
        for (int j = 1; j < meshHeight - 1; j++)
        {
            for (int i = 1; i < meshWidth - 1; i++)
            {
                if ((i >= _pasteX * (float)meshScale) && (i <= (_pasteX + _cropWidth) * (float)meshScale) && (j >= _pasteY * (float)meshScale) && (j <= (_pasteY + _cropHeight) * (float)meshScale))
                {
                    // Reduce grid pattern (horizontal), �i�q��̌덷�����܂����i���j
                    if (quotedColumn[j,i] == quotedColumn[j,i - 1])
                    {
                        float random = (float)rnd.NextDouble() * 1.0f + 0.0f / 2;
                        //random = 1f;
                        zValues[Mathf.Min((meshWidth + 1) * j + i, (meshWidth + 1) * (meshHeight + 1) - 1)] = random * pixelZMatrix[quotedRow[j, i], quotedColumn[j, i]] + (1f - random) * pixelZMatrix[Mathf.Max(Mathf.Min(originalHeight - 1, quotedRow[j, i] + 1), 0), quotedColumn[j, i]];
                    }
                    // Reduce grid pattern (vertical), �i�q��̌덷�����܂����i�c�j
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
