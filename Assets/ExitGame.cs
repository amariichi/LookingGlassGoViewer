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
        FileUtil.DeleteFileOrDirectory("Assets/SplitImages/leftImage.png");
        FileUtil.DeleteFileOrDirectory("Assets/SplitImages/leftImage.png.meta");
        UnityEditor.EditorApplication.isPlaying = false;//ゲームプレイ終了
#else
    filePath = System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');//EXEを実行したカレントディレクトリ
    if (System.IO.File.Exists(filePath + "\\Looking Glass Go Viewer_Data\\SplitImages\\leftImage.png"))
    {
        File.Delete(@filePath + "/Looking Glass Go Viewer_Data/SplitImages/leftImage.png"); //スラッシュとバックスラッシュが同居して気持ち悪いけど動くから放置。
        File.Delete(@filePath + "/Looking Glass Go Viewer_Data/SplitImages/leftImage.png.meta");
    }
    Application.Quit();//ゲームプレイ終了
#endif
    }
}
