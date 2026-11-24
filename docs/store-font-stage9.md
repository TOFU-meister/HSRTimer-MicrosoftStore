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
Select-String -LiteralPath "$run\Unity.log" -Pattern 'Editor opened','Editor Font','Editor verified'
```

このスクリプトはサイズと SHA256 を照合してから、StandaloneWindows64 向けに Dynamic Font、Include Font Data 有効、LZ4HC で作ります。非圧縮の場合は新しい出力フォルダーを指定し `-Mode embedded-raw` を追加します。出力先の `group-a` は Noto 3件、`group-b` は Roboto 2件です。Editor 検証では各バンドルの依存関係が空、アセット名が一意、想定の Font と dynamic 属性が取得できることを確認し、すべて同時に開けるか試してからアンロードします。従来の単一フォント向け手順も使用できます。

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
