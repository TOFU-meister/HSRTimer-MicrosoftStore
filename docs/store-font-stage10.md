# 第10-1段階 — HUDフォント設定の保存基盤

Store版の設定は通常 Documents 配下の `HSRTimer-MicrosoftStore` にあります。Documents を使用できない場合は既存の LocalAppData フォールバックを使います。HUDフォントの選択は `ConfigService` が読み書きする `settings.ini` の `[HudFont]` に保存します。サイズは Interface → Timer HUD の既存設定で、`layout.ini` の `[text]` にある `font_size` を使用します（初期値18、UI範囲8–72）。

```ini
[HudFont]
mode = external
bundle = notosanscjkjp-regular-embedded.bundle
asset = assets/fonts/notosanscjkjp-regular.otf
style = normal
```

`bundle` は `fonts` 直下のファイル名、`asset` はUnityが返すBundle内部の名前です。内部名のスラッシュは識別子の一部です。リセット後は `mode = imgui` となり、外部識別子が空になります。古い設定でセクションがない場合、不明なmode、無効な識別子は標準IMGUIになります。外部選択でstyleがない場合や認識できない場合はNormalです。構文上有効な選択はファイルが未配置でも保持します。第9段階の診断画面で選んだFontやサイズはそのセッション中だけ有効です。

## 第10-2段階の復元設計

1. 保存された選択を設定読み込み後に取得します。Unityオブジェクトの生成・切り替えはUnityのメインスレッドで行います。
2. 外部選択のときに限って、検証済みのファイル名を既存の `fonts` ルートに結合します。ルートと候補を `StoreDataPolicy.EnsureSafePath` で検査して、実際に開く直前にもreparse pointを拒否します。Unityが列挙した内部名と一致するFontを既存のBundleロード処理で選びます。
3. 読み込み済みのBundleスロットを再利用し、上限と所有権を既存のBundle管理に集約します。同じBundleを診断と復元が重複ロードしない設計にします。描画には既存のHUDサイズと保存されたNormal/Boldを使用します。
4. 未配置・不明なアセット・破損では標準IMGUIで起動し、保存された選択は残します。回復は操作または制限付きの既存の復旧処理で行い、毎フレーム試行しません。診断の一時適用を優先し、解除後は永続設定に基づく通常表示に戻す設計とします。持続的な標準フォントへの変更は明示リセットと既存の保存操作で行います。

## 第10-2段階の実装

設定読み込み後、永続HUDコンポーネントの最初のUpdateで1回だけ、メインスレッドから復元を試します。標準IMGUI選択ではBundleを開きません。タイマー行と警告バナーに外部Fontと保存したウェイトを適用し、サイズはその時点のレイアウトを使用します。実描画成功時は `Saved HUD font drawn` を記録します。

診断での一時適用が優先です。一時適用の解除では保存済みフォントのBundleスロットを残し、他の診断用リソースを解放します。保存と診断で同じBundleなら共通のスロットを使います。ゲーム終了時は共有スロットも解放します。シーン遷移とOne Key Retryで毎回ロードする処理は追加していません。生存監視による無効化後は既存の間隔と上限で再取得し、再取得が成功すれば保存したアセットに結び直します。

パスは `fonts` 直下のファイル名を正規化し、既存のパスポリシーでリンク・ジャンクションを拒否します。実際に開く前に再検証します。UnityのファイルパスAPIとヘッダー検査では同一ハンドルを共有できないため、別プロセスが検査とロードの間に差し替える競合までは排除できません。失敗時は標準IMGUIで続行し、有効な構文の保存値は保持します。

### Windowsでの確認

1. ゲームを終了します。起動ログの `Store data directory is writable` に出たデータフォルダーを使います。既存の `settings.ini` を上書きしない名前にバックアップします。PowerShellで `$ini` に対象のフルパスを設定し、`$backup = "$ini.stage10-2-$([guid]::NewGuid()).bak"`、`Copy-Item -LiteralPath $ini -Destination $backup -ErrorAction Stop` を実行します。初回でINIがなければ、標準設定で一度起動し設定を保存してからバックアップします。
2. 第9段階で独立生成したBundleを `fonts` に置きます。既存の `[HudFont]` セクションだけを上の例で更新します。セクションがなければ追記します。サイズを変える場合は既存HUDのサイズ設定を使います。
3. ゲームを起動し、`Saved HUD font bundle ... ready` と `Saved HUD font drawn` のウェイト・サイズ、表示を確認します。ゲーム終了後に `style = bold` に変更して再確認します。ステージ移動・One Key Retryではフォントが維持され、起動時ロードが繰り返されないことを確認します。
4. 診断で異なるフォントへ一時適用し、**Restore saved HUD font / release probe fonts** で保存済みフォントに戻ることを確認します。診断に同じBundleを読み込ませた場合は既存スロットが使われます。
5. ゲーム終了中に `bundle = missing-test.bundle` または `asset = assets/fonts/missing-test.otf` として起動します。標準IMGUIで表示され、保存値が残ることを確認します。破損を試す場合は、元Bundleとは別名の試験ファイルを使います。
6. 終了後、試験中のINIも必要なら別名に控えてから、`Copy-Item -LiteralPath $backup -Destination $ini -ErrorAction Stop` で復元します。試験中に追加した他の設定を残したい場合は、バックアップから `[HudFont]` セクションだけを戻します。

## 第10-2段階のWindows実機結果

ユーザーの実機検証では、Noto Sans CJK JP Regularを起動時に復元し、Normal／Boldと既存HUDサイズ24pxを適用できました。ステージ移動とOne Key Retry後の維持、Bundle欠落時の標準IMGUIへの復帰と保存値の維持も報告されています。ログで診断の一時適用を確認したのは保存済みと同一のJP Regular（Normal、18px）です。解除後に保存済みBold、24pxに戻り、同一Bundleは重複ロードされませんでした。

新規ログはError 0件、Warning各76件でした。最終状態はBundleスロット1件、復旧待ち0件で、Font、Material、Textureはliveでした。Windowsのジャンクション拒否、破損Bundleの実ロード、実際のFont消失に伴う自動復旧は未検証です。

## 第10-3段階 — 正式なフォント設定

**Interface → Timer HUD → HUDフォント** を開きます。フォント一覧には、安全な `fonts` 直下のBundleファイル名が表示されます。選択したBundleだけをロードし、利用できるDynamic Fontの候補を出します。複数のFontが入っている場合は、Bundle内フォントのボタンで切り替えます。NotoとRobotoの5件を同時にフォルダーに置けます。ロード上限は4件です。診断で上限を使い切っていた場合は、診断画面から未使用Bundleを解放して再試行してください。

Normal／Bold、3行のプレビューを確認し、**適用して保存** を押します。Fontとパスを検証して `settings.ini` の書き込みが成功した後に、実際のHUDと診断の一時上書きを切り替えます。保存に失敗した場合は従来の設定値とHUDを維持します。**標準（IMGUI）→ 適用して保存** で外部指定を消去します。**フォントの編集を取り消す**、画面を閉じる、他のページへ移動する、のいずれでも未保存のフォントとウェイトを破棄します。サイズスライダーは従来どおり即時反映し、通常の設定保存時に `layout.ini` に保存します。プレビューには8～26pxの範囲で反映します。

**fontsフォルダーを開く** と **フォント一覧を再読み込み** でBundleを追加できます。欠落した保存済みBundleは保存値を残し、現在の表示と問題を併記します。ファイルを戻して一覧を再読み込みし、再適用できます。診断の一時適用は永続設定から独立し、正式な保存が成功した時点で解除します。

### 第10-3段階の実機確認

1. 第9段階のBundleを5件とも `fonts` に置きます。Interface → Timer HUDで一覧を開き、5件を順に選択し、プレビューが入れ替わってもHUDは保存操作まで変わらないことを確認します。
2. 5件目でNormal／Boldを選び保存し、HUDと再起動後の表示を確認します。別の選択で編集取り消し、ページ移動、閉じるを試し、保存済み設定が維持されることを確認します。標準に戻して保存し、再起動します。
3. テスト専用コピーを欠落・破損状態にして、状態表示とIMGUIフォールバックを確認します。実ファイルを戻して再読み込み・再適用します。Aboutで一時適用した後、正式UIから保存して診断の上書きが解除されることを確認します。
4. 日本語、英語、簡体字中国語の表記と一覧・プレビューの収まり、フォルダーを開く動作を確認します。

### 第10-3段階のWindows実機結果

ユーザー報告では、正式設定の表示、5件の一覧・選択、Normal／Boldとプレビュー、編集とHUDの分離、取り消し・ページ移動、保存・再起動復元、標準へのリセット、診断との共存、フォルダーを開く操作・再読込、3言語表示の10項目が通過しました。標準IMGUIの3行プレビューはスクリーンショットで重ならずに表示されています。

| 正式HUD（24px） | Normalのログ | Boldのログ |
| --- | --- | --- |
| Noto Sans CJK JP Bold | あり | あり |
| Noto Sans CJK JP Regular | あり | あり |
| Noto Sans CJK SC Regular | あり | あり |
| Roboto Regular | あり | 直接記録なし |
| Roboto Mono Regular | あり | あり |

診断でJP Bold（Normal、18px）を一時適用し、正式UIからRoboto Regular（Normal、24px）を保存すると診断の上書きが解除され、再起動後もRoboto Regularが復元されました。再起動前後ともError 0件、Warning 76件です。診断でのロード後はスロット4、再起動後は1件で、復旧待ちはいずれも0。再起動後のFont／Material／Texture／Bundleはliveでした。個々の選択時のスロット数は記録されていないため、プレビュー専用スロット解放の根拠は自動テストです。通常利用の実機検証は以上で完了です。

## 第11-1段階 — 耐障害性点検

- 適用はフォント・パス検証から始まり、既存の原子的なINI書き込みの成功後に管理状態を切り替えます。書き込み失敗は従来値を維持します。保存後にUnityのスタイル参照解除が失敗した場合は、保存成功として扱い、旧Bundleを両HUDスタイルの正常な再設定まで保持します。保持中も4件上限に含みます。新Fontが無効なら標準IMGUIへフォールバックし、UIは保存値と実際の描画状態を別々に示します。
- 診断がロードした候補には保持フラグを持たせ、正式HUDの変更・リセットで共有Bundleが解放される問題を修正しました。診断の明示解放はそのフラグを外し、保存済みで使用中のBundleは残します。回復待ちの旧保存Bundleは、他の所有者がいなければリセット時に取り除きます。
- 一覧はオープン時と再読み込み時に走査し、ロードは選択・操作・上限付き復旧のときだけです。プレビューのGUIStyleは再利用し、描画後にFont参照を外します。既存の500 ms間隔の生存監視、2秒の待機と3回の復旧上限を維持しています。
- ファイルの検出後、ロード前に消えた場合は該当する候補を利用不可として扱います。リンクを含まないことはパス検査の時点で確認しています。ファイル名で呼ぶUnity APIはヘッダー検査と同一ハンドルを使わないため、検査後の差し替えには競合の余地があります。

### Windows追加試験（PowerShell 5.1）

ゲームを終了し、起動ログが示す実データディレクトリを以下の `$data` に設定します。既存Bundleを変更せず、試験専用のファイル名を使います。

```powershell
$data = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'HSRTimer-MicrosoftStore'
$ini = Join-Path $data 'settings.ini'
$fonts = Join-Path $data 'fonts'
$id = [guid]::NewGuid().ToString('N')
$backup = "$ini.stage11-$id.bak"
Copy-Item -LiteralPath $ini -Destination $backup -ErrorAction Stop
$bad = Join-Path $fonts "stage11-corrupt-$id.bundle"
[IO.File]::WriteAllBytes($bad, [byte[]](85, 110, 105, 116, 121, 70, 83, 0, 0, 0))
```

1. 正常フォント表示中に正式UIで一覧を再読込し、試験用破損Bundleを選びます。読み込み失敗を表示し、以前の選択とHUDが維持されることを確認します。
2. 正常Bundleから固有名の試験コピーを作成して保存します。ゲームを終了して、そのコピーだけをフォルダー外に移動します。再起動時に標準IMGUIと欠落表示になることを確認します。試験コピーを戻し、再読込と再適用を実行します。
3. ジャンクションの試験では、以下で試験名のリンクを作ります。終了中に `[HudFont]` の `bundle` だけを試験リンクのファイル名に変更し、選択が拒否され標準IMGUIで起動することを確認します。

```powershell
$target = Join-Path $env:TEMP "hsrtimer-stage11-$id"
New-Item -ItemType Directory -Path $target -ErrorAction Stop | Out-Null
$link = Join-Path $fonts "stage11-link-$id.bundle"
New-Item -ItemType Junction -Path $link -Target $target -ErrorAction Stop | Out-Null
```

4. 形式が正常な5件を正式UIで連続選択し、別ページに移動します。診断で候補をロードした後、共有Bundleを正式適用し、別フォントへの切り替え・標準リセット後も診断候補が利用できることを確認します。診断の明示解放と `Record current status` でスロットの変化を確認します。
5. ゲームを終了します。以下でINIを復元し、作成した試験ファイルとリンクのみ片付けます。ステップ2の試験コピーも作成したものだけを除きます。

```powershell
Copy-Item -LiteralPath $backup -Destination $ini -Force -ErrorAction Stop
if (Test-Path -LiteralPath $bad) { Remove-Item -LiteralPath $bad -ErrorAction Stop }
if ($link -and (Test-Path -LiteralPath $link)) {
    $entry = Get-Item -LiteralPath $link -Force
    if ($entry.LinkType -ne 'Junction') { throw 'The test path is no longer a junction.' }
    cmd.exe /c ('rmdir "{0}"' -f $link)
    if ($LASTEXITCODE -ne 0) { throw 'Could not remove the test junction.' }
}
if ($target -and (Test-Path -LiteralPath $target)) { Remove-Item -LiteralPath $target -ErrorAction Stop }
```

実際にUnityのFontが消失する故障、スタイル参照解除例外、Windowsのジャンクション、破損ファイルのネイティブロードは第11-1段階の実機確認に残ります。故障を注入する一般向けUIはありません。

### 第11-1段階のWindows実機結果

ユーザーの実機報告では、外部フォント5種のNormal／Bold描画、Bundle共有と解放、破損Bundleの拒否、欠落Bundleを戻した後の再適用、ディレクトリジャンクションの拒否が通過しました。Windows PowerShell 5.1.26100.8875で生成スクリプトの構文検査も通過し、試験後のINI復元と試験Bundle削除も完了しています。UnityのFontを実際に消失させる試験と、ネイティブGUIStyle参照解除例外は引き続き自動テストの障害注入による検証です。パス検査からUnityのロードまでの差し替え競合の制約は上記のとおりです。

## 第11-2段階 — 診断モード（開発者向け）

ゲーム終了中に、起動ログが示すデータディレクトリの `settings.ini` をバックアップし、以下のセクションを追加または更新します。

```ini
[Debug]
EnableDebugMode=true
```

標準は `false` です。項目がない場合や不正値の場合もOFFになります。有効化・無効化は次回ゲーム起動時に反映します。Release DLLで利用できます。設定画面に有効化の操作はありません。

| 区分 | OFF時の動作 |
| --- | --- |
| AboutのFont Diagnostics（診断Bundle、OSフォント調査、一時適用、詳細状態、出力） | セクション全体を非表示 |
| Aboutのパフォーマンス計測・CSV出力 | 非表示、収集を開始しない |
| Subsegmentのデバッグログ操作、MarkersとSubsegmentの詳細ログ | GUI操作を非表示にし、詳細ログを停止。各機能の既存フラグはINIに保持 |
| 診断の履歴、HUD描画成功の詳細、復旧ロード時のUnityログ取り込み | 停止 |
| 正式HUDフォント設定、起動時復元、Bundle所有権・監視・復旧、通常のログ | 従来どおり |
| ゲーム内のShellコマンド、Aboutの更新確認・データフォルダー | 従来どおり。Shellには診断のほかリロードやマーカー編集が混在 |

Windowsで新DLLを起動し、OFFではAboutとSubsegmentに診断操作がなく、保存済みフォントが描画されることを確認します。ゲーム終了後にONへ切り替え、再起動して診断・計測操作が使えることを確認します。最後にOFFへ戻し、再起動して通常表示に戻ります。正式フォントを切り替えて保存する場合は、`[Debug]` の指定が維持されることも確認します。Bundleの再生成は不要です。

### 第11-2段階のWindows実機結果

OFF → ON → OFFを確認しました。OFF時は通常GUIに診断欄が現れず、保存済み外部フォントの復元とHUD描画は正常でした。ON時はフォント診断、パフォーマンス計測、CSV出力を確認しました。保存された起動ログ2件は各Error 0件、既存のIl2CppInterop Warning各76件でした。初回OFFはユーザーによる目視確認です。TwilightInputOverlay v0.1.2-preとの併用も正常でした。

## 第11-3段階 — フォントパック作成（配布担当者向け）

第9段階の**独立生成済みembedded出力**を使います。入力には `group-a`、`group-b`、同時生成された `bundle-hashes.csv` が必要です。`tools/StoreFontPack/verified-bundles.csv` は独立生成版の実機比較記録から固定した5件の容量とSHA256です。旧版と非圧縮版は検証を通りません。5件合計は45,246,980 bytesです。

```powershell
Set-Location C:\HSRTimer-MicrosoftStore
$run = 'C:\path\to\the\verified-stage9-run'
$out = Join-Path $PWD 'fontpack-output'
New-Item -ItemType Directory -Path $out -Force | Out-Null
powershell.exe -NoProfile -File .\scripts\New-StoreFontPack.ps1 -BundleRunDirectory $run -OutputDirectory $out
```

PowerShell 7では最後の行で `pwsh` を使えます。パッケージ工程はオフラインで実行でき、Unityは不要です。入力CSV・実ファイルを固定値と照合し、グループ内の余分なファイルやリンクを拒否します。ZIP内の名前とハッシュを再検証してから `HSRTimer-MicrosoftStore-FontPack-v1.0.zip` として確定し、最終ZIPのSHA256を表示します。同名ZIPが既にある場合は停止します。

ZIPに収録するのは `fonts/` の5件、3言語の導入説明、原文ライセンスと著作権表示、`FONT-SOURCES.csv`、`SHA256SUMS.txt` のみです。入力ディレクトリ直下のUnity作業物やログは収録しません。生成ZIPとBundleはGitに追加しないでください。

Noto Sans CJKとRoboto MonoはOFL 1.1、Roboto Regularは取得元タグv2.138のApache 2.0です。固定済みフォントファイルのnameテーブルから著作権表示を取り、取得元と一致するライセンス原文を収録しています。Roboto取得元のタグのファイル一覧で別の `NOTICE` または `NOTICE.txt` は確認されませんでした。ZIPに含める `FONT-SOURCES.csv` のURLに取得元タグ／コミットを記録します。フォント本体の改変や文字削減は行わず、検証済みUnity Bundleをそのまま収録します。

構文と模擬データでの回帰検査は `pwsh -NoProfile -File .\tests\StoreFontBundles\Test-StoreFontPack.ps1` で実行できます。模擬用のハッシュはテストが作る一時コピーの中だけで使います。

### 第11-3段階のWindows実機結果

`HSRTimer-MicrosoftStore-FontPack-v1.0.zip` は42,472,033 bytes（40.50 MiB）、Bundle 5種類、ZIP内14件です。報告されたSHA256は `AF74D061D6FBA56130B41C33CDF1BFAC04FD5F5E6873CCDCDBB2BD83A8A56B8C` です。容量削減前のBundleは固定表との不一致で拒否され、独立生成版からZIPを作成して内部ファイルのSHA256再検証まで成功しました。

Windows PowerShellの回帰結果は `Font pack tests passed on PowerShell 5.1.26100.8875.` です。パッケージのシンボリックリンク拒否テストはLinuxのPowerShell 7で実行したもので、Windowsでの実行結果には含めません。

### 公開タグとの差分

`v0.1.2-pre`（`fd51da989aa9fd219df356c33a19b4fcc3a91e72`）から第11-3段階の `000faf38b26199ade252550b39b849fd455089f5` までの19コミットを比較しました。数値マップIDの解決、外部フォントの選択・保存・復元、Bundle共有と復旧、診断モードの分離、および任意フォントパック用スクリプトが含まれます。Automatic PB Backupの設定・既定OFF・既存PBの更新時バックアップは公開タグにもあります。今回の差分で新設した機能としては扱いません。[GitHubの差分](https://github.com/TOFU-meister/HSRTimer-MicrosoftStore/compare/v0.1.2-pre...000faf38b26199ade252550b39b849fd455089f5)

[公開前チェック](releases/VERIFY-v0.1.3-pre.md)
