using LookingGlass;
using System;
using System.Collections;
using System.IO;
using UnityEngine;

public class QuiltCapButton : MonoBehaviour
{
    [SerializeField]
    public QuiltCapture quiltCapture;

    private string filePath;

    void Start()
    {
#if UNITY_EDITOR
        filePath = Directory.GetCurrentDirectory();//Editor上では普通にカレントディレクトリを確認
#else
		filePath = System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');//EXEを実行したカレントディレクトリ (ショートカット等でカレントディレクトリが変わるのでこの方式で)
#endif
    }

    private void Update()
    {
        // Set saving quilt file name, ファイル名の設定
        string year = DateTime.Now.Year.ToString();
        string month = DateTime.Now.Month.ToString("00");
        string day = DateTime.Now.Day.ToString("00");
        string hour = DateTime.Now.Hour.ToString("00");
        string minute = DateTime.Now.Minute.ToString("00");
        string second = DateTime.Now.Second.ToString("00");
        string _filePath = filePath + "/Recordings/" + year + month + day + hour + minute + second + "LKG_Quilt_qs11x6a0.56.png";

        // When the F1 key is pressed, save the quilt image in the Recordings folder in the execution folder
        // F1キーが押されたら実行フォルダ内のRecordingsフォルダにquilt画像を保存
        if (Input.GetKey(KeyCode.F1))
        {
            StartCoroutine(Screenshot(_filePath));
        }
    }

    // capture a quilt image if "F1" key is pushed
    IEnumerator Screenshot(string path)
    {
        // It doesn't work properly with unity editor. There is no problem with the build app.
        // Capture the quilt. 
        // unity editor では動作不良。ビルドアプリでは問題なし。
        // quiltをキャプチャ。
        quiltCapture.Screenshot3D(path, false);
        yield return new WaitForSeconds(5f);// Wait 5 seconds.Is it okay ?, 5秒待つ。いいのか？
    }


}
