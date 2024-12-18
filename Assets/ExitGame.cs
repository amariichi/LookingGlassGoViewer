using UnityEditor;
using UnityEngine;
using System.IO;

// Exit after deleting the left image created by the script.
// スクリプトで作成した左画像を削除した後終了。
public class ExitGame : MonoBehaviour
{
    private string filePath;

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;//ゲームプレイ終了
#else
    Application.Quit();//ゲームプレイ終了
#endif
    }
}
