# HSRTimer Microsoft Store

Windows x64 の **Human: Fall Flat（Microsoft Store／Xbox PC アプリ版）** 向けの非公式移植です。ゲーム時間の自動計測、Timer HUD、タグ別の PB、サブセグメント比較、マーカーを利用できます。

テスト版のバージョンは **0.1.0-pre** です。[テスト版の導入説明](docs/releases/README-v0.1.0-pre.txt)

## 対応環境

- Windows x64、Human: Fall Flat の Unity 2019.4.24f1 IL2CPP 版
- [BepInEx 6.0.0-be.793 IL2CPP Windows x64（dbc82f0）](https://builds.bepinex.dev/projects/bepinex_be/793/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.793%2Bdbc82f0.zip)
- プラグインの対象ランタイムは .NET 6。ソースからビルドする場合は .NET 6 SDK が必要です。

## インストールと更新

1. BepInEx をゲームの実行ファイルがあるフォルダーへ展開し、ゲームを一度起動してから終了します。
2. [Store 版の Releases](https://github.com/TOFU-meister/HSRTimer-MicrosoftStore/releases) から Store 版の配布物を取得します。配布物がまだない場合は、[実機の参照 DLL を使ったビルド手順](docs/MICROSOFT-STORE.md#ビルド)を参照してください。
3. ゲームを終了した状態で、`HSRTimer.dll` を `BepInEx/plugins/HSRTimer-MicrosoftStore/` に置きます。言語ファイルが同梱されている場合は、`lang` フォルダーを DLL の隣に置けます。
4. 更新時は同じ場所の DLL を差し替えます。以前の HSRTimer DLL が `BepInEx/plugins` 配下の別フォルダーにある場合は、そのフォルダーの外に移動し、読み込まれる HSRTimer DLL が1個になるようにします。
5. 起動後、**Home** キーで設定画面を開きます。About の更新確認はこの専用リポジトリの安定版を案内します。DLL の差し替えは手動です。

### BepInEx コンソールを非表示にする

ゲームを終了し、初回起動で生成された `BepInEx/config/BepInEx.cfg` の既存の `[Logging.Console]` セクションで `Enabled = false` に設定します。調査用ログは `BepInEx/LogOutput.log` を確認してください。[BepInEx のコンソール設定](https://docs.bepinex.dev/articles/user_guide/troubleshooting.html#enable-console)

## 設定と PB

保存先は既定で **`<Windows のドキュメント>/HSRTimer-MicrosoftStore/`** です。Windows が返すドキュメントの場所を使うため、OneDrive 側に移動している場合も、その場所が安全かつ書き込み可能なら使います。フォルダーが見つからない、書き込みできない、または再解析ポイントを経由する場合は **`%LOCALAPPDATA%\HSRTimer-MicrosoftStore`** に切り替わります。

ゲーム内では **Home → About → Open Data Folder** で、今使っている保存先をエクスプローラーで開けます。

| データ | 保存先からの相対パス |
| --- | --- |
| 設定・レイアウト・タグ | `settings.ini`、`layout.ini`、`tags.ini` |
| 言語・プリセット | `lang/`、`presets/` |
| IL・Full Game PB | `subsegment/pb/IL/`、`subsegment/pb/ML/` |
| マーカー・Marker PB | `markers/` |

旧 LocalAppData の設定や PB を引き継ぐには、ゲームを終了し、旧・新フォルダーをバックアップしてから、新しい場所へ必要なファイルをコピーします。PB は `meta.json` とサンプルを含むカテゴリフォルダーごとコピーし、同名データがある場合はどちらを使うか確認してから移してください。[手動移行の詳しい手順](docs/MICROSOFT-STORE.md#localappdata-の設定記録を引き継ぐ場合)

## 既知の制限

- Store 版プリセットはレイアウトのみを保存・復元します。
- 設定欄はクリックで編集し、Enter で確定、Esc で取り消します。日本語 IME の直接入力は未検証です。
- Steam Workshop 連携や外部プラグインには Store 対応が必要です。

不具合は [このリポジトリの Issues](https://github.com/TOFU-meister/HSRTimer-MicrosoftStore/issues) に、ゲームとプラグインのバージョン、再現手順、関連する `LogOutput.log` の抜粋を添えて報告してください。共有前にローカルのユーザー名などを伏せてください。

## ソースとクレジット

開発ブランチは `dev`、配布用ブランチは `main` です。配布前に、実機参照でビルドした Store 版 DLL と同梱の LICENSE を確認します。将来自動更新を導入する場合は、専用リポジトリに公開した Store 専用の資産名、対象プラットフォーム、署名済みハッシュを検証し、検証成功後にバックアップ付きで置き換える構成を前提とします。

原作は TwilightCup の [HSRTimer](https://github.com/TwilightCup/HSRTimer) です。[MIT License](LICENSE) と原作者の著作権表示を継承しています。
