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
Select-String -LiteralPath "$run\Unity.log" -Pattern 'dependencies for','Imported','Editor Font','Editor verified'
```

このスクリプトはサイズと SHA256 を照合してから、StandaloneWindows64 向けに Dynamic Font、Include Font Data 有効、LZ4HC で作ります。非圧縮の場合は新しい出力フォルダーを指定し `-Mode embedded-raw` を追加します。出力先の `group-a` は Noto 3件、`group-b` は Roboto 2件です。Editor 検証では各バンドルの依存関係が空、アセット名が一意、想定の Font と dynamic 属性が取得できることを確認し、すべて同時に開けるか試してからアンロードします。従来の単一フォント向け手順も使用できます。

Windows PowerShell 5.1 と PowerShell 7 で同じコマンドを使います。ダウンロード前に JSON の5件と各 `file` が文字列であることを検証します。Unity の起動時に表示されるログの場所は `$run\Unity.log` です。

初回の Windows ログでは、5件のインポートとビルドに成功した後、JP Bold の外部依存チェックで停止しました。そのログからは依存先や参照元のアセットを特定できません。今回のビルドは次の順で検証します。

- `build\combined-diagnostics` に比較用の一括ビルドのマニフェストを残し、ログに全バンドルの直接依存と間接依存のみの一覧を記録します。インポートしたサブアセット、フォント名指定、参照アセットのパスと、ロードした Font の Material・アトラスも記録します。
- `build\individual` で、固有のバンドル名とアセットパスを使い、フォント1件ずつビルドします。個別マニフェストに外部依存がある場合は、依存先を記録して停止します。
- 個別に開いてアンロードした後、全件を同時に開き直します。各段階で想定したアセット名と Dynamic 属性を照合します。成功した個別ビルドだけが `group-a` と `group-b` にコピーされます。

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
