# Store フォント第6段階 — 寿命と復旧

開発版 0.1.3-pre、Human: Fall Flat 1696732、Unity 2019.4.24f1、BepInEx 6.0.0-be.793、WindowsPlayer x64。

## Windows で確認されたこと

- Unity Editor プロジェクトの AssetBundle モジュールを有効にした後、バンドルの生成と Editor 内での再読込に成功しました。`Packages/manifest.json` に同じ依存関係を正式に追加しています。
- Texture2D 対照はゲーム内の LoadFromFile(string) で取得できました。非圧縮 Dynamic Font の NotoSansCJKjp-Regular は候補に追加され、英数字・日本語・中国語のプレビュー、サイズ・太字、字形診断、Material と Texture の確認、一時適用に成功しました。前回失敗した LZ4 方式の原因は未確定です。
- シーン移動後、Candidate の名前は残りましたが Font は無効になり、適用中の HUD は IMGUI に戻りました。手動再取得で Instance ID が 33406、36148、38590 と変化しました。従来の `already open` はパスの登録だけを見ており、ネイティブ Bundle や Font が有効であることを確認していませんでした。

ログから Font の無効化は確認できます。正確な呼び出し元は記録されていません。Resources.UnloadUnusedAssets やシーン移動時の整理、明示的な bundle アンロードはいずれも可能性があります。管理側に参照が残っても Unity ネイティブ資源の寿命は保証されません。

## 実装

- 診断が所有するバンドルの Font に `HideFlags.DontUnloadUnusedAsset` を追加します。アトラス Texture と Material は共有・再生成され得るため、状態のみ監視し、フラグ設定や個別破棄はしません。Unity が有効なアトラスへ差し替えた場合はそのまま受け入れます。まだアトラスが生成されていないことだけでは無効と判定しません。 [Unity 2019.4 HideFlags](https://docs.unity3d.com/2019.4/Documentation/ScriptReference/HideFlags.html)
- Timer HUD のメインスレッド Update で、最大500msに1回、登録済み Bundle、Font、以前有効だった Material と Texture を確認します。無効な場合は候補を除き、HUD を標準に戻して GUIStyle の外部 Font 参照を解除します。その後、まだ有効な所有 Bundle のみを明示的に解放します。
- 2秒以上間隔を空けて、元のパス・読み込み API で再読込し、アセット名で候補と適用を引き継ぎます。1回の障害につき最大3回です。再読込直後に再び無効になる場合も通算し、約5秒安定したら回数をリセットします。古い登録が残っていても、手動読込時に有効性を再確認します。
- **Restore IMGUI / release probe fonts** は待機中の復旧と再適用も解除します。設定はセッション限定です。既存のゲーム所有 Font にはフラグ変更や破棄を行いません。
- **Record current status / Export diagnostic report** は現在のシーン、候補の Instance ID、各資源の状態と HideFlags、保持・復旧中の Bundle 件数を記録します。無効化・試行・復旧・再適用は発生時に記録します。

## Windows 再検証

1. 更新した DLL と、前回成功した非圧縮 Font バンドルを使用します。**Home → About → Font Diagnostics** で読み込み、目視確認後に Timer HUD へ適用します。
2. メインメニュー → Mansion、Mansion → Train、ゲーム → メインメニューを順に試します。各遷移後の Candidate、プレビュー、HUD、レポートを確認します。維持できた場合は Instance ID が変わらず、復旧した場合は同じアセット名に新しい Instance ID が対応します。
3. 復旧中に標準 IMGUI が表示され、復旧後は外部 Font が戻ることを確認します。**Restore IMGUI / release probe fonts** を押した後は、遷移しても外部 Font が自動で戻らないことを確認します。
4. 繰り返しシーン移動した後、Bundle の件数、例外ログ、HUD、PB 保存、One Key Retry、Input Overlay タブを確認します。ネイティブ側の解放と実際の描画は Windows で再確認が必要です。
