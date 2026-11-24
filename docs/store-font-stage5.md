# Store フォント調査 第5段階 — AssetBundle を開けない原因

対象は Human: Fall Flat 1696732 と Unity 2019.4.24f1、HSRTimer 0.1.3-pre。実機ログには従来の診断の失敗が3回記録されています。失敗は LoadAsset や Font キャストより前です。従来の診断は CLR null と Unity の無効オブジェクトをまとめて扱っており、Unity 自身の拒否理由は提供ログから特定できませんでした。直後の Status at Startup は現在シーン名を含むスナップショットです。

## 提供バンドル

| 項目 | 実測値 |
| --- | --- |
| サイズ / SHA256 | 14,773,004 bytes / 6069e139862ba348e653f3aabafb7b69401059c6c22f8a05462cc69b1933d2fc |
| ヘッダー | UnityFS 形式7、プレイヤー互換文字列 5.x.x、エンジン文字列 2019.4.24f1 |
| ブロック | メタデータ LZ4HC、126データブロックすべて LZ4HC。合計 14,772,313 bytes → 16,475,328 bytes |
| CRC | 展開データから計算した CRC32 2054291516、提供 manifest と一致 |
| シリアライズ結果 | UnityPy 1.25.4 が CAB を読み取り、target 19（StandaloneWindows64）、Unity 2019.4.24f1、Font、Texture2D、Material を確認 |
| Font | NotoSansCJKjp-Regular、18 px、事前字形矩形0件、m_FontData 16,467,736 bytes、名前 Noto Sans CJK JP |
| 入力アセット | Assets/Fonts/NotoSansCJKjp-Regular.otf、manifest に外部 bundle 依存なし |

添付ファイルの破損、Editor バージョンの取り違え、フォントデータの未埋め込みを示す証拠はありません。実機の fonts に置かれたコピーがこの添付と同一かは SHA256 で照合してください。ヘッダーから Editor の詳細なビルドハッシュまでは取得していません。

## API の比較

第4段階の Cpp2IL 24.5 解析および同バージョンの Generator で再生成した AssetBundleModule の IL を比較しました。実機の AssetBundleModule DLL そのものは未提供です。

| 診断の操作 | API | 出荷定義と再生成 IL |
| --- | --- | --- |
| Load local font bundles (file) | LoadFromFile(string) | 出荷定義あり。interop は runtime invoke |
| Compare file (CRC 0, offset 0) | LoadFromFile(string, uint, ulong) | 公開3引数版は補完。出荷定義の LoadFromFile_Internal へ渡す |
| Compare memory | LoadFromMemory(Il2CppStructArray&lt;byte&gt;) | 出荷定義あり。正規 interop 配列を使用 |
| Compare memory (CRC 0) | LoadFromMemory(Il2CppStructArray&lt;byte&gt;, uint) | 公開2引数版は補完。出荷定義の LoadFromMemory_Internal へ渡す |

Game.LoadLevel と ReloadBundleCoroutine の AOT コードから、AssetBundle.GetAllScenePaths および Unload の直接呼び出しを確認しました。調べた範囲では最初に外部ファイルを開く API は確定していません。今回、実際に選んだルートを記録し、存在しないルートからは別ルートに自動切替しません。

診断は Application.platform、ファイルサイズ、CLR null、IL2CPP ラッパー、Unity の null 判定、アセットの列挙・型判定、Reflection の内部例外を記録します。Unity のメインスレッドログフックが公開されていれば、試行中の警告・エラーを最大12件記録します。利用不可の場合はその旨を表示します。ファイルを開いた後の失敗では所有 bundle をアンロードします。開いているパスは **Restore IMGUI / release probe fonts** で解放するまで再試行をスキップします。Texture2D 対照は候補一覧へ追加しません。

## 仮説の切り分け

- ファイルで失敗しメモリで成功する場合は、Unity のパス処理、アクセス権限、Unicode パスを優先します。従来の UnityFS チェックにより、少なくともプラグインの .NET 側ではファイルを開けていました。
- raw が成功し同じ埋め込みフォントの LZ4HC 版だけが失敗する場合は圧縮経路を優先します。添付の LZ4HC データはオフラインで展開済みです。
- Texture2D は成功し Font だけが失敗する場合は、型情報や Font のシリアライズ・初期化を優先します。オフラインでは Font を読み取れました。IL2CPP の ctor 欠落が Native Bundle の Font デシリアライズにも影響したという証拠はまだありません。
- 全方式・Texture2D ともに失敗する場合は、実機の Application.platform と Unity ログを確認します。StandaloneWindows64 と WSAPlayer のバンドルは別ターゲットです。Microsoft Store という配布経路だけで WSA を選ぶ根拠にはなりません。今回の実機ログに Application.platform は未記録です。 [Unity 2019.4 の圧縮方式](https://docs.unity3d.com/2019.4/Documentation/Manual/AssetBundles-Cache.html)

## Windows での再試験

1. 同じ Editor 2019.4.24f1 で、第2段階のバッチ起動コマンドの -fontMode をそれぞれ **embedded**（LZ4HC）、**embedded-raw**（非圧縮）、**os**、**static**、**texture**（2×2 Texture2D 対照）へ変更し、モード別の -fontOutput を指定します。既定は StandaloneWindows64。実機で WSAPlayer が確認された場合のみ、対応モジュールをインストールし -fontTarget WSAPlayer を比較します。
2. Builder は生成直後に Editor 内で再読込し、名前、Font の dynamic、字形、アトラス、Texture2D 寸法を確認してアンロードします。Editor の成功とゲーム内の成功は別に記録します。
3. 元の生成先の bundle と fonts へコピーした bundle について、PowerShell でそれぞれ `Get-FileHash -Algorithm SHA256 -LiteralPath '対象の.bundle'` を実行し、Hash が同じか確認します。
4. **Home → About → Font Diagnostics** で Texture2D、埋め込み raw、埋め込み LZ4HC の順に、1種類ずつ fonts に置いて試します。各 API の前に解放し、成功した場合は Candidate または Texture2D 件数を確認します。**Export diagnostic report** から実機の結果を取得します。ファイル系だけが失敗した場合は、データルートやパスに Unicode が含まれるか記録します。

この環境では Unity Editor と Windows 実機を実行していません。Editor の再読込検証および追加したルートの実機結果は未確認です。
