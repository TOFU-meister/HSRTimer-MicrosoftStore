# One Key Retry のマップ名と ID（Microsoft Store 版）

Home → General → Specify retry level を ON にし、**Retry Map (Name or ID)** に入力します。入力した番号は文字列として保存されます。`001` と入力すると画面にも `001` が残り、解決時は ID 1 として扱います。前後の空白は解決時に無視します。

## plcc timer との対応

`Timer v1.7.8.2(1).dll` の IL で、`Speedrun.Update`、`RestartCoroutine.MoveNext`、`Timer.get_Key` を照合しました。絶対指定は、通常マップがゲーム内番号のまま、Extra がゲーム内番号 + 100、200 以上が Workshop 用です。Extra の例では `100` はゲーム内部の `EditorPick 0` です。plcc のマップパック相対モードは、別の仕組みでパック内の 0 始まりの順番を参照します。Store 版のこの入力欄では絶対番号を使用します。

下表の表示名は英語です。[コミュニティのステージ一覧](https://www.speedrun.com/hff/levels) と既存のステージ番号資料に沿った参照表です。実装は表の文字列を固定せず、そのゲームの `levels`／`editorPickLevels` を参照します。Store のバージョンや配信状況により、未収録の番号、範囲外の番号、空の枠は拒否されます。全ステージについて Store 実機での収録・起動確認は未実施です。名前指定はゲームに登録された表示名・英語名・内部名を照合します。

| 入力 ID | 英語表示名 | HSRTimer での名前入力例 | 分類 | Store 版での対応 |
| --- | --- | --- | --- | --- |
| 0 | Mansion | `Mansion` / `Intro` | Normal | ゲームに収録されていれば指定可 |
| 1 | Train | `Train` | Normal | ゲームに収録されていれば指定可 |
| 2 | Carry | `Carry` | Normal | ゲームに収録されていれば指定可 |
| 3 | Mountain | `Mountain` | Normal | ゲームに収録されていれば指定可 |
| 4 | Demolition | `Demolition` | Normal | ゲームに収録されていれば指定可 |
| 5 | Castle | `Castle` | Normal | ゲームに収録されていれば指定可 |
| 6 | Water | `Water` | Normal | ゲームに収録されていれば指定可 |
| 7 | Power Plant | `Power Plant` | Normal | ゲームに収録されていれば指定可 |
| 8 | Aztec | `Aztec` | Normal | ゲームに収録されていれば指定可 |
| 9 | Dark | `Dark` | Normal | ゲームに収録されていれば指定可 |
| 10 | Steam | `Steam` | Normal | ゲームに収録されていれば指定可 |
| 11 | Ice | `Ice` | Normal | ゲームに収録されていれば指定可 |
| 12 | Reprise | `Reprise` / `Intro_Reprise` | Normal | 内部シーン `Intro_Reprise` を確認 |
| 100 | Thermal | `Thermal` | Extra | ゲームに収録されていれば指定可 |
| 101 | Factory | `Factory` | Extra | ゲームに収録されていれば指定可 |
| 102 | Golf | `Golf` | Extra | ゲームに収録されていれば指定可 |
| 103 | City | `City` | Extra | ゲームに収録されていれば指定可 |
| 104 | Forest | `Forest` | Extra | ゲームに収録されていれば指定可 |
| 105 | Laboratory | `Laboratory` | Extra | ゲームに収録されていれば指定可 |
| 106 | Lumber | `Lumber` | Extra | ゲームに収録されていれば指定可 |
| 107 | Red Rock | `Red Rock` | Extra | ゲームに収録されていれば指定可 |
| 108 | Tower | `Tower` | Extra | ゲームに収録されていれば指定可 |
| 109 | Miniature | `Miniature` | Extra | ゲームに収録されていれば指定可 |
| 110 | Copper World | `Copper World` | Extra | ゲームに収録されていれば指定可 |
| 111 | Port | `Port` | Extra | ゲームに収録されていれば指定可 |
| 112 | Underwater | `Underwater` | Extra | ゲームに収録されていれば指定可 |
| 113 | Dockyard | `Dockyard` | Extra | ゲームに収録されていれば指定可 |
| 114 | Museum | `Museum` | Extra | ゲームに収録されていれば指定可 |
| 115 | Hike | `Hike` | Extra | ゲームに収録されていれば指定可 |
| 116 | Candyland | `Candyland` | Extra | ゲームに収録されていれば指定可 |
| 117 | Test Chamber | `Test Chamber` | Extra | ゲームに収録されていれば指定可 |
| 118 | Steampunk Party | `Steampunk Party` | Extra | ゲームに収録されていれば指定可 |
| 119 | Viking | `Viking` | Extra | ゲームに収録されていれば指定可 |
| 120 | 10th Anniversary | `10th Anniversary` | Extra | ゲームに収録されていれば指定可 |
| 121 | Jail | `Jail` | Extra | ゲームに収録されていれば指定可 |
| 200 以上 | Steam Workshop の公開 ID | — | Workshop | Store 版の数値指定では対象外 |

固定のリトライ先を使わない場合は **Specify retry level** を OFF にします。ON のまま空欄、負数、範囲外、未収録 ID を指定すると、従来と同じ無効なリトライ先の表示が出て、移動しません。

plcc では絶対指定の空欄・数値として解釈できない入力の場合はリトライを呼びません。符号なし整数として読める未登録番号は、plcc のリトライ入口ではマップ配列の範囲を照合せず、ゲーム側への呼び出しに渡ります。ゲーム側での結果は未検証です。マップパック相対モードには、この添付版のインデックス参照前に範囲チェックがありません。
