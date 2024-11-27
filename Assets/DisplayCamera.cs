using UnityEngine;

public class DisplayCamera : MonoBehaviour
{
    /// <summary>
    /// UI camera not showing Looking Glass, Looking Glass���f���Ă��Ȃ�UI�p�J����
    /// </summary>
    [SerializeField]
    private Camera _DisplayCamera;

    /// <summary>
    /// Width Resolution of Looking Glass Go, Looking Glass Go�̉𑜓x�i���j 
    /// </summary>
    private const int LOOKINGGLASS_WIDTH = 1440;

    /// <summary>
    /// Height Resolution of Looking Glass Go, Looking Glass Go �̉𑜓x�i�c�j
    /// </summary>
    private const int LOOKINGGLASS_HEIGHT = 2560;

    private void Start()
    {
        UnityEngine.Display[] displays = UnityEngine.Display.displays;

        // Display.displays[0] is the main default display and is always ON.
        // Check and activate additional displays. (quoted from the web)
        // Display.displays[0] �͎�v�f�t�H���g�f�B�X�v���C�ŁA��� ON�B
        // �ǉ��f�B�X�v���C���\�����m�F���A�N�e�B�x�[�g�B�iweb������p�j
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
