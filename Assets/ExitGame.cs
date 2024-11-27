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
        FileUtil.DeleteFileOrDirectory("Assets/SplitImages/leftImage.png");
        FileUtil.DeleteFileOrDirectory("Assets/SplitImages/leftImage.png.meta");
        UnityEditor.EditorApplication.isPlaying = false;//�Q�[���v���C�I��
#else
    filePath = System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');//EXE�����s�����J�����g�f�B���N�g��
    if (System.IO.File.Exists(filePath + "\\Looking Glass Go Viewer_Data\\SplitImages\\leftImage.png"))
    {
        File.Delete(@filePath + "/Looking Glass Go Viewer_Data/SplitImages/leftImage.png"); //�X���b�V���ƃo�b�N�X���b�V�����������ċC�����������Ǔ���������u�B
        File.Delete(@filePath + "/Looking Glass Go Viewer_Data/SplitImages/leftImage.png.meta");
    }
    Application.Quit();//�Q�[���v���C�I��
#endif
    }
}
