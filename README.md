## Looking Glass Go Viewer App for windows

![app](https://github.com/user-attachments/assets/3d6f9620-8a40-4f43-b5a4-c683f2a371ed) ![app2](https://github.com/user-attachments/assets/211f5bd9-b899-4d91-937b-9387a54fca49)

### 概要
付属の Python スクリプトを使用して、Apple が公開した Depth Pro で推定した画像のデプス情報を追加したPNG画像データを作成します。そのPNG画像を読み込み、画像を拡大したり、Looking Glass Go での見え方を調整するツールです。
Looking Glass Go では、遠くにあるものは大きくぼやけてしまいますが、このツールは奥行きを調整することができるので、元の 2D 画像に近い見え方の裸眼立体視画像（Quilt）を出力することもできます。
出力した Quilt 画像は LookingGlassStudio で読み込むことができます。

python スクリプトの実行には、別途 Depth Pro [[URL](https://github.com/apple/ml-depth-pro)] のインストールが必要です。

### [ファイルのダウンロード](https://github.com/amariichi/LookingGlassGoViewer/releases/tag/v0.1.0)

### 設定方法及び使用方法の概要
1. Depth Pro を公式ページに記載の方法でインストール。
2. 1.で Depth Pro をインストールしたフォルダ内に、ビルドして作られたすべてのファイルやフォルダをすべてコピー。[^1]
3. Looking Glass Go で裸眼立体視したい画像をinputフォルダに入れる。
4. ターミナル上で `python depth-pro_rgbde.py` と入力してスクリプトを実行。`output` フォルダに左半分が元画像で右半分がほぼ透明[^2] の PNG ファイルが生成されます。
5. Looking Glass Bridge [[URL(https://lookingglassfactory.com/software/looking-glass-bridge)]] を実行し常駐させます。
6. スクリプトで出力した画像をアプリで読み込むと Looking Glass Go に立体画像が表示されます。
7. アプリでズームや奥行きを調整し、F1 キーを押し、Quilt 画像を保存。

[^1]: アプリは任意の場所に置いて実行することができますが、その場合は `depth-pro_rgbde.py` は Depth Pro のフォルダに入れてスクリプトを実行します。また `input`、`output` フォルダも作成してください。

[^2]: Depth Pro の推定デプスの最大値は 10,000m です。このデプス情報を 10,000 倍した値を uint32 にして、8 ビットずつリトルエンディアンで RGBA に保存していますので、アルファチャンネルの値は 5 以内に収まります。このため右側はほぼ透明の画像となっています。

### アプリの具体的な使用方法
- メッシュの解像度を選択します。解像度を細かくすると動作が重くなります。なお、画像を読み込んだ後はこの選択肢を変更しても解像度は変更されません。また、メッシュを細かくしても Looking Glass Go で見る画質はほとんど違わないように見えます。
- **"LOAD RGBDE"** ボタンを押すと表示されるファイラー（UnityStandaloneFileBrowser）で読み込む画像を選択します。
- マウスの左ドラッグで移動、ホイールでズーム、右クリックで初期状態になります。
- 各スライダーの関係は下の図のとおりです。なお、2つの "Camera Position" スライダーは Looking Glass Go 上の合焦する奥行きの調整に使用します。

![fig1](https://github.com/user-attachments/assets/4e8e1507-1e02-4fd2-b5b6-7451270beeb6)

- **F1 キー**を押すと `Recordings` フォルダに Quilt 画像が保存されます。Quilt 画像は LookingGlassStudio で読み込むことができます。なお、画像にメタデータは入っていないので、手動で横11 x 縦６を指定します。
- **QUIT** ボタンでアプリを終了します。

### Unity Editor への読み込み及び利用
- ソースファイルの内容を任意のフォルダに入れ、Unity Hub の Add project from disk で当該フォルダを指定してプロジェクトを追加します。
- 外部パッケージが含まれていないので途中でコンパイルエラーが出ますが、`Ignore` を選択します。
- Unity Editor が自動的に追加したカメラとライトを削除します。
- Looking Glass Unity Plugin パッケージ [[URL](https://lookingglassfactory.com/software/looking-glass-unity-plugin)] と UnityStandaloneFileBrowser の Unity パッケージ [[URL](https://github.com/gkngkc/UnityStandaloneFileBrowser)] をダウンロードし、プロジェクトに追加（`import package -> custom package`）します。これでエラーは解消されます。
- `Assets > Scenes` にある `SampleScene` を Hierarchy にドロップします。
- 追加した "Sample Scene" 以外のシーンが Hierarchy にある場合は、Unity Editor が自動で追加したものなので削除してください。
- このアプリを実行する前に Looking Glass Bridge [[URL(https://lookingglassfactory.com/software/looking-glass-bridge)]] を実行して常駐させてください。
- **CTRL + E** で Looking Glass Go の画面出力がアクティベートされます。
- Unity Editor 内で実行した場合、F1 キーを押すとエラーで中断しますが、Quilt 画像は正しく生成されます。アプリの実行を再開して引き続き使用することもできます。なお、Quilt 画像は Hologram Camera 内の `QuiltCapture.cs` のインスペクタから生成することもできます（むしろこれが正しい Quilt 画像作成方法です）。

### Q&A
**Q: 通常の RGBD 画像との違いは何ですか。**

**A:** 通常、RGBD 画像は、元画像の右側に255階調のデプス情報を保持します。Depth Pro 付属の `run.py` では、0.1m から 250m までのデプス情報の逆数で各ピクセルのデプスを正規化し、0 から 255 までのデプスを割り当てています。この方法では、近くにある被写体の震度情報の解像度は高いですが、例えば地面、壁、草、偶然映り込んだ小さな物体などが手前にあり、奥にメインの被写体があるような画像の場合、メインの被写体のデプス情報の階調が低くなり、凹凸の少ない出力となってしまいます（下の図を参照）。一方、このツールのスクリプトでは、元の float のデプス情報を１万倍して uint32 で保持していますので、メインの被写体にズームしてもデプスの情報が元の推定どおり維持されますので立体感が損なわれません。

![fig2](https://github.com/user-attachments/assets/15175e2d-41d7-4a30-a5a5-6748065f1ff2)

**Q: 出力画像のサイズが大きいです。圧縮してもデプス情報は維持されますか。**

**A:** 非圧縮で出力しているため、サイズが大きいです。試しに Gimp で "Save color values from transparent pixels" にチェックを入れ、圧縮レベルを 9 にして PNG 形式で保存した画像をこのアプリで読み込んでみました。どのようなデプス情報が保持されているかは未確認ですが、デプスの相互関係が大きく破綻する様子は観られませんでした。圧縮した画像を使用するとアプリの動作が軽くなりますが、画質は低下します。

---
The following is an automatic translation by ChatGPT and is a provisional translation.

### Overview
First, use the included Python script to create PNG image data by adding depth information—estimated by Apple's published Depth Pro—to the original image. This tool allows you to load that PNG image, enlarge it, and adjust how it appears on Looking Glass Go.

In Looking Glass Go, objects that are far away tend to appear significantly blurred. However, since this tool allows you to adjust the depth, you can output a naked-eye stereoscopic image (Quilt) that looks closer to the original 2D image.

The output Quilt image can be loaded into Looking Glass Studio.

To run the Python script, you need to install Depth Pro separately. [[URL](https://github.com/apple/ml-depth-pro)]

### [Download Files](https://github.com/amariichi/LookingGlassGoViewer/releases/tag/v0.1.0)

### Summary of Setup and Usage
1. Install Depth Pro according to the method described on the official page.
2. Copy all the built files and folders created into the folder where you installed Depth Pro in step 1.[^3]
3. Place the image you want to view in naked-eye stereoscopic mode on Looking Glass Go into the `input` folder.
4. Run the script by typing `python depth-pro_rgbde.py` in the terminal. A PNG file will be generated in the `output` folder, where the left half is the original image and the right half is almost transparent.[^4]
5.Run Looking Glass Bridge [URL].
6. When you load the image output by the script into the app, a stereoscopic image will be displayed on Looking Glass Go.
7. Adjust zoom and depth in the app, press the **F1 key**, and save the Quilt image.

[^3]: The app can be placed and run from any location, but in that case, place `depth-pro_rgbde.py` in the Depth Pro folder and run the script. Also, please create `input` and `output` folders.

[^4]: The maximum depth estimated by Depth Pro is 10,000 meters. This depth information is multiplied by 10,000, converted to `uint32`, and saved in RGBA as 8-bit little-endian values. Therefore, the alpha channel values are within 5, making the right side an almost transparent image.

### Detailed Usage of the App
- **Select Mesh Resolution**: Increasing the resolution will make the operation heavier. Note that changing this option after loading the image will not alter the resolution. Also, even if you increase the mesh resolution, the image quality when viewed on Looking Glass Go does not seem to differ much.
- **Load Image**: Press the **"LOAD RGBDE"** button and select the image to load in the file browser (UnityStandaloneFileBrowser) that appears.
- **Navigation Controls**: Left-drag with the mouse to move, scroll the wheel to zoom, right-click to return to the initial state.
- **Sliders**: The relationship of each slider is as shown in the figure below. Note that the two "Camera Position" sliders are used to adjust the depth at which focus is achieved on Looking Glass Go.

![fig1](https://github.com/user-attachments/assets/4e8e1507-1e02-4fd2-b5b6-7451270beeb6)

- **Save Quilt Image**: Press the **F1 key** to save the Quilt image in the `Recordings` folder. The Quilt image can be loaded into Looking Glass Studio. Note that metadata is not included in the image, so you need to manually specify 11 horizontal x 6 vertical tiles.
- **Exit App**: Use the **QUIT** button to exit the app.

### Loading and Using in Unity Editor
- Place the contents of the source files into any folder and add the project by specifying that folder in Unity Hub with **Add project from disk**.
- Since external packages are not included, a compile error will occur during the process; select **Ignore**.
- Delete the camera and light that Unity Editor automatically adds.
- Download and add (via **Import Package -> Custom Package**) the Looking Glass Unity Plugin package [[URL](https://lookingglassfactory.com/software/looking-glass-unity-plugin)] and the UnityStandaloneFileBrowser Unity package [[URL](https://github.com/gkngkc/UnityStandaloneFileBrowser)] to the project. This will resolve the errors.
- Drag `SampleScene` located in `Assets > Scenes` into the Hierarchy panel.
- If there are scenes other than the added "SampleScene" in the Hierarchy, delete them as they were automatically added by Unity Editor.
- Before running this app, run Looking Glass Bridge [[URL(https://lookingglassfactory.com/software/looking-glass-bridge)]].
- Press **CTRL + E** to activate the screen output of Looking Glass Go.
- When running within Unity Editor, pressing the **F1 key** will cause an error and interrupt execution, but the Quilt image will be correctly generated. You can resume app execution and continue using it. Note that you can also generate Quilt images from the inspector of `QuiltCapture.cs` inside the Hologram Camera (this is actually the correct way to create Quilt images).

### Q&A
**Q: What is the difference from a normal RGBD image?**

**A:** Normally, an RGBD image retains 255-level depth information on the right side of the original image. In `run.py` included with Depth Pro, the inverse of depth information from 0.1m to 250m is normalized for each pixel, and depths from 0 to 255 are assigned. In this method, the depth resolution of close objects is high, but in images where, for example, the ground, walls, grass, or small objects accidentally captured are in the foreground and the main subject is in the background, the depth gradation of the main subject becomes low. This results in an output with less depth detail (see the figure below). On the other hand, in the script of this tool, the original float depth information is multiplied by 10,000 and stored as `uint32`. Therefore, even if you zoom in on the main subject, the depth information is maintained as originally estimated, preserving the stereoscopic effect.

![fig2](https://github.com/user-attachments/assets/15175e2d-41d7-4a30-a5a5-6748065f1ff2)

**Q: The size of the output image is large. Will the depth information be preserved if compressed?**

**A:** Since the output is uncompressed, the file size is large. I tried loading an image saved in PNG format using GIMP with "Save color values from transparent pixels" checked and compression level set to 9 into this app. While I haven't confirmed the exact depth information retained, there seemed to be no significant distortion in the relative depth relationships. Using compressed images makes the app run smoother, but the image quality may deteriorate.
