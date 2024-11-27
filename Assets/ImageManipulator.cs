using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ImageManipulator : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    // Reference to ImageSplitter, ImageSplitterスクリプトへの参照をインスペクターから設定
    [SerializeField]
    private ImageSplitter imageSplitter;

    // Reference to Left Image, 左側のImageコンポーネントへの参照
    public Image leftImageSprite;

    // Variables for Drugging, ドラッグ用の変数
    private RectTransform rectTransform;
    private Vector2 originalLocalPointerPosition;
    private Vector3 originalPanelLocalPosition;

    // Variables for Zooming, ズーム用の変数
    public float zoomSpeed = 0.2f; // 拡大縮小の速度
    public float minScale = 1f;  // 最小スケール
    public float maxScale = 15f;    // 最大スケール

    // Variables for moving restriction, 移動制限用の変数
    private Vector3 initialLocalPosition;
    public float maxMoveX;   // 左右の移動制限（ピクセル、中心からの移動距離）
    public float maxMoveY;   // 上下の移動制限（ピクセル、中心からの移動距離）
    private float displayWidth; //画面表示上の横幅ピクセル数
    private float displayHeight; //画面表示上の縦幅ピクセル数
    private float offset; //imageSplitter.OFFSET値の代入先

    // Variables for Double Click, ダブルクリック用の変数
    //private bool isDoubleClickStart; //タップ認識中のフラグ
    //private float doubleTapTime = 0;

    // Flag for initialization, 初期化フラグ
    private bool isInitialized = false;

    // displayedPositionXの内部フィールド（インスペクターで設定可能）
    [SerializeField]
    private int _displayedPositionX = 0;

    /// <summary>
    /// 他のスクリプトから参照可能なdisplayedPositionXプロパティ（読み取り専用）。
    /// 表示画像のx座標値(表示上のセンター＝０）。
    /// </summary>
    public int displayedPositionX
    {
        get { return _displayedPositionX; }
    }

    // displayedPositionYの内部フィールド（インスペクターで設定可能）
    [SerializeField]
    private int _displayedPositionY = 0;

    /// <summary>
    /// 他のスクリプトから参照可能なdisplayedPositionYプロパティ（読み取り専用）。
    /// 表示画像のy座標値。
    /// </summary>
    public int displayedPositionY
    {
        get { return _displayedPositionY; }
    }

    // displayedScaleの内部フィールド（インスペクターで設定可能）
    [SerializeField]
    private float _displayedScale = 1f;

    /// <summary>
    /// 他のスクリプトから参照可能なdisplayedScaleプロパティ（読み取り専用）。
    /// 画像の拡大倍率値。
    /// </summary>
    public float displayedScale
    {
        get { return _displayedScale; }
    }

    /// <summary>
    /// Event Handler, called when an image is created, 画像生成時に呼び出されるイベントハンドラー
    /// </summary>
    /// <param name="imageObject">生成されたImageのGameObject</param>
    /// <param name="sprite">生成されたSprite</param>
    public void OnImageCreatedHandler(GameObject imageObject, Sprite sprite)
    {
        if (imageObject == null)
        {
            //Debug.LogError("ImageObjectがnullです。");
            return;
        }

        // Set an image componet as "LeftImageSprite", "LeftImageSprite"としてImageコンポーネントを設定
        leftImageSprite = imageObject.GetComponent<Image>();
        if (leftImageSprite == null)
        {
            //Debug.LogError("生成されたGameObjectにImageコンポーネントが含まれていません。");
            return;
        }

        // Setting Sprite, Spriteを設定
        leftImageSprite.sprite = sprite;

        // Get RectTransform, RectTransformを取得
        rectTransform = leftImageSprite.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            //Debug.LogError("LeftImageSpriteにRectTransformがありません。");
            return;
        }

        // Record Initial Position, 初期位置を記録
        initialLocalPosition = rectTransform.localPosition;

        // Flag of initialized, 初期化完了フラグを設定
        isInitialized = true;

        // Set display size and potision, displayサイズと場所の設定
        displayWidth = imageSplitter.OriginalWidth * imageSplitter.InitialScale;
        displayHeight = imageSplitter.OriginalHeight * imageSplitter.InitialScale;
        offset = imageSplitter.OFFSET;

        // Setting the drag range, ドラッグの可動範囲の設定
        maxMoveX = Mathf.Min(144f, displayWidth / 2 - 36);
        maxMoveY = Mathf.Min(284f, displayHeight / 2 - 36);
    }

    void Update()
    {
        if (!isInitialized)
            return;

        // Zooming with mouse wheel, マウスホイールによるズーム処理
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f && rectTransform != null)
        {
             Vector3 scale = rectTransform.localScale;
            scale += Vector3.one * scroll * zoomSpeed;
            // Scaling limitation, スケールを制限
            scale.x = Mathf.Clamp(scale.x, minScale, maxScale);
            scale.y = Mathf.Clamp(scale.y, minScale, maxScale);
            scale.z = 1f; // z軸のスケールは1に固定
            rectTransform.localScale = scale;
        }

        // Right click to set position default, 右クリック時に最初の場所に戻す
        if (Input.GetMouseButtonDown(1))
        {
            rectTransform.localPosition = initialLocalPosition;
            rectTransform.localScale = Vector3.one;
        }

        _displayedPositionX = (int)(rectTransform.localPosition.x - offset);
        _displayedPositionY = (int)rectTransform.localPosition.y;
        _displayedScale = rectTransform.localScale.x;
    }

    // Mouse button pushed, マウスボタンが押されたときの処理
    public void OnPointerDown(PointerEventData data)
    {
        if (rectTransform == null || !isInitialized)
            return;

        // Record initial position, 初期位置を記録
        originalPanelLocalPosition = rectTransform.localPosition;
        RectTransform parentRect = rectTransform.parent.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            data.position,
            data.pressEventCamera,
            out originalLocalPointerPosition);
    }

    // On dragging, ドラッグ中の処理
    public void OnDrag(PointerEventData data)
    {
        if (rectTransform == null || !isInitialized)
            return;

        RectTransform parentRect = rectTransform.parent.GetComponent<RectTransform>();
        Vector2 localPointerPosition;
        // Convert current pointer position to local coordinates, 現在のポインタ位置をローカル座標に変換
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            data.position,
            data.pressEventCamera,
            out localPointerPosition);

        // calculate offset, オフセットを計算
        Vector3 offsetToOriginal = localPointerPosition - originalLocalPointerPosition;

        // Set new position, 新しい位置を設定
        Vector3 newLocalPosition = originalPanelLocalPosition + new Vector3(offsetToOriginal.x, offsetToOriginal.y, 0);

        // Apply movement restrictions, 移動制限を適用
        Vector3 scale = rectTransform.localScale;
        newLocalPosition.x = Mathf.Clamp(newLocalPosition.x, initialLocalPosition.x - scale.x * displayWidth / 2 - maxMoveX, initialLocalPosition.x + scale.x * displayWidth / 2 + maxMoveX);
        newLocalPosition.y = Mathf.Clamp(newLocalPosition.y, initialLocalPosition.y - scale.y * displayHeight / 2 - maxMoveY, initialLocalPosition.y + scale.y * displayHeight / 2 + maxMoveY);

        rectTransform.localPosition = newLocalPosition;
    }
}
