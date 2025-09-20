using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LookingGlassGoCamera : MonoBehaviour
{
    /// <summary>
    /// LookingGlassを映しているカメラ
    /// </summary>
    [SerializeField]
    private Camera _GoCamera;

    [SerializeField]
    private GameObject sliderCP;
    Slider sliderCameraPosition;

    [SerializeField]
    private GameObject valueCP;
    TextMeshProUGUI vCP;

    [SerializeField]
    private GameObject sliderCPM;
    Slider sliderCPMultiply;

    [SerializeField]
    private GameObject valueCPM;
    TextMeshProUGUI vCPM;



    /// <summary>
    /// Width Resolution of Looking Glass Go, Looking Glass Goの解像度（横） 
    /// </summary>
    private const int LOOKINGGLASS_WIDTH = 1440;

    /// <summary>
    /// Height Resolution of Looking Glass Go, Looking Glass Go の解像度（縦）
    /// </summary>
    private const int LOOKINGGLASS_HEIGHT = 2560;
    private const float BASE_CAMERA_Z = 50f;

    private bool isInitialized;

    private void Start()
    {
        //Activate the screen of Looking Glass Go, Looking Glass Go の画面をアクティベート
        //* not effective on Editor, ※Editor上では効かない。
        UnityEngine.Display[] displays = UnityEngine.Display.displays;

        // Display.displays[0] is the main default display and is always ON.
        // Check and activate additional displays. (quoted from the web)
        // Display.displays[0] は主要デフォルトディスプレイで、常に ON。
        // 追加ディスプレイが可能かを確認しアクティベート。（webから引用）
        if (UnityEngine.Display.displays.Length > 1)
            UnityEngine.Display.displays[1].Activate();

        for (int i = 0; i < displays.Length; i++)
        {
            if (displays[i].systemWidth != LOOKINGGLASS_WIDTH
                || displays[i].systemHeight != LOOKINGGLASS_HEIGHT) continue;
            _GoCamera.targetDisplay = i;
        }

        // Set Camera Position Values of Z Using Slider Values Info
        sliderCameraPosition = sliderCP.GetComponent<Slider>();
        vCP = valueCP.GetComponent<TextMeshProUGUI>();
        vCP.text = sliderCameraPosition.value.ToString("f1");
        AttachSliderKeyboardBlocker(sliderCP);

        sliderCPMultiply = sliderCPM.GetComponent<Slider>();
        vCPM = valueCPM.GetComponent<TextMeshProUGUI>();
        vCPM.text = GetMultiplierValue().ToString("f0");
        AttachSliderKeyboardBlocker(sliderCPM);

        isInitialized = true;
        ApplyCameraPosition();
    }

    public void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        ApplyCameraPosition();
    }

    public bool IsInitialized => isInitialized;

    public float CameraOffset => isInitialized ? sliderCameraPosition.value + GetMultiplierValue() : 0f;

    public float CameraZ => CameraOffset + BASE_CAMERA_Z;

    private void ApplyCameraPosition()
    {
        float multiplier = GetMultiplierValue();
        float offset = sliderCameraPosition.value + multiplier;

        Vector3 pos = transform.position;
        pos.z = offset + BASE_CAMERA_Z;
        transform.position = pos;

        vCP.text = sliderCameraPosition.value.ToString("f1");
        vCPM.text = multiplier.ToString("f0");
    }

    private float GetMultiplierValue()
    {
        if (sliderCPMultiply == null)
        {
            return 0f;
        }

        return Mathf.FloorToInt(sliderCPMultiply.value) * 10f;
    }

    private void AttachSliderKeyboardBlocker(GameObject sliderObject)
    {
        if (sliderObject == null)
        {
            return;
        }

        if (!sliderObject.TryGetComponent(out DisableSliderKeyboardInput blocker))
        {
            sliderObject.AddComponent<DisableSliderKeyboardInput>();
        }
    }
}
