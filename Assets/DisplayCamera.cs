using UnityEngine;

public class DisplayCamera : MonoBehaviour
{
    /// <summary>
    /// UI camera not showing Looking Glass, Looking Glassを映していないUI用カメラ
    /// </summary>
    [SerializeField]
    private Camera _DisplayCamera;

    /// <summary>
    /// Width Resolution of Looking Glass Go, Looking Glass Goの解像度（横） 
    /// </summary>
    private const int LOOKINGGLASS_WIDTH = 1440;

    /// <summary>
    /// Height Resolution of Looking Glass Go, Looking Glass Go の解像度（縦）
    /// </summary>
    private const int LOOKINGGLASS_HEIGHT = 2560;

    private void Start()
    {
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
                || displays[i].systemHeight != LOOKINGGLASS_HEIGHT)
            {
                _DisplayCamera.targetDisplay = i;
            }
        }
    }
}
