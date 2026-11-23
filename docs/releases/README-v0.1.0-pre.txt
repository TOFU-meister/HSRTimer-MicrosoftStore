HSRTimer Microsoft Store 0.1.0-pre
初回テスト版の入れ方

このタイマーは、Windows の Human: Fall Flat の Microsoft Store / Xbox PC 版専用です。
Steam 版には入れないでください。

1. ゲームを完全に終了します。
2. BepInEx 6.0.0-be.793 の Unity IL2CPP Windows x64 版を別に入れます。
   BepInEx はこの ZIP に入っていません。次のページの「インストールと更新」を見てください。
   https://github.com/TOFU-meister/HSRTimer-MicrosoftStore
3. この ZIP を解凍します。HSRTimer.dll を次の場所に入れます。
   BepInEx/plugins/HSRTimer-MicrosoftStore/HSRTimer.dll
   HSRTimer-MicrosoftStore フォルダーがなければ作ってください。
   前の Store 版を使っていた場合は、ゲームを閉じたまま DLL を入れ替えます。
4. ゲームを起動し、キーボードの Home キーを押すと設定を開けます。

設定と自己ベスト（PB）は、ふつう「ドキュメント」の
HSRTimer-MicrosoftStore フォルダーに保存されます。
書き込めない場合は LocalAppData に保存されます。
設定の About → Open Data Folder を押すと、
今使っている保存フォルダーがエクスプローラーで開きます。

外したいとき
ゲームを終了し、ステップ3で入れた HSRTimer.dll を取り除きます。
設定や PB を残したい場合は、保存フォルダーをそのまま残してください。

うまく動かないとき
https://github.com/TOFU-meister/HSRTimer-MicrosoftStore/issues
に、使ったバージョン（0.1.0-pre）、起きたこと、起きるまでにした操作を書いてください。
ログはゲームの BepInEx/LogOutput.log にあります。
共有する前に、ログにある Windows のユーザー名などの個人情報を伏せてください。

原作 HSRTimer: TwilightCup / MIT License（LICENSE を参照）
