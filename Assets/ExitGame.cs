using UnityEditor;
using UnityEngine;
using System.IO;

// Exit after deleting the left image created by the script.
// �X�N���v�g�ō쐬�������摜���폜������I���B
public class ExitGame : MonoBehaviour
{
    private string filePath;

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;//�Q�[���v���C�I��
#else
    Application.Quit();//�Q�[���v���C�I��
#endif
    }
}
