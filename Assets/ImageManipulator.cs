using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ImageManipulator : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    // Reference to ImageSplitter, ImageSplitter�X�N���v�g�ւ̎Q�Ƃ��C���X�y�N�^�[����ݒ�
    [SerializeField]
    private ImageSplitter imageSplitter;

    // Reference to Left Image, ������Image�R���|�[�l���g�ւ̎Q��
    public Image leftImageSprite;

    // Variables for Drugging, �h���b�O�p�̕ϐ�
    private RectTransform rectTransform;
    private Vector2 originalLocalPointerPosition;
    private Vector3 originalPanelLocalPosition;

    // Variables for Zooming, �Y�[���p�̕ϐ�
    public float zoomSpeed = 0.2f; // �g��k���̑��x
    public float minScale = 1f;  // �ŏ��X�P�[��
    public float maxScale = 15f;    // �ő�X�P�[��

    // Variables for moving restriction, �ړ������p�̕ϐ�
    private Vector3 initialLocalPosition;
    public float maxMoveX;   // ���E�̈ړ������i�s�N�Z���A���S����̈ړ������j
    public float maxMoveY;   // �㉺�̈ړ������i�s�N�Z���A���S����̈ړ������j
    private float displayWidth; //��ʕ\����̉����s�N�Z����
    private float displayHeight; //��ʕ\����̏c���s�N�Z����
    private float offset; //imageSplitter.OFFSET�l�̑����

    // Variables for Double Click, �_�u���N���b�N�p�̕ϐ�
    //private bool isDoubleClickStart; //�^�b�v�F�����̃t���O
    //private float doubleTapTime = 0;

    // Flag for initialization, �������t���O
    private bool isInitialized = false;

    // displayedPositionX�̓����t�B�[���h�i�C���X�y�N�^�[�Őݒ�\�j
    [SerializeField]
    private int _displayedPositionX = 0;

    /// <summary>
    /// ���̃X�N���v�g����Q�Ɖ\��displayedPositionX�v���p�e�B�i�ǂݎ���p�j�B
    /// �\���摜��x���W�l(�\����̃Z���^�[���O�j�B
    /// </summary>
    public int displayedPositionX
    {
        get { return _displayedPositionX; }
    }

    // displayedPositionY�̓����t�B�[���h�i�C���X�y�N�^�[�Őݒ�\�j
    [SerializeField]
    private int _displayedPositionY = 0;

    /// <summary>
    /// ���̃X�N���v�g����Q�Ɖ\��displayedPositionY�v���p�e�B�i�ǂݎ���p�j�B
    /// �\���摜��y���W�l�B
    /// </summary>
    public int displayedPositionY
    {
        get { return _displayedPositionY; }
    }

    // displayedScale�̓����t�B�[���h�i�C���X�y�N�^�[�Őݒ�\�j
    [SerializeField]
    private float _displayedScale = 1f;

    /// <summary>
    /// ���̃X�N���v�g����Q�Ɖ\��displayedScale�v���p�e�B�i�ǂݎ���p�j�B
    /// �摜�̊g��{���l�B
    /// </summary>
    public float displayedScale
    {
        get { return _displayedScale; }
    }

    /// <summary>
    /// Event Handler, called when an image is created, �摜�������ɌĂяo�����C�x���g�n���h���[
    /// </summary>
    /// <param name="imageObject">�������ꂽImage��GameObject</param>
    /// <param name="sprite">�������ꂽSprite</param>
    public void OnImageCreatedHandler(GameObject imageObject, Sprite sprite)
    {
        if (imageObject == null)
        {
            //Debug.LogError("ImageObject��null�ł��B");
            return;
        }

        // Set an image componet as "LeftImageSprite", "LeftImageSprite"�Ƃ���Image�R���|�[�l���g��ݒ�
        leftImageSprite = imageObject.GetComponent<Image>();
        if (leftImageSprite == null)
        {
            //Debug.LogError("�������ꂽGameObject��Image�R���|�[�l���g���܂܂�Ă��܂���B");
            return;
        }

        // Setting Sprite, Sprite��ݒ�
        leftImageSprite.sprite = sprite;

        // Get RectTransform, RectTransform���擾
        rectTransform = leftImageSprite.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            //Debug.LogError("LeftImageSprite��RectTransform������܂���B");
            return;
        }

        // Record Initial Position, �����ʒu���L�^
        initialLocalPosition = rectTransform.localPosition;

        // Flag of initialized, �����������t���O��ݒ�
        isInitialized = true;

        // Set display size and potision, display�T�C�Y�Əꏊ�̐ݒ�
        displayWidth = imageSplitter.OriginalWidth * imageSplitter.InitialScale;
        displayHeight = imageSplitter.OriginalHeight * imageSplitter.InitialScale;
        offset = imageSplitter.OFFSET;

        // Setting the drag range, �h���b�O�̉��͈͂̐ݒ�
        maxMoveX = Mathf.Min(144f, displayWidth / 2 - 36);
        maxMoveY = Mathf.Min(284f, displayHeight / 2 - 36);
    }

    void Update()
    {
        if (!isInitialized)
            return;

        // Zooming with mouse wheel, �}�E�X�z�C�[���ɂ��Y�[������
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f && rectTransform != null)
        {
             Vector3 scale = rectTransform.localScale;
            scale += Vector3.one * scroll * zoomSpeed;
            // Scaling limitation, �X�P�[���𐧌�
            scale.x = Mathf.Clamp(scale.x, minScale, maxScale);
            scale.y = Mathf.Clamp(scale.y, minScale, maxScale);
            scale.z = 1f; // z���̃X�P�[����1�ɌŒ�
            rectTransform.localScale = scale;
        }

        // Right click to set position default, �E�N���b�N���ɍŏ��̏ꏊ�ɖ߂�
        if (Input.GetMouseButtonDown(1))
        {
            rectTransform.localPosition = initialLocalPosition;
            rectTransform.localScale = Vector3.one;
        }

        _displayedPositionX = (int)(rectTransform.localPosition.x - offset);
        _displayedPositionY = (int)rectTransform.localPosition.y;
        _displayedScale = rectTransform.localScale.x;
    }

    // Mouse button pushed, �}�E�X�{�^���������ꂽ�Ƃ��̏���
    public void OnPointerDown(PointerEventData data)
    {
        if (rectTransform == null || !isInitialized)
            return;

        // Record initial position, �����ʒu���L�^
        originalPanelLocalPosition = rectTransform.localPosition;
        RectTransform parentRect = rectTransform.parent.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            data.position,
            data.pressEventCamera,
            out originalLocalPointerPosition);
    }

    // On dragging, �h���b�O���̏���
    public void OnDrag(PointerEventData data)
    {
        if (rectTransform == null || !isInitialized)
            return;

        RectTransform parentRect = rectTransform.parent.GetComponent<RectTransform>();
        Vector2 localPointerPosition;
        // Convert current pointer position to local coordinates, ���݂̃|�C���^�ʒu�����[�J�����W�ɕϊ�
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            data.position,
            data.pressEventCamera,
            out localPointerPosition);

        // calculate offset, �I�t�Z�b�g���v�Z
        Vector3 offsetToOriginal = localPointerPosition - originalLocalPointerPosition;

        // Set new position, �V�����ʒu��ݒ�
        Vector3 newLocalPosition = originalPanelLocalPosition + new Vector3(offsetToOriginal.x, offsetToOriginal.y, 0);

        // Apply movement restrictions, �ړ�������K�p
        Vector3 scale = rectTransform.localScale;
        newLocalPosition.x = Mathf.Clamp(newLocalPosition.x, initialLocalPosition.x - scale.x * displayWidth / 2 - maxMoveX, initialLocalPosition.x + scale.x * displayWidth / 2 + maxMoveX);
        newLocalPosition.y = Mathf.Clamp(newLocalPosition.y, initialLocalPosition.y - scale.y * displayHeight / 2 - maxMoveY, initialLocalPosition.y + scale.y * displayHeight / 2 + maxMoveY);

        rectTransform.localPosition = newLocalPosition;
    }
}
