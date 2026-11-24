# Store 版フォント診断

開発版 `0.1.3-pre` の Windows x64 / Unity 2019.4.24f1 / BepInEx 6 IL2CPP 用の調査機能です。通常の Timer HUD は引き続き IMGUI スキンのフォントを使います。フォントファイルの追加配布はありません。

## 本家との差と確認した根拠

本家の [調査時点のソース](https://github.com/TwilightCup/HSRTimer/tree/9d245d668cc460064b1fa627e7780b46abde6766) では、Timer HUD、Leaderboard HUD、設定パネル、マーカーのラベルが、順に PingFang SC、Microsoft YaHei、Noto Sans CJK SC、Noto Sans CJK、Heiti SC、Arial Unicode MS、Arial を指定します。Timer と Leaderboard は表示サイズ変更時に生成し直し、設定は生成サイズ 14、マーカーはラベルのサイズで生成します。Timer の行、警告、カスタムテキスト、軸ラベルは同じ生成フォントを設定します。フォントチェーンでは文字ごとに利用可能なフォントが選ばれます。 [Unity 2019.4 API](https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Font.CreateDynamicFontFromOSFont.html)

Store 版では [d14069a](https://github.com/TOFU-meister/HSRTimer-MicrosoftStore/commit/d14069a05449e749df4e96d0b6904761f6e67ff5) で動的生成を既定経路から外しました。以前の Windows 実機ログには `Method not found: 'Void UnityEngine.Font..ctor(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray, Int32)'` が繰り返し記録されていました。Mono ではマネージドの Unity API を直接使用します。IL2CPP の interop では公開ラッパーが見えていても、呼出し先のコンストラクターやネイティブ側の内部呼び出しが使えない場合があります。ログで確認できたのはこの生成経路の失敗です。OS 一覧取得や既存の Unity Font の描画可否は個別に検証します。

## 候補の比較

| 方法 | 用途と制限 |
|---|---|
| Unity OS 一覧と動的生成 | 追加ファイルなしで本家に近い。OS 一覧、単一ファミリー、優先順付きチェーンを個別に調査する。既知のコンストラクター欠落時は生成をスキップ。列挙結果は字形の網羅性や描画成功を示さない。 |
| スキン、ロード済みアセット、組み込み Arial | Font を取得できれば IMGUI でそのまま比較でき、追加配布も不要。選択肢と字形はゲームとシーンに依存し、アンロード後に再取得が必要な場合がある。ゲーム所有の Font は破棄しない。 |
| Windows のファイル・レジストリ | Windows Fonts ディレクトリ、ユーザー単位のインストール先、システム・ユーザーの登録情報で候補を把握できる。ファイル名とファミリー名は別で、登録経路・言語・コレクションで見え方が異なる。Unity Font の生成には別の経路が必要。 |
| GDI / DirectWrite | GDI はデバイスコンテキストで列挙、DirectWrite はシステム フォント コレクションとローカライズされたファミリー名を取得できる。どちらも列挙結果を IMGUI に直接指定できない。P/Invoke・COM の管理が必要になるため今回の診断には追加していない。 |
| Unity AssetBundle の Font | 2019.4.24f1 に合わせた Windows 向けバンドルと読み込み API の実機検証が必要。動的 Font は字形の追加生成やアトラス再構築、静的アトラスは字形・サイズ・太字の事前選択が必要。CJK では網羅性と配布サイズの兼ね合いが大きい。 |
| TextMeshPro / 独自ラスタライズ | TMP の Font Asset は IMGUI の Font と型が異なり、ゲーム側ランタイム・シェーダーと描画側の変更が必要。外部ラスタライズでも字形選択、計測、ベースライン、アトラス、GPU 転送を管理する必要がある。どちらも保守コストが高く、今回は既存の IMGUI を使用。 |

参照資料は [Windows GDI の列挙](https://learn.microsoft.com/windows/win32/gdi/enumerating-the-installed-fonts)、[DirectWrite の列挙](https://learn.microsoft.com/windows/win32/directwrite/font-enumeration)、[AssetBundle のプラットフォーム条件](https://docs.unity3d.com/Manual/assetbundles-platforms.html) です。

次の候補は、まず既存のゲームフォントを実機で比較し、要件を満たせない場合に配布許諾を確認したフォントで Unity Font の AssetBundle を試すことです。

## Windows での操作

1. README の Windows ビルド手順で DLL を生成し、ゲームを終了してから旧 HSRTimer DLL と差し替えます。
2. ゲームで `Home → About → Font Diagnostics` を開き、IMGUI スキン・ラベル・Timer HUD の表示を確認します。
3. **Scan game fonts** で既存の Unity Font を取得します。**List Unity OS fonts** で OS 一覧を調べ、**Probe selected OS font** と **Probe upstream fallback order** を個別に試します。OS 一覧が取れない場合も本家候補を順に選べます。候補一覧に追加された時点で確認できたのは取得だけです。
4. **Inspect glyph and atlas APIs** で Dynamic、HasCharacter、RequestCharactersInTexture、GetCharacterInfo、マテリアルとテクスチャの結果を確認します。未公開の API はスキップされます。IME やキーボード入力は不要です。比較プレビューの文字や太字・サイズを切り替え、数字、時刻、符号、英文、和文、簡体字・繁体字の欠け、豆腐文字、空白を確認します。
5. 目視で確認した候補は **I checked the candidate visually → Apply to Timer HUD for this session** で Timer の行と警告バナーに適用できます。通常プレイ、ステージ移動、One Key Retry、表示の切り替えを確認します。キャッシュが作られ直した後も文字の欠けがないか見ます。
6. **Restore IMGUI / release probe fonts** で復帰し、ゲーム再起動時も標準のままか確認します。設定パネル、Leaderboard や専用のマーカー表示には診断フォントを適用しません。例外は種類ごとに一度だけログに記録されます。

プレビューの呼び出し成功と正しい文字の表示は別です。ネイティブ側で例外を出さず字形が欠ける場合はプレビューとプレイ中の目視で確認してください。
