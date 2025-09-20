using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 画像のドラッグ・ズーム・リセット操作を提供するコンポーネント
/// </summary>
public class ImageManipulator : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    // --- Inspectorで設定可能なフィールド ---
    [SerializeField] private ImageSplitter imageSplitter; // ImageSplitterへの参照

    // --- 公開フィールド ---
    public Image leftImageSprite; // 操作対象のImage

    // --- ドラッグ操作用 ---
    private RectTransform rectTransform;
    private Vector2 originalLocalPointerPosition;
    private Vector3 originalPanelLocalPosition;

    // --- ズーム操作用 ---
    [SerializeField] public float zoomSpeed = 0.2f; // ズーム速度
    [SerializeField] public float minScale = 1f;     // 最小スケール
    [SerializeField] public float maxScale = 15f;    // 最大スケール

    // --- 移動制限用 ---
    private Vector3 initialLocalPosition;
    [SerializeField] public float maxMoveX;   // 横方向の移動制限
    [SerializeField] public float maxMoveY;   // 縦方向の移動制限
    private float displayWidth;  // 表示画像の幅
    private float displayHeight; // 表示画像の高さ
    private float offset;        // オフセット値

    // --- 初期化フラグ ---
    private bool isInitialized = false;

    // --- 表示パラメータ ---
    [SerializeField] private int _displayedPositionX = 0;
    [SerializeField] private int _displayedPositionY = 0;
    [SerializeField] private float _displayedScale = 1f;

    public int displayedPositionX => _displayedPositionX;
    public int displayedPositionY => _displayedPositionY;
    public float displayedScale => _displayedScale;

    public event Action OnDisplayParametersChanged;

    /// <summary>
    /// 画像生成時の初期化ハンドラ
    /// </summary>
    /// <param name="imageObject">生成されたImageのGameObject</param>
    /// <param name="sprite">生成されたSprite</param>
    public void OnImageCreatedHandler(GameObject imageObject, Sprite sprite)
    {
        if (imageObject == null) return;

        // 操作対象Imageを取得
        leftImageSprite = imageObject.GetComponent<Image>();
        if (leftImageSprite == null) return;

        // Spriteを設定
        leftImageSprite.sprite = sprite;

        // RectTransformを取得
        rectTransform = leftImageSprite.GetComponent<RectTransform>();
        if (rectTransform == null) return;

        // 初期位置を記録
        initialLocalPosition = rectTransform.localPosition;

        // 初期化フラグ
        isInitialized = true;

        // 表示サイズ・位置を設定
        displayWidth = imageSplitter.OriginalWidth * imageSplitter.InitialScale;
        displayHeight = imageSplitter.OriginalHeight * imageSplitter.InitialScale;
        offset = imageSplitter.OFFSET;

        // ドラッグ範囲を設定
        maxMoveX = Mathf.Min(144f, displayWidth / 2 - 36);
        maxMoveY = Mathf.Min(284f, displayHeight / 2 - 36);
    }

    /// <summary>
    /// 毎フレーム呼ばれる。ズーム・リセット・表示パラメータ更新を行う
    /// </summary>
    void Update()
    {
        if (!isInitialized) return;

        // マウスホイールでズーム
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f && rectTransform != null)
        {
            Vector3 scale = rectTransform.localScale;
            scale += Vector3.one * scroll * zoomSpeed;
            scale.x = Mathf.Clamp(scale.x, minScale, maxScale);
            scale.y = Mathf.Clamp(scale.y, minScale, maxScale);
            scale.z = 1f;
            rectTransform.localScale = scale;
        }

        // 右クリックで位置・スケールをリセット
        if (Input.GetMouseButtonDown(1))
        {
            rectTransform.localPosition = initialLocalPosition;
            rectTransform.localScale = Vector3.one;
        }

        // 表示パラメータを更新
        int newDisplayedPositionX = (int)(rectTransform.localPosition.x - offset);
        int newDisplayedPositionY = (int)rectTransform.localPosition.y;
        float newDisplayedScale = rectTransform.localScale.x;

        bool hasChanged = newDisplayedPositionX != _displayedPositionX
            || newDisplayedPositionY != _displayedPositionY
            || !Mathf.Approximately(newDisplayedScale, _displayedScale);

        _displayedPositionX = newDisplayedPositionX;
        _displayedPositionY = newDisplayedPositionY;
        _displayedScale = newDisplayedScale;

        if (hasChanged)
        {
            OnDisplayParametersChanged?.Invoke();
        }
    }

    /// <summary>
    /// マウスボタン押下時の処理
    /// </summary>
    public void OnPointerDown(PointerEventData data)
    {
        if (rectTransform == null || !isInitialized) return;

        // 初期位置を記録
        originalPanelLocalPosition = rectTransform.localPosition;
        RectTransform parentRect = rectTransform.parent.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            data.position,
            data.pressEventCamera,
            out originalLocalPointerPosition);
    }

    public void OnPointerUp(PointerEventData data)
    {
        // 何もしない（将来の拡張用）
    }

    /// <summary>
    /// ドラッグ中の処理
    /// </summary>
    public void OnDrag(PointerEventData data)
    {
        if (rectTransform == null || !isInitialized) return;

        RectTransform parentRect = rectTransform.parent.GetComponent<RectTransform>();
        Vector2 localPointerPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            data.position,
            data.pressEventCamera,
            out localPointerPosition);

        // オフセット計算
        Vector3 offsetToOriginal = localPointerPosition - originalLocalPointerPosition;

        // 新しい位置を計算
        Vector3 newLocalPosition = originalPanelLocalPosition + new Vector3(offsetToOriginal.x, offsetToOriginal.y, 0);

        // 移動制限を適用
        Vector3 scale = rectTransform.localScale;
        newLocalPosition.x = Mathf.Clamp(newLocalPosition.x, initialLocalPosition.x - scale.x * displayWidth / 2 - maxMoveX, initialLocalPosition.x + scale.x * displayWidth / 2 + maxMoveX);
        newLocalPosition.y = Mathf.Clamp(newLocalPosition.y, initialLocalPosition.y - scale.y * displayHeight / 2 - maxMoveY, initialLocalPosition.y + scale.y * displayHeight / 2 + maxMoveY);

        rectTransform.localPosition = newLocalPosition;
    }
}
