# 第9段階 — 複数の外部フォントを比較する

対象は Human: Fall Flat Microsoft Store v1696732、Unity 2019.4.24f1、BepInEx 6.0.0-be.793、Windows x64、HSRTimer 開発版 0.1.3-pre。Font Diagnostics 内での一時適用を使います。

## 試験フォント

下表は静的 OTF / TTF の実ファイルを検査した結果です。[試験フォント一覧 JSON](../tools/StoreFontBundleProject/test-fonts.json) に公式配布元の直接ダウンロード URL、ライセンスの URL、検証用 SHA256 を記録しています。フォントの版はファイル内の版表記です。

| グループ | フォントと公式配布元 | 版 | ライセンス | サイズ (bytes) | SHA256 |
| --- | --- | --- | --- | ---: | --- |
| A | [Noto Sans CJK JP Regular](https://github.com/notofonts/noto-cjk/releases/tag/Sans2.004) | 2.004 | OFL 1.1 | 16467736 | `68a3fc98800b2a27b371f2fb79991daf3633bd89309d4ffaa6946fd587f375b5` |
| A | [Noto Sans CJK JP Bold](https://github.com/notofonts/noto-cjk/releases/tag/Sans2.004) | 2.004 | OFL 1.1 | 17032620 | `e53dcb0dcb2922e45d01aae1ebd2f382bb81d4229b18b6b883bd170678af1f76` |
| A | [Noto Sans CJK SC Regular](https://github.com/notofonts/noto-cjk/releases/tag/Sans2.004) | 2.004 | OFL 1.1 | 16437364 | `2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b` |
| B | [Roboto Regular](https://github.com/googlefonts/roboto/tree/v2.138/src/hinted) | 2.001047 (配布元タグ v2.138) | Apache 2.0 | 515100 | `56a45233d29f11b4dfb86d248e921939d115778f87325e7ae8cc108383d6664d` |
| B | [Roboto Mono Regular](https://github.com/googlefonts/RobotoMono/tree/7bf0b33178844cc7d0c9915b746ece1be7f46bea/fonts/ttf) | 3.001 | OFL 1.1 | 125748 | `af0bff7599c3df3831755c16e39b3c496df74b8c8d8a1161b14dc8461be17cb4` |

| フォント | 文字マップの範囲と試験文字 | 数字 0–9 の送り幅 (フォント単位 / em) |
| --- | --- | --- |
| Noto 3種 | 各 44,810 符号位置。英数字、指定の記号、ひらがな、カタカナ、日本語と簡体・繁体中文の試験文字をすべて収録 | JP Regular 555/1000、JP Bold 590/1000、SC Regular 555/1000 |
| Roboto | 2,772 符号位置。ラテン、ギリシャ、キリルなど。指定の英数字・記号を収録 | 1150/2048 |
| Roboto Mono | 876 符号位置。ラテン、ギリシャ、キリルなど。指定の英数字・記号を収録 | 1229/2048 |

Roboto 2種には今回の日本語・中国語の試験文字がありません。表示された場合は Unity のフォールバックとして記録し、欠字表示と区別します。収録範囲はファイルの文字マップ、送り幅はフォントのメトリクスを確認した値です。最終的な IMGUI の描画と送り幅は実機で比較します。生成スクリプトは配布元のライセンスを `licenses` に保存します。Bundle を再配布する場合は、対象フォントのライセンスと著作権表示を添えて各ライセンスの条件に従ってください。

## Windows PowerShell で生成する

Unity Hub で Unity Editor **2019.4.24f1** と Windows Build Support を使用できる状態にしてください。独立した試験プロジェクトにフォントを配置するため、既存の Unity プロジェクトに追加したフォントはそのまま利用できます。

```powershell
Set-Location C:\HSRTimer-MicrosoftStore
git switch dev
git pull --ff-only origin dev

$run = Join-Path $env:TEMP ("hsrtimer-font-stage9-" + [guid]::NewGuid())
.\scripts\New-StoreFontTestBundles.ps1 -OutputDirectory $run
Get-ChildItem -Path "$run\group-*\*.bundle"
Import-Csv "$run\bundle-hashes.csv"
Import-Csv "$run\bundle-comparison.csv"
Select-String -Path "$run\logs\*.log" -Pattern 'Individual dependencies','Editor verified standalone'
Select-String -LiteralPath "$run\Unity.log" -Pattern 'Concurrent validation','Editor verified'
```

Windows PowerShell 5.1 と PowerShell 7 で同じコマンドを使います。スクリプトはフォントごとの新しい `projects\<識別名>` に1ファイルだけ配置し、サイズと SHA256 を照合します。Unity 2019.4.24f1 が各プロジェクトに個別の `Library` を作成します。StandaloneWindows64 用の Dynamic Font を Include Font Data 有効、LZ4HC でビルドします。非圧縮は別の出力先を指定し `-Mode embedded-raw` を追加してください。

- `logs\<識別名>.log` にインポーター設定、依存関係、単独再ロード結果を記録します。各生成物とマニフェストを `build` に集約します。
- フォントをインポートしていない `verify` プロジェクトが全5件を同時に開きます。各Bundleで32 MiB上限、Dynamic Font 1件、別インスタンス、字形サンプルとアトラスを確認します。検証ログは出力先直下の `Unity.log` です。
- 成功すると `group-a` にNoto 3件、`group-b` にRoboto 2件を出力します。従来の `bundle-hashes.csv` は従来どおりです。`bundle-comparison.csv` には旧方式の実測容量との比較、ハッシュ、Font数、依存数、Editor検証結果が入ります。非圧縮は対応する旧実測値がないため、比較欄は空になります。
- 検証結果を共有する場合は `logs\*.log`、`Unity.log`、`bundle-hashes.csv`、`bundle-comparison.csv`、必要に応じて `build\*.manifest` とゲーム内の診断レポートを提出してください。異常終了時は該当するEditorログを提出してください。

各プロジェクトは出力先の中に残ります。作業用の Assets、Library、フォント、Bundle はGitに追加しないでください。従来の手動生成入口 `StoreFontBundleBuilder.Build` は比較用として利用できます。

## ゲーム内で検証する

開発版の DLL をまだ用意していない場合は、実機の参照 DLL を使って次のコマンドで作成します。出力先は生成済みバンドルとは別です。

```powershell
.\scripts\New-StoreReleaseDll.ps1 `
    -BepInExDir 'C:\XboxGames\Human Fall Flat\Content\BepInEx' `
    -OutputDirectory (Join-Path $run 'plugin')
```

1. ゲームを終了し、既存の HSRTimer DLL をゲーム外に控えて、生成した `HSRTimer-v0.1.3-pre.dll` を `BepInEx\plugins` 直下に置きます。同じプラグインの DLL は1件にします。
2. HSRTimer が実際に使用しているデータフォルダーの `fonts` 直下に、まず `group-a` の `.bundle` 3件をコピーします。通常は Documents 配下の `HSRTimer-MicrosoftStore\fonts` です。既存の `.bundle` がある場合は、ゲーム終了中に固有名の一時フォルダーに退避します。ファイル名の衝突がある場合は上書きせずに中断してください。コピー前後の SHA256 は `Get-FileHash -Algorithm SHA256` で比較できます。
3. ゲームを起動し、**Home → About → Font Diagnostics → Load local font bundles (file)** を選びます。Candidate で3種の名前、Bundle のファイル名、Instance ID、Dynamic 属性を確認します。
4. 候補ごとに標準 IMGUI と見比べます。14・18・22・26px、Normal / Bold、Noto JP Regular / Bold を比較します。英数字、符号付きタイム、日本語、中国語、記号がそろっているか見ます。Roboto と Roboto Mono では数字ごとの送り幅と小数点の桁揃えも見ます。Bold ファイルの字形と GUIStyle の Bold は別々に試します。
5. **I checked the candidate visually → Apply to Timer HUD for this session** を選びます。別の候補をプレビューしても HUD が変わらず、その候補を確認・適用すると HUD が切り替わることを確かめます。マップ移動後は **Record current status** で ID と保持数を比較します。復旧が起きた場合はログの復旧・再適用を確認します。
6. **Restore IMGUI / release probe fonts** で標準表示と保持数 0 を確認し、再読み込みを試します。グループ切り替え前にゲームを終了し、試験用の3ファイルをフォルダー外に戻して `group-b` の2件を追加します。試験後は試験用ファイルだけを戻し、退避していた既存ファイルを元の場所に戻します。
7. 各グループで **Export diagnostic report** を実行し、`font-diagnostics.txt` をグループ名付きで別途保存します。含まれる文字と実際に見えた文字は別々に記録します。必要に応じて第7段階の CSV を取り、HUD 適用前後の Font ID と Bundle 保持数を比較します。

## 実装上の制約

- ゲームでは同時4 Bundle、各32 MiB、1 Bundle あたり名前付きアセット32件までです。同じパスは大文字・小文字を区別せず管理します。Editor の生成検証にはゲーム側の同時4件制限を適用しません。
- 候補は Unity の Instance ID で識別します。同名の候補は Bundle ファイル名でも見分けられます。選択中の候補と HUD に適用中の候補を分けて保持します。無効化からの復旧は元のパスとアセット名を使います。明示的な解放は標準 IMGUI に戻り、復旧待ちも解除します。
- StoreLogic では異なる Bundle の同名候補、プレビューと適用の分離、HUD 切り替え、重複と5件目の拒否、解放、計測スナップショットをスタブで検証します。Unity Editor による生成、ゲーム内の複数ロード、文字表示、シーン移動後の寿命は Windows 検証手順の対象です。

## プレビューとHUDの描画差を調べる

元のHSRTimer（比較したソースは TwilightCup/HSRTimer の `9d245d668cc460064b1fa627e7780b46abde6766`）も、タイマー行と警告バナーに `FontStyle.Bold` を指定しています。Store版の診断プレビューは `Bold preview` で Normal と Bold を切り替えますが、従来のHUD適用ではFontとサイズだけを書き換え、行とバナーのBold指定を引き継いでいました。このため、Normalのプレビューを見た直後でもHUDはBoldで描画していました。今回は試験フォントの適用時にNormalを既定とし、比較用に従来のBoldを選べるようにしました。解除後の標準IMGUIは従来のBoldを使います。

タイマー行はリッチテキストを無効にしており、フォント描画の影や縁取りはありません。プレビューは文字列をまとめて描き、HUDは同じGUIStyleで1文字ずつCalcSizeし、その幅を丸めずに加算してグラデーションを描きます。列の開始位置は全文字列のCalcSizeから求めます。計測と描画に同じFont、FontStyle、サイズを使っていても、字間調整やCalcSizeの計測単位の違いで、全文字列の幅と文字別の幅の合計に差が出る可能性があります。タイマー文字用に独自のGUI倍率は設定していません。軸ガイド描画の行列はその描画直後に復元します。Store版の主な差は標準のOSフォント生成をスキンフォントで代用する点と、試験フォントを行・バナーに一時適用できる点です。文字単位描画と列の幅計算は本家由来です。

### 実機比較

1. 既存のBundleを使い、グループAでJP RegularとJP Boldを同じ18pxで選びます。各候補のNormalとBoldのプレビューを撮影します。
2. **HUD style on next apply** を **Normal (font's own weight)** に設定し、**I checked the candidate visually → Apply to Timer HUD for this session** を押します。各候補で **Record preview and HUD style comparison** を押します。同じ候補を **Legacy HUD (Bold)** でも適用します。プレビューのBold切り替えだけでも現在のHUDが変わらないことを確認します。
3. グループBでRoboto RegularとRoboto Monoでも比較します。ログの `Preview Normal` / `Preview Bold` と `HUD before` / `HUD after` を見比べます。HUDの記録は次の描画で追加されます。表示名・Instance ID・スタイル・サイズ・richText・GUI変換の線形部を確認し、文字列のCalcSizeと個々の文字幅の合計を比較します。
4. **Export diagnostic report** を使い、スクリーンショットと一緒に保存します。**Restore IMGUI / release probe fonts** 後は標準の見た目に戻ることを確認します。

Bold強制指定がプレビューとの条件差を生んでいたことはコードから確認できました。一方、同じNoto JPファミリー内でUnityがどのウェイトの実グリフを選んだかはFontのInstance IDだけでは判定できません。旧BundleでもNormalとBoldを比べ、差が残る場合は下記の1ファイルだけをインポートしたBundleでも比較します。

### JP系Bundleの容量増加

実機では一括生成時にJP RegularとJP Boldが相互依存し、同じプロジェクトで1件ずつビルドした場合は依存が消え、両方とも約30 MBになりました。これは別ウェイトが暗黙の依存としてBundleに含まれたという仮説と整合します。マニフェストの外部依存が空であることは、そのBundle内に別ウェイトのデータがないという証拠にはなりません。

検証するには、[単一フォントの生成手順](store-font-stage2.md)を使い、新規の試験プロジェクトを2つ作ります。一方の `Assets/Fonts` にJP Regularだけ、他方にJP Boldだけを配置します。両方とも `embedded` で生成し、個別ビルドのログ、出力サイズ、マニフェストを旧Bundleと比較します。当該フォントのインポート時の参照やビルドレポートの収録アセットを確認し、ゲームでは同名の旧Bundleと同時に配置せず、同一条件でウェイトの差を比較します。

### 取得できた旧JP Regularの実体

別ウェイトと混在していない初期のJP Regular（14,773,004 bytes、SHA256 `6069e139862ba348e653f3aabafb7b69401059c6c22f8a05462cc69b1933d2fc`）をUnityPyで調べました。シリアライズされていたのは `NotoSansCJKjp-Regular`、`Font Texture`、`Font Material` の各1件です。Fontの `m_FontData` は16,467,736 bytesで、元OTFのSHA256と一致しました。`m_FontNames` は `Noto Sans CJK JP`、`m_FallbackFonts` は空、付属マニフェストの外部依存は0件でした。

約30 MBになったJP Regular／Boldの実体は取得できていません。旧方式の相互依存と容量差から、同じファミリー名を持つ別ウェイトが暗黙の依存として各Bundle内に取り込まれた可能性があります。外部依存0件はBundle間参照の結果です。新方式のEditor検証はロード可能なFont数を数えます。シリアライズされた全フォントデータのバイト単位の比較が必要な場合は、新旧Bundleを同じ方法で解析します。

## 第9段階の完了記録

### Windows実機 — 生成と再ロード

生成スクリプトは `cc267fc4312b091f34dc40600a131dfdf403431b` に対し、Windows側で `SavedPercent` の `if` を `$()` で囲った状態で検証しました。同じ修正を正式に取り込みました。環境は Windows PowerShell 5.1.26100.8875、Unity Editor 2019.4.24f1、StandaloneWindows64、`embedded` です。

| フォント | 旧容量（bytes） | 新容量（bytes） | 削減（bytes） |
| --- | ---: | ---: | ---: |
| Noto Sans CJK JP Regular | 30,127,272 | 14,772,172 | 15,355,100（50.97%） |
| Noto Sans CJK JP Bold | 30,127,291 | 15,354,921 | 14,772,370（49.03%） |
| Noto Sans CJK SC Regular | 14,732,889 | 14,732,552 | 337 |
| Roboto Regular | 302,793 | 302,789 | 4 |
| Roboto Mono Regular | 84,507 | 84,546 | −39 |
| **合計** | **75,374,752（71.88 MiB）** | **45,246,980（43.15 MiB）** | **30,127,772** |

全5件の独立生成と単独ロードに成功しました。各BundleのFont数は1、外部Bundle依存は0、各32 MiB以下でした。別プロジェクトでの5件同時ロードにも成功し、Noto 3種はサンプル字形103/103、Roboto 2種は10/10、Editorは全6回正常終了しました。両CSVの容量とSHA256は一致しました。ハッシュの確認範囲はCSV相互の照合です。

初回はJP Regularの生成直後に止まったように見え、再実行では正常完了しました。原因は未特定です。PowerShellには各フォントの開始・検証完了、同時ロード検証、集約結果を表示するようにしました。

### Windows実機 — ゲーム内表示

環境は Human Fall Flat Microsoft Store v1696732、Unity 2019.4.24f1、BepInEx 6.0.0-be.793、.NET 6.0.7、HSRTimer 0.1.3-pre（DLLソース `1c7280cac9d396d66ae8f4e357646329605d982c`）です。

- Group A（Noto 3種）とGroup B（Roboto 2種）で読み込み、18pxのHUDにNormal／Legacy HUD (Bold)の両方を適用しました。左上のHUDでウェイトや文字間隔の違いと、Roboto Monoの等幅表示を目視確認しました。
- 標準IMGUIへの復帰と解放を確認しました。解放後の残存スロット、復旧待ちはいずれも0でした。両テストともError 0件、Warning 76件（従来の起動時警告）でした。
- ユーザーによるステージ移動とOne Key Retryでは一時適用フォントが維持されました。フォント消失を伴う自動復旧の実機検証は今回の範囲外です。

JP Regularの18pxにおけるHUDの `GUIStyle.CalcSize` 幅です。

| サンプル | 旧Normal | 旧Bold | 新Normal | 新Bold |
| --- | ---: | ---: | ---: | ---: |
| `0123456789` | 100 | 110 | 100 | 100 |
| `IIII WWWW` | 88 | 92 | 88 | 88 |
| `Game Time: 00:00.000` | 185 | 197 | 185 | 185 |
| `11:11.111` | 80 | 89 | 80 | 80 |
| `88:88.888` | 80 | 89 | 80 | 80 |

新JP RegularのBoldとNormalはこの表の全サンプルで同幅でした。全5種で `11:11.111` と `88:88.888` が同幅であり、今回調べた文字列では全文字列の幅と個別の文字幅の合計も一致しました。適用先は `HUD applied` のフォント名・Instance ID、計測は `HUD after row` 等で確認します。スクリーンショットの右側に写っているのはIMGUI comparisonです。Candidateはスクロール領域外にあり、画像からCandidateと左上HUDの視覚的一致は判定できません。

### 自動テストと残課題

構文だけを確認する場合は、対象とするPowerShellで以下を実行します。Unityは起動しません。

```powershell
powershell.exe -NoProfile -File .\tests\StoreFontBundles\Test-ScriptSyntax.ps1
pwsh -NoProfile -File .\tests\StoreFontBundles\Test-ScriptSyntax.ps1
```

PowerShell 7のモックで両モード、既存出力先の保護、入力ハッシュ不一致、Editor異常終了時のログ保持を検証しました。StoreLogic、Input Overlay連携スモーク、x64 Releaseビルドと版番号・GUID・埋め込みLICENSE検査も通過しました。実機の結果は上記embeddedの検証です。PowerShell 7による実Editor起動と、非圧縮Bundleの実生成はこの完了確認の対象外です。

約30 MBの旧Bundleの内部構成と実際の字形参照元は未確定です。今回計測したサンプルの一致は、全字形・全文字列の一致を保証しません。以上で第9段階の技術検証を完了とし、正式なフォント選択UI、設定保存、自動復元は第10段階に残します。
