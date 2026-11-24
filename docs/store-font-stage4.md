# Store 版フォント調査 第4段階

対象は Human: Fall Flat 1696732、Unity 2019.4.24f1、Windows x64 です。提供されたファイルは解析用ワークスペース内で読み取り、Git に含めていません。

## 入力の照合と解析方法

| ファイル | SHA256（実機の採取結果と一致） |
| --- | --- |
| GameAssembly.dll | 2061bd0a0c55c84f44ad53e4436440d6fa29ba260aec3a2268ff93d656503d43 |
| global-metadata.dat | 99d63fee40bd05fc7b6c2a84f0ea4f17f991d31021dec32249e95e522d17e81b |
| UnityEngine.TextRenderingModule.dll（実機 interop） | 6ed86fd18df53534b3c7557981aa07cb4d91f3e35042aea04ca8313e465b9c48 |

使用した Cpp2IL / LibCpp2IL は BepInEx be.793 同梱の 2022.1.0-development.1452（558ddd98642010897d54316b51fbaa7889fda093）です。Cpp2IL.Core と Generator の DLL は実機採取の SHA256 とも一致しました。PE と global-metadata の組を Unity 2019.4.24f1 として初期化した結果、Cpp2IL は **24.5** と判定しました。ファイルヘッダーの版フィールド自体は24です。Cpp2IL の低水準メタデータと、同じ Cpp2IL の BepInEx 相当のダミー DLL 出力を別々に検査しました。

## Generator の再現結果

同じ be.793 に含まれる Il2CppInterop Generator 1.5.3（dbda1cb353b0f4253345dc45136d170b9e50a5a0）に、生成した Cpp2IL アセンブリと BepInEx の Unity 2019.4.24 ベースライブラリを与え、隔離ディレクトリで97アセンブリを生成しました。オフライン比較では xref スキャンとキャッシュ生成を省略しました。Font について、再生成と実機 interop のメソッド・フィールド・プロパティの比較対象66件は一致しました。DLL 全体のバイト同一性を示す比較ではありません。

再生成した単一名ファクトリーの IL は Font(string)、配列版は Font(Il2CppStringArray, int) を参照します。ベースライブラリに存在する ctor は補われず、GetOSInstalledFontNames と3種類の Internal_Create* は名前解決デリゲートを持つ形で再現されました。したがって、これらのラッパーはゲームの Font 定義の復元に由来せず、ベースライブラリを適用した補完に由来すると判断できます。

## ゲーム本体の Font 全定義

イメージは UnityEngine.TextRenderingModule.dll、型名は UnityEngine.Font、親は UnityEngine.Object です。実アドレスは公開せず、表にあるすべてのメソッドについて GameAssembly PE に対する Cpp2IL のコードポインター対応が非ゼロであることを確認しました。Internal Call 行の対応は AOT 側のディスパッチ経路であり、UnityPlayer 内の処理を実行して検証した結果ではありません。

| メタデータトークン | 種別 | シグネチャ → 戻り値 | 実装フラグ |
| --- | --- | --- | --- |
| 06000025 | static | add_textureRebuilt(System.Action&lt;UnityEngine.Font&gt;) → Void | 通常 AOT |
| 06000026 | static | remove_textureRebuilt(System.Action&lt;UnityEngine.Font&gt;) → Void | 通常 AOT |
| 06000027 | instance | get_material() → UnityEngine.Material | Internal Call |
| 06000028 | instance | get_dynamic() → Boolean | Internal Call |
| 06000029 | instance | get_fontSize() → Int32 | Internal Call |
| 0600002A | static | InvokeTextureRebuilt_Internal(UnityEngine.Font) → Void | 通常 AOT |
| 0600002B | instance | HasCharacter(Char) → Boolean | 通常 AOT |
| 0600002C | instance / private | HasCharacter(Int32) → Boolean | Internal Call |

フィールドは textureRebuilt（System.Action&lt;UnityEngine.Font&gt;）と m_FontTextureRebuildCallback（UnityEngine.Font.FontTextureRebuildCallback）。プロパティは material（Material）、dynamic（Boolean）、fontSize（Int32）。コンストラクター定義は0件です。Cpp2IL のダミーにもこの8件と同じフィールド・プロパティがありました。ダミーの IL や実装フラグはネイティブ動作の証拠に使っていません。

## 9件が一致しなかった理由

**9件とも、実際に出荷メタデータの Font に定義がありません。** 前回の診断は対象の名前だけを記録しており、正常に列挙できたかを判定できませんでした。今回のファイル解析で、不一致の結果自体は正しいと確認できました。

| 対象 | ゲームメタデータ / Cpp2IL dummy | 実機 interop | 実機で確認した範囲 |
| --- | --- | --- | --- |
| Font()、Font(string)、Font(string[], int) | 全てなし | 全てなし | 生成経路なし |
| CreateDynamicFontFromOSFont（単一名と配列） | 両方なし | 両方あり。各ラッパーが欠落した ctor を参照 | 生成不可 |
| GetOSInstalledFontNames() | なし | 名前解決型の補完ラッパーあり | 名前解決・191件取得 |
| Internal_CreateFont、Internal_CreateFontFromPath、Internal_CreateDynamicFont | 全てなし | 各名前解決型の補完ラッパーあり | 名前解決のみ成功 |

対照としてゲーム側には Font.get_dynamic、Font.HasCharacter(Char)、Object.GetInstanceID、Texture2D.get_whiteTexture、AssetBundle.GetAllAssetNames が存在します。Font.RequestCharactersInTexture は Font の出荷定義にはなく、補完ラッパーと区別できます。AssetBundle は LoadFromFile、型指定 LoadAsset、GetAllAssetNames、Unload のゲーム定義を確認済みです。LoadFromFile の引数を省略した managed overload と、ゲーム側の3引数定義は区別します。

## 欠落段階と安全な経路

正確な実機 Generator コミット dbda1cb353b0f4253345dc45136d170b9e50a5a0 の Pass80UnstripMethods は、ベースライブラリからの補完対象として ctor を明示的にスキップします。今回の欠落は既に出荷メタデータにあり、Cpp2IL や Generator がゲームに残っていた ctor を取り落とした結果ではありません。UnityLinker によるストリッピングと整合しますが、ゲームのリンク直後のマネージ中間物なしでは、UnityLinker と IL2CPP の出力工程を区別できません。 [対象 Generator ソース](https://github.com/BepInEx/Il2CppInterop/blob/dbda1cb353b0f4253345dc45136d170b9e50a5a0/Il2CppInterop.Generator/Passes/Pass80UnstripMethods.cs)

OS フォント名、TTF・OTF・TTC のいずれも、本ビルド向けに安全な直接生成は確立していません。残存 Internal Call の登録だけでは Unity ネイティブ実体との関連付け、アトラス初期化、所有権・破棄の契約を確認できません。Object.Instantiate の定義はありますが、Font の複製可否やアトラスの独立性、補完版 fontNames セッターで目的の字形へ更新されるかは未検証です。将来試すなら既存のゲーム所有 Font に手を加えず、所有できる複製を確認してから、字形・破棄・シーン移動を検証します。 [Unity 2019.4 Font 参照ソース](https://github.com/Unity-Technologies/UnityCsReference/blob/2019.4/Modules/TextRendering/TextRendering.bindings.cs)

フォント生成コードを追加する根拠は得られなかったため、既存の IMGUI・Font プレビュー・AssetBundle 実装を維持しました。次は埋め込み Dynamic Font の AssetBundle を1本作って、読み込み・プレビュー・適用とシーン移動後の復帰を確認するのが適切です。この段階で追加のゲーム DLL は不要です。UnityPlayer は Internal Call のネイティブ側を追加調査する場合にだけ必要になります。

## 実機診断

Font Diagnostics の **Inspect interop and internal-call availability** は、Font クラスの名前空間・イメージ・基底型・型名、現在の IMGUI Font とのクラス一致、インスタンス ctor 数、最大64件の全メソッド（引数・戻り値・static/instance・トークン・実装フラグ）を記録します。列挙終了と打ち切りを表示し、0件、対照不一致等では欠落判定を保留します。照会は読み取り専用で、対象メソッドの実行はしません。実機では診断して Export diagnostic report で出力できます。Unity Editor と Windows 実機での新診断および AssetBundle の描画・適用は今回未実施です。
