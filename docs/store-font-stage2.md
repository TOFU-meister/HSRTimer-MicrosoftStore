# Store 版フォント調査 第2段階

対象はゲーム 1696732、Windows x64、Unity 2019.4.24f1、BepInEx 6.0.0-be.793 IL2CPP、HSRTimer 開発版 0.1.3-pre です。第1段階の Windows 試験では IMGUI は Arial、OS 一覧は191件、既存 Candidate は5件でした。5件はいずれもプレビュー・一時適用できたように見えました。この時点で OS フォント191件を個別に生成・描画した結果はありません。

## 生成経路と確認した証拠

以前取得した Store の `UnityEngine.TextRenderingModule.dll` の CLR メタデータとラッパー IL を読み取りました。現在の Windows インストールでの結果は、後述の診断レポートで照合できます。

| 経路 | 確認結果 |
| --- | --- |
| `Font()` | 手元の Store interop の Font 型には引数なしコンストラクターがありません。Unity 2019.4 の参照実装は `Internal_CreateFont(this, null)` を呼びます。既存 Font を任意 OS フォントに初期化する公開 API は確認できませんでした。 |
| `Font(string)` | 手元の interop では欠落しています。参照実装には名前とパスを扱う独立した経路があります。公開コンストラクターが存在する環境でだけ動作を試せるボタンを追加しました。 |
| `Font(Il2CppStringArray, int)` | 手元の interop で欠落しています。以前の実機で配列版がこのシグネチャの MissingMethodException を出したことと一致します。 |
| `CreateDynamicFontFromOSFont(string, int)` | 手元の生成ラッパーの IL は `Font(string)` を参照します。Unity 2019.4 の参照ソースは配列 ctor に委譲するため、生成されたラッパーと参照ソースで違いがあります。単一版は配列 ctor の欠落だけを理由にスキップしなくなりました。 |
| `CreateDynamicFontFromOSFont(Il2CppStringArray, int)` | ラッパー IL は欠落した配列 ctor を参照します。今回の診断でも、配列 ctor がなければ呼び出しを中止します。 |
| `GetOSInstalledFontNames()` | ラッパーがあり、実機では191件を返しました。ユーザーが追加した OS フォントも認識しました。 |
| `Internal_CreateFont`、`Internal_CreateFontFromPath`、`Internal_CreateDynamicFont` | 手元の interop には各名前の managed ラッパーと内部呼び出しの名前が見えます。登録の有無、正しいオブジェクトの初期化、メソッドの呼出し可否は別々です。 |
| `Font(IntPtr)` | interop に存在するラッパー用 ctor です。任意フォントを生成する公開 Unity API として扱いません。 |

公開仕様と参照実装は [Unity 2019.4 の TextRendering ソース](https://github.com/Unity-Technologies/UnityCsReference/blob/2019.4/Modules/TextRendering/TextRendering.bindings.cs) を参照しました。現行 Windows の `GameAssembly.dll`、`UnityPlayer.dll`、`global-metadata.dat` は今回の実行環境にありません。**Inspect interop and internal-call availability** は生成された型のシグネチャと MVID を記録し、実機では公式の IL2CPP 解決 API で名前が登録されているかだけを照会します。解決したポインターは呼び出しません。登録されていても公開 ctor と安全な初期化経路が得られるとは限りません。

## AssetBundle の方式

ゲーム側 `Assembly-CSharp.dll` に AssetBundle モジュールへの参照があり、Unity の公開 AssetBundle API はインポート済みのアセットを返します。診断では実行中の Store interop にある `LoadFromFile`、`GetAllAssetNames`、型指定 `LoadAsset`、`Unload` を確認し、必要な API が欠落していれば読み込みを中止します。手元の Store 用参照セットには AssetBundle モジュールの生成 DLL がないため、現行 Windows で各ラッパーが利用できるか、正常に読み込めるかは実機試験が必要です。

| モード | Editor 設定と確認点 |
| --- | --- |
| `embedded` | Dynamic、Include Font Data 有効。バンドル内のインポート済み Font をロードし、実行時に字形を追加します。プラグインは OS の Font ファクトリーや ctor を呼びません。ゲーム側でのアトラス構築は未検証です。 |
| `os` | Dynamic、Include Font Data 無効。フォント名を指定し、端末側に同じファミリーが必要です。OS の字形解決または代替フォントの挙動を比較できます。 |
| `static` | Custom Set、18 px。診断用サンプル文字だけを事前に生成します。拡大・太字・文字セット外の表示には制約があります。 |

[Unity 2019.4 Font アセット](https://docs.unity3d.com/2019.4/Documentation/Manual/class-Font.html) と [TrueTypeFontImporter](https://docs.unity3d.com/2019.4/Documentation/ScriptReference/TrueTypeFontImporter.html) に基づく構成です。

### Windows での作成

1. Unity Hub から **Unity 2019.4.24f1** を入れ、Windows のデスクトップターゲットを利用できる構成にします。Editor スクリプトは `StandaloneWindows64` 向けにアセットだけを出力します。Windows Player の IL2CPP コンパイルは行いません。
2. `tools/StoreFontBundleProject` を開き、静的ウェイトの TTF または OTF を `Assets/Fonts` に配置します。最初は1ファイルで試します。同じ配布元のライセンスも保管してください。
3. GUI では **Tools → HSRTimer → Build embedded font bundles** を実行します。PowerShell ではリポジトリ直下から次のように実行できます。試験モードは `embedded`、`os`、`static` から選びます。

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\2019.4.24f1\Editor\Unity.exe'
$project = (Resolve-Path '.\tools\StoreFontBundleProject').Path
$output = Join-Path $project 'Build\embedded'
$editor = Start-Process -FilePath $unity -ArgumentList @(
  '-batchmode', '-quit', '-buildTarget', 'Win64',
  '-projectPath', ('"{0}"' -f $project),
  '-executeMethod', 'StoreFontBundleBuilder.Build',
  '-fontMode', 'embedded', '-fontOutput', ('"{0}"' -f $output),
  '-logFile', ('"{0}"' -f (Join-Path $project 'font-bundle-build.log'))
) -Wait -PassThru
if ($editor.ExitCode -ne 0) { throw 'Unity bundle build failed. See font-bundle-build.log.' }
```

4. 出力した `*.bundle` だけを HSRTimer の Data Folder 内の `fonts` にコピーします。通常の場所は `Documents\HSRTimer-MicrosoftStore\fonts` です。実際の場所は **About → Open Data Folder** で確認できます。Editor と Windows ゲームでの生成・読み込みは今回未実行です。

### ライセンス

候補の [Noto Sans](https://github.com/google/fonts/blob/main/ofl/notosans/OFL.txt)、[Noto Sans JP](https://github.com/google/fonts/blob/main/ofl/notosansjp/OFL.txt)、[Noto Sans SC](https://github.com/google/fonts/blob/main/ofl/notosanssc/OFL.txt) は各配布元で SIL Open Font License 1.1 を確認しました。フォントを含む bundle を配布する場合は、実際に使ったフォントの著作権表示と OFL 本文を添付し、改変する場合は予約フォント名の条件も確認してください。このリポジトリとプラグインにはフォント本体や生成済み bundle を含めません。Microsoft YaHei などは端末上での比較用です。

## Windows での比較と記録

1. README の Release DLL 作成手順で DLL を作り、旧 HSRTimer を差し替えます。`Home → About → Font Diagnostics` で **Inspect interop and internal-call availability** と **List Unity OS fonts** を実行します。
2. 検索ボタンを押して `yahei`、`noto`、`arial` などを入力し、Enter で確定します。件数と選択位置を確認し、一覧から直接選びます。単一ファクトリー、チェーン、`Font(string)` は別々に試せます。
3. **Scan game fonts** で既存候補を再確認します。候補はフォント名・取得元・インスタンス ID・Dynamic/Static で識別できます。**Load local font bundles** は明示操作時だけ読み込みます。上限は4 bundle、各32 MiB・32アセットです。自分で作成した試験用 bundle を使用してください。
4. 標準と候補を比較し、サイズ・太字を切り替えて、欠字・豆腐・空白を確認します。描画呼び出しが返っただけでは適用できません。目視確認を記録した候補は、そのセッションの Timer HUD にだけ適用できます。
5. ステージ移動・One Key Retry 後に **Record current status** を押し、対象フォントの生存と表示を確認します。**Restore IMGUI / release probe fonts** で標準に戻してから診断所有の bundle・Font を解放します。再起動時には標準 IMGUI が使用されます。
6. **Export diagnostic report** を押すと、直近256件の診断が Data Folder の `font-diagnostics.txt` に出力されます。取得、プレビュー呼び出し、目視、適用、解除、各例外を記録します。通常プレイ中のフレーム単位の記録はありません。
