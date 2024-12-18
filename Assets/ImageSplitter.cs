using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.IO;
//using System.Windows.Forms; // Import System.Windows.Forms ���O��Ԃ��C���|�[�g
//using System;
//using System.Linq; // for basic functions, ��{�I�ȋ@�\�p
using SFB;
using System;

// Definition of custom UnityEvent, UnityEvent �̃J�X�^����`
[System.Serializable]
public class ImageCreatedEvent : UnityEvent<GameObject, Sprite> { }

public class ImageSplitter : MonoBehaviour
{
    // Target frame size (in pixels), �ڕW�̘g�T�C�Y�i�s�N�Z���P�ʁj
    private const int TARGET_WIDTH = 360;
    private const int TARGET_HEIGHT = 640;

    // Variable that holds a reference to the last displayed image, �O��\�������摜�̎Q�Ƃ�ێ�����ϐ�
    private GameObject previousImageObject;

    // Internal fields of OFFSET (configurable in Inspector), OFFSET�̓����t�B�[���h�i�C���X�y�N�^�[�Őݒ�\�j
    [SerializeField]
    private float _offset = 90f;

    /// <summary>
    /// ���̃X�N���v�g����Q�Ɖ\��OFFSET�v���p�e�B�i�ǂݎ���p�j�B
    /// �摜��x���W���E�ɂ��炷���߂̃I�t�Z�b�g�l�B
    /// </summary>
    public float OFFSET
    {
        get { return _offset; }
    }

    // originalWidth�̓����t�B�[���h
    [SerializeField]
    private int _originalWidth = 0;

    /// <summary>
    /// ���̃X�N���v�g����Q�Ɖ\��OriginalWidth�v���p�e�B�i�ǂݎ���p�j�B
    /// Width after separated, �������leftImage.png�̕��������܂��B
    /// </summary>
    public int OriginalWidth
    {
        get { return _originalWidth; }
    }

    // originalHeight�̓����t�B�[���h
    [SerializeField]
    private int _originalHeight = 0;

    /// <summary>
    /// ���̃X�N���v�g����Q�Ɖ\��OriginalHeight�v���p�e�B�i�ǂݎ���p�j�B
    /// Height after separeted, �������leftImage.png�̍����������܂��B
    /// </summary>
    public int OriginalHeight
    {
        get { return _originalHeight; }
    }

    // scale�̓����t�B�[���h�i��X�P�[���j
    [SerializeField]
    private float _scale = 1f;

    /// <summary>
    /// ���̃X�N���v�g����Q�Ɖ\��Scale�v���p�e�B�i�ǂݎ���p�j�B
    /// �摜�̊�X�P�[�����O�W���B
    /// </summary>
    public float InitialScale
    {
        get { return _scale; }
    }

    // pixelZdata�̓����t�B�[���h
    private float[,] _pixelZMatrix;

    /// <summary>
    /// Right side depth data (UInt32 stored in RGBA32) float[] (read-only) that can be referenced from other scripts.
    /// ���̃X�N���v�g����Q�Ɖ\�ȉE���f�v�X�f�[�^�iRGBA32��UInt32���i�[�jfloat[]�i�ǂݎ���p�j�B
    /// ���s���Q�Ƃ̌��f�[�^
    /// </summary>
    public float[,] PixelZMatrix
    {
        get { return _pixelZMatrix; }
    }

    // pixelZdata�̍ő�l�̓����t�B�[���h
    private float _pixelZMax;
    public float PixelZMax
    {
        get { return _pixelZMax; }
    }

    // ���b�V���T�C�Y��Dropdown���X�g
    [SerializeField]
    public GameObject dropdownItem;
    Dropdown dropdown;
    private int dropdownValue;

    // Mesh Width �̓����t�B�[���h
    [SerializeField]
    private int _meshX;

    /// <summary>
    /// Choosed mesh width from dropdown list, �h���b�v�_�E���̑I�����ʂɂ�郁�b�V���̕��̐�
    /// </summary>
    public int meshX
    {
        get { return _meshX; }
    }

    // Mesh Height �̓����t�B�[���h
    [SerializeField]
    private int _meshY;

    /// <summary>
    /// Choosed mesh height from dropdown list, �h���b�v�_�E���̑I�����ʂɂ�郁�b�V���̏c�̐�
    /// </summary>
    public int meshY
    {
        get { return _meshY; }
    }

    /// <summary>
    /// Image���쐬���ꂽ�ۂɔ�������UnityEvent�B
    /// �����Ƃ��ĐV�����쐬���ꂽImage��GameObject��Sprite��n���܂��B
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
    /// Attached to "Load" button, �{�^���� OnClick �C�x���g�ɃA�^�b�`���郁�\�b�h�B
    /// �t�@�C���_�C�A���O���J���A�I�����ꂽ�摜���������܂��B
    /// </summary>
    public void OpenAndProcessImage()
    {
        //Debug.Log("OpenAndProcessImage called.");
        /*
                // OpenFileDialog �̐ݒ�
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "PNG Files|*.png",
                    Title = "Select a PNG Image",
                    RestoreDirectory = true
                };

                // �t�@�C���_�C�A���O��\��
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
    /// List of dropdown for mesh resolutions, Dropdown ���X�g�Ɋ�Â����b�V���𑜓x�̃v���p�e�B�ݒ�
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
    /// �摜�𕪊����A�ۑ�����R���[�`���B
    /// </summary>
    /// <param name="filePath">�I�����ꂽ�摜�̃p�X</param>
    /// <returns>IEnumerator</returns>
    private IEnumerator SplitImage(string filePath)
    {
        //Debug.Log($"Splitting and saving image: {filePath}");

        // Load an image as Texture2D, �摜��Texture2D�Ƃ��ă��[�h
        Texture2D originalTexture = LoadTexture(filePath);
        if (originalTexture == null)
        {
            //Debug.LogError("Failed to load texture.");
            yield break;
        }

        // Set image width and height, �������镝�ƍ�����ݒ�
        _originalWidth = originalTexture.width / 2;
        _originalHeight = originalTexture.height;

        // Create left texture, �����̃e�N�X�`�����쐬
        Texture2D leftTexture = new Texture2D(_originalWidth, _originalHeight, originalTexture.format, false);
        // Create right texture, �E���̃e�N�X�`�����쐬�i���j�A�J���[��ԂƂ��Ĉ����j
        Texture2D rightTexture = new Texture2D(originalTexture.width - _originalWidth, _originalHeight, originalTexture.format, false, true); // true for linear

        // Get pixel data, �s�N�Z���f�[�^���擾
        Color32[] originalPixels = originalTexture.GetPixels32();
        Color32[] leftPixels = new Color32[_originalWidth * _originalHeight];
        Color32[] rightPixels = new Color32[(originalTexture.width - _originalWidth) * _originalHeight];

        // Copy left image pixels, �����̃s�N�Z�����R�s�[
        for (int y = 0; y < _originalHeight; y++)
        {
            for (int x = 0; x < _originalWidth; x++)
            {
                leftPixels[y * _originalWidth + x] = originalPixels[y * originalTexture.width + x];
            }
        }

        // Copy right image pixels,�E���̃s�N�Z�����R�s�[
        for (int y = 0; y < _originalHeight; y++)
        {
            for (int x = _originalWidth; x < originalTexture.width; x++)
            {
                rightPixels[y * (originalTexture.width - _originalWidth) + (x - _originalWidth)] = originalPixels[y * originalTexture.width + x];
            }
        }

        // Calculate the depth information from the right half of the image and make it into a two-dimensional array and set it in the property.
        // �E�����̉摜����[�x�����v�Z���Q�����z��ɂ��ăv���p�e�B�ɃZ�b�g
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

        // Set max depth, �v���p�e�B�ɍŐ[�l���Z�b�g
        _pixelZMax = Mathf.Max(pixelZData);

        // Set pixel data, �s�N�Z���f�[�^��ݒ�
        leftTexture.SetPixels32(leftPixels);
        leftTexture.Apply();

        // Load the left image as a sprite and place it on Canvas, �����̉摜���X�v���C�g�Ƃ��ă��[�h���ACanvas �ɔz�u
        StartCoroutine(LoadSpriteFromTexture(leftTexture));

        // Release memory, ���������
        Destroy(originalTexture);
        Destroy(rightTexture);

        yield return null;
    }

    /// <summary>
    /// �w�肳�ꂽ�p�X����X�v���C�g�����[�h���AUI �� Image �R���|�[�l���g�ŕ\�����܂��B
    /// �O��\�������摜������΍폜���܂��B
    /// </summary>
    /// <param name="path">�摜�t�@�C���̃p�X</param>
    /// <returns>IEnumerator</returns>
    private IEnumerator LoadSpriteFromTexture(Texture2D loadedTexture)
    {
        // Delete any previously displayed images, �O��\�������摜������΍폜
        if (previousImageObject != null)
        {
            //Debug.Log("Destroying previous image.");
            Destroy(previousImageObject);
            previousImageObject = null;
        }

        if (loadedTexture)
        {
            // Create Sprite, �X�v���C�g���쐬
            Sprite loadedSprite = Sprite.Create(loadedTexture, new Rect(0, 0, loadedTexture.width, loadedTexture.height),
                new Vector2(0.5f, 0.5f));

            // Create GameObject for left image on Canvas, Canvas���Image�p��GameObject���쐬
            GameObject imageObject = new GameObject("LeftImageSprite");
            imageObject.transform.SetParent(this.transform, false); // ImageSplitterManager ��e�ɐݒ�

            // Add image component, Image�R���|�[�l���g��ǉ�
            UnityEngine.UI.Image uiImage = imageObject.AddComponent<UnityEngine.UI.Image>();
            uiImage.sprite = loadedSprite;

            // Get RectTransform of the image, Image��RectTransform���擾
            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // Shift to the right by OFFSET, OFFSET �����E�ɂ��炷
            Vector2 offsetPosition = new Vector2(_offset, 0f);
            rectTransform.anchoredPosition = offsetPosition;

            // Calculate Scaling factor, �X�P�[�����O�W���̌v�Z
            float scaleFactor = CalculateScaleFactor(_originalWidth, _originalHeight, TARGET_WIDTH, TARGET_HEIGHT);
            _scale = scaleFactor;

            Vector2 newSize = CalculateScaledSize(_originalWidth, _originalHeight, TARGET_WIDTH, TARGET_HEIGHT, scaleFactor);
            rectTransform.sizeDelta = newSize;

            // Set sortingOrder of CanvasRenderer of the image, Image��CanvasRenderer��sortingOrder��ݒ�
            CanvasRenderer canvasRenderer = imageObject.GetComponent<CanvasRenderer>();
            canvasRenderer.SetAlpha(1f); // �����x��ݒ�i�K�v�ɉ����āj

            // Adjust the sort order in Canvas (order in Hierarchy determines display order), Canvas���̃\�[�g���𒲐��iHierarchy�ł̏������\����������j
            imageObject.transform.SetAsLastSibling();

            // Keep previous image as reference, �O��̉摜���Q�ƂƂ��ĕێ�
            previousImageObject = imageObject;

            // Invoke event to notify other objects, �C�x���g�𔭉΂��đ��̃I�u�W�F�N�g�ɒʒm
            OnImageCreated?.Invoke(imageObject, loadedSprite);
        }
        else
        {
            //Debug.LogError("Failed to load image as Texture2D");
        }

        yield return null;
    }

    /// <summary>
    /// Texture2D �����[�h���܂��B
    /// </summary>
    /// <param name="filePath">�摜�t�@�C���̃p�X</param>
    /// <returns>���[�h���ꂽ Texture2D �I�u�W�F�N�g</returns>
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
    /// �X�P�[�����O�W�����v�Z���܂��i�t�B�[���h���X�V���܂���j�B
    /// </summary>
    /// <param name="originalWidth">���̉摜�̕�</param>
    /// <param name="originalHeight">���̉摜�̍���</param>
    /// <param name="maxWidth">�ő啝</param>
    /// <param name="maxHeight">�ő卂��</param>
    /// <returns>�X�P�[�����O�W��</returns>
    private float CalculateScaleFactor(int originalWidth, int originalHeight, float maxWidth, float maxHeight)
    {
        float widthRatio = maxWidth / originalWidth;
        float heightRatio = maxHeight / originalHeight;
        float scaleFactor = Mathf.Min(widthRatio, heightRatio);
        return scaleFactor;
    }

    /// <summary>
    /// �w�肳�ꂽ�g�Ɏ��܂�悤�ɏc������ێ������V�����T�C�Y���v�Z���܂��B
    /// </summary>
    /// <param name="originalWidth">���̉摜�̕�</param>
    /// <param name="originalHeight">���̉摜�̍���</param>
    /// <param name="maxWidth">�ő啝</param>
    /// <param name="maxHeight">�ő卂��</param>
    /// <param name="scaleFactor">�X�P�[�����O�W��</param>
    /// <returns>�V�����T�C�Y (��, ����)</returns>
    private Vector2 CalculateScaledSize(int originalWidth, int originalHeight, float maxWidth, float maxHeight, float scaleFactor)
    {
        float newWidth = originalWidth * scaleFactor;
        float newHeight = originalHeight * scaleFactor;
        return new Vector2(newWidth, newHeight);
    }
}
