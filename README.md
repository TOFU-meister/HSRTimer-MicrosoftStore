# HSRTimer Microsoft Store

Windows x64 の **Human: Fall Flat（Microsoft Store／Xbox PC アプリ版）** 向けの非公式移植です。ゲーム時間の自動計測、Timer HUD、タグ別の PB、サブセグメント比較、マーカーを利用できます。

開発版は **0.1.3-pre**、公開済みの初回テスト版は **0.1.0-pre** です。[テスト版の導入説明](docs/releases/README-v0.1.0-pre.txt)

## 対応環境

- Windows 64 ビットの Human: Fall Flat Microsoft Store／Xbox PC 版（Unity 2019.4.24f1 IL2CPP）
- BepInEx **6.0.0-be.793 Unity IL2CPP Windows x64**
- ソースからビルドする場合は .NET 6 SDK が必要です。

## インストール方法

### 1. Xbox アプリでゲームのフォルダーを開く

1. Windows の **Xbox アプリ**を開き、**マイ ライブラリ**で **Human: Fall Flat** を探します。
2. ゲームを右クリックするか、ゲームの **…** メニューを開き、**管理 (Manage)** を選びます。
3. **ファイル (Files)** タブの **参照 (Browse)**、またはフォルダーを開くボタンを押します。エクスプローラーで開いた場所に `Content` があれば、その中を確認します。**ゲーム本体の `.exe` があるフォルダー**が、この手順で使う場所です。

`C:\XboxGames\Human Fall Flat\Content\` は例です。別のドライブやインストール先を選んだ PC では場所が異なります。メニュー名が違う場合や参照ボタンが見つからない場合は、Xbox アプリで選んだインストール先のドライブをエクスプローラーで開き、`XboxGames` などのゲーム保存フォルダーを確認してください。エクスプローラーで `.exe` が表示されない場合は、表示メニューでファイル名拡張子を表示できます。開けるゲームフォルダーを確認できず、`WindowsApps` にしか見当たらない場合は、ここで作業を止めてください。

参考 [Xbox アプリのゲーム管理とインストール先](https://news.xbox.com/en-us/2021/11/18/advanced-installation-and-management-features-available-for-insiders-through-the-xbox-app-for-windows/)

### 2. 対応する BepInEx をダウンロードする

[BepInEx 6.0.0-be.793 Unity IL2CPP Windows x64 の ZIP](https://builds.bepinex.dev/projects/bepinex_be/793/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.793%2Bdbc82f0.zip) をダウンロードします。**BepInEx 5、Unity Mono、Linux、Windows x86** はそれぞれ別の環境向けです。この手順ではリンク先の **Unity IL2CPP Windows x64** を使います。

### 3. BepInEx を入れて、ゲームを一度起動する

1. ゲームを終了します。ダウンロードした BepInEx の ZIP を右クリックし、**すべて展開**を選びます。
2. 展開先を開き、`BepInEx` と `winhttp.dll` が並んでいる場所まで進みます。**その階層の中身をすべて**、手順1で見つけたゲーム本体の `.exe` と同じフォルダーへコピーします。
3. 次の並びになっているか確認します。**`Content\BepInEx\BepInEx\`** のように二重になると読み込めません。

```text
C:\XboxGames\Human Fall Flat\Content\
├─ （ゲーム本体の .exe など）
├─ BepInEx\
│  └─ core\
├─ dotnet\
├─ winhttp.dll
├─ doorstop_config.ini
├─ .doorstop_version
└─ changelog.txt
```

4. Xbox アプリからゲームを起動します。初回は BepInEx が準備用のファイルを作るため、起動に時間がかかることがあります。ゲームのメニューが表示されたら終了します。
5. ゲームフォルダーの `BepInEx` を開き、`config\BepInEx.cfg`、`interop\`、`LogOutput.log` を確認します。`interop` は初回の準備が成功すると生成されます。`plugins` がなければ、次の手順で `BepInEx` の中に作れます。

参考 [BepInEx の IL2CPP ゲーム向け導入手順](https://docs.bepinex.dev/v6.0.0-pre.1/articles/user_guide/installation/unity_il2cpp.html)

### 4. プラグインの DLL を入れる

ゲームを終了したまま、使いたいプラグインの GitHub Releases を開き、対象バージョンの **Assets** から **DLL を直接**ダウンロードします。以下は対象バージョンが公開されたときのファイル名です。

| プラグイン | ダウンロードする DLL | 置くフォルダー |
| --- | --- | --- |
| [HSRTimer Releases](https://github.com/TOFU-meister/HSRTimer-MicrosoftStore/releases) | `HSRTimer-v0.1.3-pre.dll` | `BepInEx\plugins\` |
| [TwilightInputOverlay Releases](https://github.com/TOFU-meister/TwilightInputOverlay-MicrosoftStore/releases) | `TwilightInputOverlay-v0.1.1-pre.dll` | `BepInEx\plugins\` |

`BepInEx` の中に `plugins` がなければ作ります。旧 DLL を取り除き、ダウンロードした DLL を名前そのままで `plugins` の直下にコピーします。プラグインごとのサブフォルダーは不要です。新しい配布形式は **DLL 単体**です。GitHub の **Source code (zip)** はソースコードで、導入には使いません。過去の HSRTimer **`0.1.0-pre`** を使う場合だけ、当時の配布 ZIP を展開して、中の `HSRTimer.dll` を同じ HSRTimer 用フォルダーに入れます。

片方だけ使う場合は、その DLL だけを入れます。両方使う場合は次のようになります。パスは一例です。

```text
C:\XboxGames\Human Fall Flat\Content\
└─ BepInEx\
   └─ plugins\
      ├─ HSRTimer-v0.1.3-pre.dll
      └─ TwilightInputOverlay-v0.1.1-pre.dll
```

## 動作確認

1. Xbox アプリからゲームを起動し、メニューが表示されることを確認します。
2. HSRTimer を入れた場合はタイマー HUD を確認します。TwilightInputOverlay を入れた場合はキーを押し、入力表示が反応するか確認します。
3. **Home** キーで設定を開きます。キーを変更してある場合は設定済みのキーを使います。TwilightInputOverlay 単独なら独立した設定画面が開きます。対応版を両方入れた場合は、HSRTimer の設定画面で **Input Overlay** タブを開きます。
4. 読み込みを確認したい場合は、ゲームフォルダー内の `BepInEx\LogOutput.log` をメモ帳で開き、プラグイン名とバージョンを検索します。

## 更新・アンインストール

- **更新**するときはゲームを終了し、`BepInEx\plugins` 以下の別フォルダーも含めて、同じプラグインの旧 DLL を取り除いてから新しい DLL を入れます。名前にバージョンが付かない旧 DLL も確認してください。併用時は各プラグインの DLL を1個ずつ置きます。
- **アンインストール**するときはゲームを終了し、外したいプラグインの DLL を取り除きます。設定・PB・プリセットは DLL と別の場所にあるので、通常は残ります。
- **保存データも消したい場合**は、下の「設定と保存先」とログで実際の保存場所を確認します。残したいデータを先にバックアップし、ゲームを終了したうえで、対象プラグインの保存フォルダーだけを削除します。

## 設定と保存先

**ワンキーリトライの行き先**は Home → General → Specify retry level を ON にし、**Retry Map (Name or ID)** に指定します。名前のほか、plcc timer と同じ絶対番号（例 `0`、`1`、`100`）を入力できます。[対応するマップと番号](docs/RETRY-MAP-IDS.md)

**Home → General → Automatic PB Backup（PBの自動バックアップ）** は既定で OFF です。ON にすると、更新前の PB を保存します。OFF にしても PB の通常保存と読み込みは使え、既存のバックアップは残ります。マーカー編集時のバックアップも従来どおりです。

保存先は既定で **`<Windows のドキュメント>/HSRTimer-MicrosoftStore/`** です。Windows が返すドキュメントの場所を使うため、OneDrive 側に移動している場合も、その場所が安全かつ書き込み可能なら使います。フォルダーが見つからない、書き込みできない、または再解析ポイントを経由する場合は **`%LOCALAPPDATA%\HSRTimer-MicrosoftStore`** に切り替わります。

ゲーム内では **Home → About → Open Data Folder** で、今使っている保存先をエクスプローラーで開けます。

| データ | 保存先からの相対パス |
| --- | --- |
| 設定・レイアウト・タグ | `settings.ini`、`layout.ini`、`tags.ini` |
| 言語・プリセット | `lang/`、`presets/` |
| IL・Full Game PB | `subsegment/pb/IL/`、`subsegment/pb/ML/` |
| マーカー・Marker PB | `markers/` |

旧 LocalAppData の設定や PB を引き継ぐには、ゲームを終了し、旧・新フォルダーをバックアップしてから、新しい場所へ必要なファイルをコピーします。PB は `meta.json` とサンプルを含むカテゴリフォルダーごとコピーし、同名データがある場合はどちらを使うか確認してから移してください。[手動移行の詳しい手順](docs/MICROSOFT-STORE.md#localappdata-の設定記録を引き継ぐ場合)

### 既知の制限

- About の更新確認は、この専用リポジトリの安定版を案内します。DLL の差し替えは手動です。
- Store 版プリセットはレイアウトのみを保存・復元します。
- 設定欄はクリックで編集し、Enter で確定、Esc で取り消します。日本語 IME の直接入力は未検証です。
- Steam Workshop 連携や外部プラグインには Store 対応が必要です。

不具合は [このリポジトリの Issues](https://github.com/TOFU-meister/HSRTimer-MicrosoftStore/issues) に、ゲームとプラグインのバージョン、再現手順、関連する `LogOutput.log` の抜粋を添えて報告してください。共有前にローカルのユーザー名などを伏せてください。

## トラブルシューティング

| 症状 | 確認すること |
| --- | --- |
| ゲームのフォルダーが見つからない | Xbox アプリのゲームの **… → 管理 → ファイル → 参照** を探します。見つからない場合は、インストール先に選んだドライブで `XboxGames` などを確認します。 |
| BepInEx が読み込まれない | `winhttp.dll`、`doorstop_config.ini`、`dotnet`、`BepInEx` がゲーム本体の `.exe` と同じ階層にあるか確認します。初回起動後に `LogOutput.log` が作られたかも確認します。 |
| `BepInEx` が二重になっている | コピー先を確認し、ゲーム本体の `.exe` から見て `BepInEx\core` になるよう、展開した ZIP の中身を配置し直します。 |
| 間違った BepInEx を入れた | ゲームを終了し、混在を避けて上記の **6.0.0-be.793 Unity IL2CPP Windows x64** を入れ直します。 |
| HUD が出ない | DLL が `BepInEx\plugins` の下にあるか、ログに読み込みが記録されているか、設定で HUD 表示が有効かを確認します。 |
| Home で設定が開かない | ゲーム画面をクリックしてから Home を押します。キーを変更済みの場合は変更後のキーを使います。統合時は HSRTimer 側の設定キーを使います。 |
| Input Overlay タブが出ない | HSRTimer **0.1.3-pre** と TwilightInputOverlay **0.1.1-pre** の両方がロードされているかをログで確認します。旧 HSRTimer **0.1.0-pre** との組み合わせでは Overlay の独立画面を使います。 |
| 旧版と新版が重複している | ゲームを終了し、`BepInEx\plugins` 全体でプラグイン名を検索します。同じプラグインの旧 DLL を外し、新 DLL だけを残します。 |

BepInEx のコンソール画面を隠したい場合は、ゲーム終了後に `BepInEx\config\BepInEx.cfg` をメモ帳で開き、既存の `[Logging.Console]` の `Enabled = false` を設定します。ログファイルは `BepInEx\LogOutput.log` で確認できます。[コンソール設定の参考](https://docs.bepinex.dev/articles/user_guide/troubleshooting.html#enable-console)

## 開発者向けビルド・配布手順

`dev` のビルドには、実機で生成した `BepInEx/interop` と、同じインストールの `BepInEx/core` を使います。配布用の DLL 作成とライセンス掲載は [Microsoft Store 版のビルド・配布手順](docs/MICROSOFT-STORE.md#配布用-dll-を作成する)を参照してください。

### バージョン管理

Store 版は `0.x.x-pre` を使い、各プラグインで独立して進めます。先頭の `0` と末尾の `-pre` はプレリリース期間中固定です。小機能、バグ修正、UI 調整は3番目を進めます。大きな機能追加、仕様変更、大規模な修正は2番目を進め、3番目を `0` に戻します。バージョン更新時は `PluginInfo.cs` と `.csproj` の番号をそろえます。公開時のタグは `v<version>`、配布 DLL は `HSRTimer-v<version>.dll` とします。



### 開発版のフォント確認

開発版 `0.1.3-pre` では、`Home → About → Font Diagnostics` から取得方法と実描画を比較できます。標準フォントは従来の IMGUI スキンで、一時適用はそのセッションの Timer HUD に限ります。[第1段階の調査](docs/store-font-diagnostics.md)・[第2段階の診断と AssetBundle 作成手順](docs/store-font-stage2.md)・[第3段階の採取手順](docs/store-font-stage3.md)・[第4段階のメタデータ解析](docs/store-font-stage4.md)・[第5段階の Bundle 読込診断](docs/store-font-stage5.md)・[第6段階の寿命管理](docs/store-font-stage6.md)

## ライセンス・クレジット

原作は TwilightCup の [HSRTimer](https://github.com/TwilightCup/HSRTimer) です。[MIT License](LICENSE) と原作者の著作権表示を継承しています。

### パフォーマンス診断

Windows 実機での計測・比較手順は [第7段階のパフォーマンス調査](docs/store-performance-stage7.md) を参照してください。
