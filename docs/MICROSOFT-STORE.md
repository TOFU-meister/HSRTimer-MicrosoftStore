# HSRTimer の Microsoft Store 版プレビュー

移植元はフォークの `dev`、コミット `0eead2f51b9e322f719669e26c8b33744141e61b` です。調査時点の TwilightCup/HSRTimer の `dev` も同じコミットでした。対象は Human: Fall Flat Microsoft Store 版 Windows x64、Unity 2019.4.24f1、BepInEx 6.0.0-be.793、.NET 6 です。

## ビルド

ゲームを BepInEx 6 の IL2CPP 版で一度起動して `BepInEx/interop` を生成します。ゲームと同じインストールの `core` と `interop` を使用します。`BepInExDir` にはその親フォルダーを指定します。このビルドは .NET 6 SDK を使用します。

```powershell
$BepInExDir = 'C:\Path\To\HumanFallFlat\BepInEx'
dotnet build .\src\HSRTimer\HSRTimer.csproj -c Release -p:StoreIl2Cpp=true "-p:BepInExDir=$BepInExDir"
dotnet run --project .\tests\StoreLogic\StoreLogic.csproj -c Release
```

出力は `src/HSRTimer/bin/store/Release/net6.0/HSRTimer.dll` です。DLL を配布する場合は同梱の `LICENSE` も含めます。参照用のゲーム DLL と BepInEx DLL は出力に含まれません。既定で PB 保存が有効です。PB 無効版が必要な場合は `-p:StorePreview=true` を指定します。

## Input Overlay の設定タブ

HSRTimer Microsoft Store `0.1.2-pre` と TwilightInputOverlay Microsoft Store
`0.1.1-pre` を同時に導入すると、HSRTimer の設定に「Input Overlay」
タブが追加されます。Overlay の設定とプリセットは Overlay の Documents
フォルダーへ保存されます。

外部プラグインは `SettingsPanelTabRegistry.Instance.RegisterCallbacks` に
.NET のデリゲートを渡して登録できます。既存の `ISettingsPanelTab` と
`ILocalizableSettingsPanelTab` も引き続き使用できます。登録は Unity
スレッドで行い、デリゲートはゲームプロセスの終了まで有効です。
描画コールバックは、例外時にも自分が開いた IMGUI グループを閉じてください。

## 配布用 DLL を作成する

Windows x64 でゲームを起動して生成された `BepInEx/interop` と、同じインストールの `BepInEx/core` を使います。リポジトリ直下で次を実行します。

```powershell
$BepInExDir = 'C:\Path\To\HumanFallFlat\BepInEx'
.\scripts\New-StoreReleaseDll.ps1 -BepInExDir $BepInExDir -OutputDirectory .\release
```

この開発版では `HSRTimer-v0.1.2-pre.dll` が生成されます。スクリプトは x64、アセンブリ名、プラグイン GUID、BepInEx の版番号、製品バージョン、LICENSE の埋め込みを確認します。配布先に出力するのは検証済みの DLL だけです。

公開時は DLL を Release に添付し、著作権表示を含む [LICENSE](../LICENSE) の全文を Release 本文に掲載するか、別の `LICENSE` アセットとして添付します。DLL にも同じライセンス全文が埋め込まれています。対象コミットのタグは `v<version>` を使用し、Release は **Pre-release** に設定します。

カスタムテキストの `{version}` はゲーム版（例 `v1096141`）、`{pluginversion}` は HSRTimer 版（例 `0.1.2-pre`）です。従来のレイアウトでプラグイン版を表示したい場合は `{pluginversion}` に変更します。

## 機能の状態

| 機能 | このブランチの状態 | Windows で確認する内容 |
| --- | --- | --- |
| 起動、Unity コールバック | BepInEx 6 の永続コンポーネントホストを使用。IL2CPP 用のコンストラクターを追加 | 起動とシーン遷移後の `Update`、`FixedUpdate`、`OnGUI` |
| LRT、RT、Wake Up、セグメント、フレーム表示 | `dev` の `GameClock`、`TimerCore`、`RunState` を使用。Store 版の `AfterLoad(int,int)` と `Fall(HumanBase,bool)` に接続 | 完走、失敗した落下、ロード、ポーズ、再開、リセットと一キーリトライ。Full Game、IL、各 Co-op ロールでの境界 |
| HUD、設定、ローカライズ、プリセット | HUD、IMGUI、設定タブ、言語、レイアウトを維持。Store プレビューの設定は専用ディレクトリへ保存 | Home、ドラッグ、複数列、カスタムテキスト、設定の再起動後の反映、進捗表示の中央寄せ |
| カテゴリ、タグ、無効判定 | `dev` のルールを保持。Jumpless のローカル操作判定にネイティブオブジェクトの同一性を使用 | Checkpoint、NoCheckpoint、Jumpless、Voiceline、Glitchless、NoEC、Co-op とチート検出 |
| Subsegment と PB 参照 | サンプリングと比較を維持。JSON/JSONL は .NET 側で読み書きする形式に移植。IL・複数ステージ PB を既定で保存 | 旧形式の参照の読み込み、比較、Leaderboard 表示、キャンペーン進行 |
| Markers、PB、プリセット | マーカーの定義・評価・表示を維持。PB を既定で保存。プリセットの保存・復元はレイアウトのみ | チェックポイント、プレーン、球、オブジェクトへの追従、複数プレイヤー。より速い有効な記録だけで自動更新されること |
| ゲーム内コンソール | IL2CPP デリゲートで `hsr` と `hsrtimer` を登録 | コマンド登録、`hsr clock`、`hsr status`、コンソールが開いている間の入力 |
| 外部プラグイン連携 | タグと設定タブの拡張 API、LevelCollections の既存連携を保持 | Microsoft Store に対応した外部プラグインを入れた場合の登録、コレクション進行とリトライ |
| アップデーター | リリース情報の確認コードを維持。Store ビルドの自動インストールは専用リリース配布元が必要 | リリース情報を表示できること。Mono 版 DLL を Store 環境へ適用しないこと |

ゲーム側の登録・動作はこのプレビューで実機未検証です。Phase 5.51 で確認したのは診断コンポーネントの起動、`FixedUpdate`、`AfterLoad` と `Fall` のフック、およびゲーム状態の観測です。それらの結果はプレビュー本体の実機テストとは別です。

## 初回の実機テスト

1. ゲームを終了し、従来の `BepInEx/config/HSRTimer` と過去の検証版設定・記録をバックアップします。既存プラグインと診断 DLL を `BepInEx/plugins` の外へ一時移動します。
2. 実機の `core` と `interop` で Release ビルドします。生成した `HSRTimer.dll` または配布名の `HSRTimer-v<version>.dll` を1個だけ `BepInEx/plugins/` へ置きます。言語ファイルは DLL に埋め込まれた英語・簡体字中国語で起動できます。
3. ゲームを起動し、`BepInEx/LogOutput.log` で `HSRTimer Microsoft Store` のロード、`PB writes: True` と `Store data directory is writable`、`GameAfterLoadBoundaryPatch` と `GameFallBoundaryPatch` のインストールを確認します。失敗時はログ全文を保存します。Home で設定を開き、HUD の変更を確認します。
4. Mansion で通常の落下、ポーズと再開、チェックポイントの再読み込み、通常完走を行います。コンソールで `hsr clock status` と `hsr clock history 30` を採取します。入力を再現できる条件で複数回完走し、ロードの `load`、終了の `pass`、セグメントの確定値を比較します。同じ物理ステップに複数回 `Fall` が来ても終了値が更新されないことを確認します。フレーム表示でも試し、ポーズ中に LRT は静止し RT は進むことを確認します。
5. Home を閉じて設定を保存し、再起動後に HUD の文字サイズと有効なタグが復元されることを確認します。
6. Subsegment を有効にして有効な Mansion (IL) を完走します。最初の完走でファイルが生成され、遅い試行で変わらず、より速い試行だけで更新されることを確認します。別のタグで完走して別ディレクトリに保存されること、再起動後の同じカテゴリで PB 比較が表示されることも確認します。
7. キャンペーンを Mansion から進め、Aztec などの区切りで `ML` の記録を確認します。定義したマーカーを通過する有効な IL では `pb` と `pbTimes` を確認します。Steam 側の設定・記録が心配な場合は、実機テストの前後でファイルハッシュを比較できます。

## PB の形式と保存先

| データ | Store 版の既定パス（ユーザーデータルートからの相対パス） |
| --- | --- |
| 設定・レイアウト・タグ | `settings.ini`、`layout.ini`、`tags.ini` |
| IL | `subsegment/pb/IL/<level>/<category>/meta.json` と `sample.jsonl` |
| Full Game | `subsegment/pb/ML/<project>/<category>/meta.json` と `levels/*.jsonl` |
| Marker PB | `markers/<level>/<category>.json` |

保存先は `<Windows のドキュメント>/HSRTimer-MicrosoftStore` です。Windows の既知フォルダーを取得し、起動時に一時ファイルへの書き込みを試します。OneDrive へ移動されたドキュメントも、Windows が返す実際の場所が書き込み可能で再解析ポイントを経由しなければ使えます。ドキュメントが見つからない、安全性を確認できない、または書き込めない場合は `%LOCALAPPDATA%\HSRTimer-MicrosoftStore` を使います。WindowsApps は書き込み先の対象外です。

**Home → About → Open Data Folder** で現在の保存先をエクスプローラーで開けます。開けない場合は About にエラーが表示されます。起動ログの `Store data directory:` でも実際の保存先を確認できます。外部の Subsegment 参照用 `LoadPath` は読み取り専用です。

### LocalAppData の設定・記録を引き継ぐ場合

1. ゲームを終了し、LocalAppData とドキュメント の両方の `HSRTimer-MicrosoftStore` フォルダーを別の場所にバックアップします。
2. LocalAppData 側から、引き継ぎたい INI、`lang`、`presets`、`markers`、`subsegment/pb` をコピーします。IL／ML の PB は、`meta.json` と対応するサンプルを含むカテゴリフォルダーごとコピーします。
3. コピー先に同名ファイルや同じ PB カテゴリがある場合は、両方をバックアップに保持したまま、どちらの一式を使うか確認してから作業します。個別の JSONL と `meta.json` を異なる記録から混ぜないでください。
4. 起動ログで ドキュメントが選ばれたことを確認し、設定と PB 比較を確認します。

自動コピーや自動上書きはありません。保存先を切り替えた直後に別の設定や PB が表示される場合は、起動ログのパスを確認してください。フォールバック実機テストは、書き込み可能な通常環境と、ドキュメントの Store 専用データフォルダーに書き込めないテスト用環境で行います。後者ではログに失敗理由と LocalAppData 側のパスが表示されることを確認します。

分類は `dev` と同じで、タグなしは `Any`、有効タグは序数順に並べて `+` で連結します。ファイル名には従来のサニタイズ規則が適用されます。Full Game は `Aztec%`、`Dark%`、`Steam%`、`Any%` の区切りで更新されます。無効な試行やシミュレーションでは記録せず、同タイム・遅いタイムは据え置きます。Marker PB の自動更新は従来どおりソロ／ホスト側で行います。

Store 版の書き込み先はユーザーデータルート配下に制限します。PB／マーカーの絶対パスやフォルダー外への相対パスを指定した場合は既定の相対パスを使います。保存時にリンクやジャンクションをたどるパスは拒否します。既存記録を更新すると、Subsegment は `.previous`（古い世代は `.backup-*`）、Markers は `.bak-*` を残します。破損・未完の既存 PB は上書きせず、ログにパスを記録します。Store 版では記録の意図しない置き換えを避けるため、プリセットの適用はレイアウトのみです。

## 今回の検証

- BepInEx be.793 と入手済みの Store 版 interop（ゲーム、HumanAPI、CoreModule、IMGUI、TextRendering、UI）を使い、未入手のモジュールを Unity 2019.4.24 用の参照および補助生成した `Il2Cppmscorlib` で補った .NET 6 Release コンパイルは成功しました。この補助参照版の DLL は Windows のテスト用には使用できません。実機生成済みの `BepInEx/interop` 一式でのビルド結果は未確認です。
- 自動テストでは、物理ステップ内での Tick の共有、`FixedUpdate` より前と後に終了境界が来た場合の同値性、重複した終了通知、リトライ中と待機中の分岐、開始時点とポーズ時間のスナップショット、PB の JSON/JSONL 互換読み込み、PB の初回保存・更新・同タイム／遅いタイムの維持、タグ分離、前世代からの復旧、破損データと書き込み失敗時の保護を検証します。Unity のネイティブフックの発火順と描画は Windows 側のテスト項目です。
