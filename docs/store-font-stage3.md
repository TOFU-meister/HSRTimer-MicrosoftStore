# Store 版フォント調査 第3段階

対象は Human: Fall Flat 1696732、Unity 2019.4.24f1、BepInEx 6.0.0-be.793、Windows x64 です。前回の実機で OS 一覧は191件、既存 Font 5件はプレビュー・一時適用できたように見えました。

## 欠落箇所の比較

| メソッド | Unity 2019.4 参照 | ゲームメタデータ | ネイティブ側 | Store interop |
| --- | --- | --- | --- | --- |
| Font() | あり | 未確認 | 未確認 | 欠落 |
| Font(string) | あり | 未確認 | 未確認 | 欠落 |
| Font(string[], int) | private ctor | 未確認 | 未確認 | Il2CppStringArray 版が欠落 |
| CreateDynamicFontFromOSFont(string, int) | あり、配列 ctor へ委譲 | 未確認 | AOT 実体未確認 | 手元の IL は欠落した文字列 ctor を参照 |
| CreateDynamicFontFromOSFont(string[], int) | あり | 未確認 | AOT 実体未確認 | 手元の IL は欠落した配列 ctor を参照 |
| GetOSInstalledFontNames | Internal Call | 未確認 | 実機で成功 | 191件取得 |
| Internal_CreateFont | Internal Call | 未確認 | 実機で名前解決、呼び出し・ABI 未確認 | ラッパーあり |
| Internal_CreateFontFromPath | Internal Call | 未確認 | 実機で名前解決、呼び出し・ABI 未確認 | ラッパーあり |
| Internal_CreateDynamicFont | Internal Call | 未確認 | 実機で名前解決、呼び出し・ABI 未確認 | ラッパーあり |

確定している欠落は生成済み interop の段階です。Managed Code Stripping は整合する仮説です。ゲーム本体のメタデータ、ネイティブ実体、Cpp2IL の中間出力が未照合のため、最初に欠けた段階は未確定です。リンク設定や Managed Stripping Level はゲームのビルド時の入力で、配布済みゲームに後から追加したプラグインで変更できません。 [Unity のストリッピング手順](https://docs.unity3d.com/ja/2018.4/Manual/IL2CPP-BytecodeStripping.html) · [Unity 2019.4 の参照ソース](https://github.com/Unity-Technologies/UnityCsReference/blob/2019.4/Modules/TextRendering/TextRendering.bindings.cs)

## Cpp2IL と Il2CppInterop

BepInEx be.793 は GameAssembly・global-metadata・Unity バージョンを Cpp2IL に渡し、ダミー DLL を作って Generator に渡します。今回確認した Generator の通常パスは、入力にあるコンストラクターの専用生成処理を持ちます。一方、Unity ベースライブラリから欠落メソッドを補う **Pass80UnstripMethods は ctor をスキップ**します。内部呼び出しのラッパーとファクトリーが見えても、呼び先の ctor が欠落する組み合わせを説明できます。この実装だけではゲーム側の ctor の欠落は確定しません。 [BepInEx の生成処理](https://github.com/BepInEx/BepInEx/blob/dbc82f05b8ea34833de5b6a723eb60dd8398e0b1/Runtimes/Unity/BepInEx.Unity.IL2CPP/Il2CppInteropManager.cs) · [通常の生成パス](https://github.com/BepInEx/Il2CppInterop/blob/81a6f78c8b653e0da4a3420ac4cd00819e8b6292/Il2CppInterop.Generator/Passes/Pass50GenerateMethods.cs) · [補完パス](https://github.com/BepInEx/Il2CppInterop/blob/81a6f78c8b653e0da4a3420ac4cd00819e8b6292/Il2CppInterop.Generator/Passes/Pass80UnstripMethods.cs)

実機の定義がなければ欠落は少なくとも wrapper 生成前です。UnityLinker と IL2CPP 変換の区別にはビルド中間物等が必要です。定義がありダミーにない場合は Cpp2IL の解析・出力を、ダミーにもある場合は Generator の入力解決・警告・出力を調べます。まず実機と同じ Cpp2IL を使います。Unity 2019.4 系のメタデータ形式はヘッダーの整数だけでは細分化を判別できません。Il2CppDumper、Il2CppInspector は同じ Unity 版と Windows PE を解釈できる場合のクロスチェック候補です。今回はゲーム本体のペアがないため、ネイティブ解析は未実施です。

定義と実体が残っていれば、同じツールチェーンで隔離した生成物を再検証できます。共有 interop を差し替える配布は、他プラグインとの互換性、ゲームや BepInEx の更新、再生成での消失を考慮すると適しません。

## 代替経路

| 内部処理 | Unity の参照ソース上の役割 | Store 版での判断 |
| --- | --- | --- |
| Internal_CreateFont(Font, string) | 引数なし ctor は null、文字列 ctor の名前分岐はファイル名を渡す | 正規の文字列 ctor が通れば候補。任意の OS ファミリー名の扱いは未確認。 |
| Internal_CreateFontFromPath(Font, string) | 文字列 ctor のパス分岐 | Windows Fonts、ユーザー単位フォント、TTF・OTF・TTC、および日本語・中国語は、正規 ctor の確立後に別々に描画検証する必要がある。 |
| Internal_CreateDynamicFont(Font, string[], int) | private ctor が優先順付きの名前とサイズを渡す | 本家の順序を再現する正規経路。標準 interop で Il2CppStringArray を渡せることと、Font の初期化が成立することは別。 |

既存 ctor 用の生成コードは IL2CPP オブジェクトを確保して ctor を呼びます。GC の管理対象になっても、UnityEngine.Object としてのネイティブ側の初期化や所有権は保証されません。内部処理の名前解決は、受け手をどう初期化するか、ABI と破棄契約が正しいかを保証しません。新しい生成経路は実装せず、安全な調査に限定しました。既存のフォント診断、セッション適用、標準 IMGUI、AssetBundle は維持します。

## Windows で採取するもの

1. **Home → About → Font Diagnostics → Inspect interop and internal-call availability** を実行し、**Export diagnostic report** でデータフォルダーに font-diagnostics.txt を出力します。新しい診断は IL2CPP の Font クラスからシグネチャとトークンを読み、interop および Internal Call の結果と併記します。クラス・問い合わせ API が利用できない場合は診断不能と記録します。
2. EXE があるフォルダーを指定します。スクリプトは存在、サイズ、SHA256、版情報、メタデータヘッダー、関連ログをテキストへ記録します。

```powershell
.\scripts\Get-StoreFontEvidence.ps1 -GameDir 'C:\XboxGames\Human Fall Flat\Content'
```

3. 最初に必要なのは font-diagnostics.txt と hsrtimer-font-evidence.txt の2つです。ctor 定義が見えた場合は Cpp2IL の Font メソッド一覧かダミー TextRenderingModule と生成ログを照合します。オフライン解析が必要な場合のみ、同一版の GameAssembly.dll と *_Data/il2cpp_data/Metadata/global-metadata.dat のペアが必要です。UnityPlayer.dll は Internal Call の実体を調べる段階の資料です。ゲームのバイナリは GitHub や Release に含めないでください。

## 任意の再現実験

Unity 2019.4.24f1 の同一プロジェクトを Windows IL2CPP でビルドし、Font 未使用、公開 ctor とファクトリーを直接使用、link.xml による保持、Managed Stripping Level の変更を比較します。リンカー直後のアセンブリ、メタデータ、Cpp2IL 出力、interop を保存します。今回は Editor 実験を行っていません。この比較で再現しても、Human: Fall Flat の欠落段階を示す直接証拠とは区別します。
