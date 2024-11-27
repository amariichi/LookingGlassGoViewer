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
        filePath = Directory.GetCurrentDirectory();//Editor��ł͕��ʂɃJ�����g�f�B���N�g�����m�F
#else
		filePath = System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');//EXE�����s�����J�����g�f�B���N�g�� (�V���[�g�J�b�g���ŃJ�����g�f�B���N�g�����ς��̂ł��̕�����)
#endif
    }

    private void Update()
    {
        // Set saving quilt file name, �t�@�C�����̐ݒ�
        string year = DateTime.Now.Year.ToString();
        string month = DateTime.Now.Month.ToString("00");
        string day = DateTime.Now.Day.ToString("00");
        string hour = DateTime.Now.Hour.ToString("00");
        string minute = DateTime.Now.Minute.ToString("00");
        string second = DateTime.Now.Second.ToString("00");
        string _filePath = filePath + "/Recordings/" + year + month + day + hour + minute + second + "LKG_Quilt.png";

        // When the F1 key is pressed, save the quilt image in the Recordings folder in the execution folder
        // F1�L�[�������ꂽ����s�t�H���_����Recordings�t�H���_��quilt�摜��ۑ�
        if (Input.GetKey(KeyCode.F1))
        {
            StartCoroutine(Screenshot(_filePath));
        }
    }

    // capture a quilt image if "F1" key is pushed
    IEnumerator Screenshot(string path)
    {
        // It doesn't work properly with unity editor. There is no problem with the build app.
        // Capture the quilt. It seems that meta data is not entered even if it is set to true.
        // When registering to looking glass studio, please manually set the number of quilts to 11x6.
        // Sorry for your inconvenience.
        // unity editor �ł͓���s�ǁB�r���h�A�v���ł͖��Ȃ��B
        // quilt���L���v�`���Bmeta�f�[�^��true�ɂ��Ă�����Ȃ��l�q�B
        // looking glass studio�ɓo�^����ۂ͎蓮��quilt�̐���11x6�Ƃ��Ă��������B���萔�����������܂��B
        quiltCapture.Screenshot3D(path, false);
        yield return new WaitForSeconds(5f);// Wait 5 seconds.Is it okay ?, 5�b�҂B�����̂��H
    }


}
