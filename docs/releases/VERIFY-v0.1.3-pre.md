# 第11-5段階の公開前チェック

対象は最終 `HSRTimer-v0.1.3-pre.dll` と第11-3段階で生成済みのFontPack v1.0です。

1. Windowsの実ゲームで生成された同じBepInExインストールの `core` と `interop` を使い、`scripts/New-StoreReleaseDll.ps1` を実行します。x64、アセンブリ名、バージョン、既存GUID、埋め込みLICENSEの検査結果と最終DLLのSHA256を控えます。公開用には検証した同じDLLを使い、著作権表示を含むLICENSE全文を添えます。
2. データフォルダーの控えを作り、初回導入と既存環境からの更新を各1回確認します。初回は標準HUDとデバッグモードOFF、更新は設定・HUDレイアウト・PBの維持と旧DLLの重複がないことを確認します。
3. 検証用の既存ILサブセグメントPBとマーカーPBを更新します。ONでは旧記録のバックアップ、OFFでは新規の自動バックアップができず通常の保存・読み込みが成功すること、既存バックアップが残ることを確認します。マーカー編集時のバックアップは別に確認します。
4. One Key Retryで名前と数値IDを代表1件ずつ確認します。HUDフォントは保存済み1種の復元と標準への復帰を確認します。Overlay `0.1.2-pre` を併用し、Input Overlayタブと両HUDを確認します。
5. 実際に添付予定のFontPackを `Get-FileHash -Algorithm SHA256` で照合します。展開結果14件と内部 `SHA256SUMS.txt`、ライセンスを確認します。[第11-3段階の実測値](../store-font-stage10.md#第11-3段階のwindows実機結果)と一致すれば、再生成は不要です。

確認済みの全フォント組み合わせ、破損・欠落・ジャンクション、長時間負荷の再試験は、最終DLLで関連差分や不具合が見つかった場合に絞ります。

## 2026-11-25 Windows実機結果（ユーザー報告）

環境は Human: Fall Flat Microsoft Store／Xbox PC 1696732、Unity 2019.4.24f1、BepInEx 6.0.0-be.793、IL2CPP／Windows x64、.NET 6.0.7、Windows PowerShell 5.1です。DLLの生成元は `e59a4d38aab4ba3196c36160011dc62b1d2ecf7b` です。

- Releaseビルド、x64、バージョン、GUID、埋め込みLICENSEの検査、BepInExでの読み込みに成功しました。最終ログはError 0件、既存のIl2CppInterop Warning 76件でした。検査ツールのNETSDK1138は.NET 6のサポート終了を知らせる警告です。
- 初回のデータフォルダーとデフォルトプリセットの生成、標準フォント、PB自動バックアップOFF、診断GUI非表示を確認しました。更新時は設定・レイアウト・PBを保持し、保存済みフォントを描画しました。標準IMGUIに戻して保存し、外部フォントへ再変更して保存するとRoboto Mono Boldが次回起動時に復元されました。元のデータフォルダーは試験後に復元しました。
- One Key Retryは `Mansion`、`12`（Reprise）、`112`（Underwater）で動作しました。TwilightInputOverlay `0.1.2-pre` との併用でInput Overlayタブと両HUDの表示を確認しました。

### PB自動バックアップ

Mansion／IL／Anyの検証用記録です。この機能は `0.1.2-pre` からあります。

| 試行 | 自動バックアップ | タイム | サンプル数 |
| --- | --- | ---: | ---: |
| 1 | OFF | 25.867秒 | 24 |
| 2 | ON | 20.333秒 | 18 |
| 3 | OFF | 18.517秒 | 17 |

最終的にILサブセグメントの `Any/meta.json` は18517 ms／17サンプル、`Any.previous/meta.json` は25867 ms／24サンプルでした。マーカーPBの `Any.json` は18517 ms、`Any.json.bak-*` は25867 msでした。ONで旧PBを残し、OFFに戻して通常PBを更新しても既存バックアップが保持されることを、両方の実ファイルで確認しました。

マーカー登録済み状態での個別通過タイム、手動登録・編集時のバックアップ、Full Game PBバックアップは今回の実機確認に含みません。既存のStoreLogicで確認したマーカー編集バックアップは自動テストです。

### FontPack v1.0

Windows PowerShell 5.1での生成とテスト、ZIP全体のSHA256照合、展開、内部SHA256 **13/13 PASS** を確認しました。14件の内訳はBundle 5件、README 3言語、ライセンス・著作権表示4件、`FONT-SOURCES.csv` と `SHA256SUMS.txt` 各1件です。

| Bundle | Bytes |
| --- | ---: |
| `notosanscjkjp-regular-embedded.bundle` | 14,772,172 |
| `notosanscjkjp-bold-embedded.bundle` | 15,354,921 |
| `notosanscjksc-regular-embedded.bundle` | 14,732,552 |
| `roboto-regular-embedded.bundle` | 302,789 |
| `robotomono-regular-embedded.bundle` | 84,546 |
| 合計 | 45,246,980 |

### 添付前の最終照合

| 配布候補 | サイズ | 期待SHA256 |
| --- | ---: | --- |
| `HSRTimer-v0.1.3-pre.dll` | 今回の報告に記載なし | `FE0D6030E5929A5A8EA96BC8C8EC8DC1CA71E31D61EBD13291D0FD7CCD4E3A86` |
| `HSRTimer-MicrosoftStore-FontPack-v1.0.zip`（任意） | 42,472,033 bytes（40.50 MiB） | `AF74D061D6FBA56130B41C33CDF1BFAC04FD5F5E6873CCDCDBB2BD83A8A56B8C` |

第12-1段階の作業環境からWindows上の原本にはアクセスできなかったため、ここに記載した照合は実機報告です。公開時は既存の候補から添付する実ファイルを選び、`Get-Item -LiteralPath $file` と `Get-FileHash -LiteralPath $file -Algorithm SHA256` で確認します。欠落または不一致があれば公開を止めます。生成元からの変更はドキュメントのみです。候補の一致が確認できれば再ビルド・再生成は不要です。

公開予定タグは `v0.1.3-pre` です。著作権表示を含む[MIT LICENSE全文](../../LICENSE)を添付または掲載します。FontPackには独立したフォントライセンスと著作権表示が入っており、本体のみでも使用できます。
