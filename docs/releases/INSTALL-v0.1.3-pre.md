# HSRTimer Microsoft Store 0.1.3-pre

[日本語](#日本語) · [English](#english) · [简体中文](#简体中文)

## 日本語

Windows x64のHuman: Fall Flat Microsoft Store／Xbox PC版（Unity 2019.4.24f1 IL2CPP）用です。Steam版ではSteam向けのHSRTimerを使ってください。

1. Xboxアプリでゲームの **… → 管理 → ファイル → 参照** を開き、ゲームの `.exe` があるフォルダーを探します。場所はPCによって異なります。[詳しい手順](../../README.md#インストール方法)
2. 初回はゲームを終了し、[BepInEx 6.0.0-be.793 Unity IL2CPP Windows x64](https://builds.bepinex.dev/projects/bepinex_be/793/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.793%2Bdbc82f0.zip) のZIPを展開します。`BepInEx` と `winhttp.dll` が並ぶ階層の中身を、ゲームの `.exe` と同じフォルダーにコピーします。Xboxアプリから一度起動し、メニューが出たら終了します。
3. 公開後、[HSRTimer Releases](https://github.com/TOFU-meister/HSRTimer-MicrosoftStore/releases) の **Assets** から `HSRTimer-v0.1.3-pre.dll` を入手し、`BepInEx\plugins` 直下に置きます。フォルダーがなければ作ります。
4. ゲームを起動して **Home**（初期設定）を押します。更新時はゲームを閉じ、`plugins` のサブフォルダーも調べて旧HSRTimer DLLをすべて外し、新版を1つだけ置きます。設定・レイアウト・PBは削除せず、そのまま使えます。
5. 入力表示も使う場合は対応するMicrosoft Store版[TwilightInputOverlay Releases](https://github.com/TOFU-meister/TwilightInputOverlay-MicrosoftStore/releases)のDLLを同じ場所に1つ置き、HSRTimer設定の **Input Overlay** タブを開きます。併用実機確認はOverlay `0.1.2-pre` で行いました。

**任意のHUDフォント** — 本体はフォントパックなしで使えます。公開後に `HSRTimer-MicrosoftStore-FontPack-v1.0.zip` を展開し、**About → Open Data Folder** で開く保存先の `fonts` に、ZIP内の `.bundle` 5件をコピーします。`fonts` がなければ作り、同名ファイルはバックアップしてから置き換えます。**Interface → Timer HUD → HUDフォント → フォント一覧を再読み込み** で選び、「太さ: 標準／太字」を指定して **適用して保存**。戻すときは **標準（IMGUI）→ 適用して保存** を選び、ファイルを外すならゲームを終了してパックのBundleだけを削除します。

**PBの自動バックアップ** — **General → PBの自動バックアップ** で有効にできます。初期値はOFFです。ONでは既存のILサブセグメントPBとマーカーPBが更新された際、更新前の記録を残すため、保存容量が増える場合があります。OFFにしても既存バックアップは残り、通常のPB保存・読込も使えます。マーカー編集時のバックアップは別扱いです。

## English

For Human: Fall Flat Microsoft Store / Xbox PC on Windows x64 (Unity 2019.4.24f1 IL2CPP). Use the Steam edition of HSRTimer for the Steam game.

1. In the Xbox app, open the game's **… → Manage → Files → Browse** and find the folder with the game executable. [Detailed setup with folder examples](../../README.md#インストール方法)
2. For a first install, close the game and extract [BepInEx 6.0.0-be.793 Unity IL2CPP Windows x64](https://builds.bepinex.dev/projects/bepinex_be/793/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.793%2Bdbc82f0.zip). Copy everything from the level containing `BepInEx` and `winhttp.dll` beside the game executable. Start the game once from Xbox and exit when the menu appears.
3. Once published, download `HSRTimer-v0.1.3-pre.dll` from **Assets** in [HSRTimer Releases](https://github.com/TOFU-meister/HSRTimer-MicrosoftStore/releases). Put it directly in `BepInEx\plugins`; create that folder if needed.
4. Launch and press **Home** (the default key). To update, close the game, remove older HSRTimer DLLs including copies in subfolders, and leave one new DLL. Keep your existing settings, HUD layout and PB files.
5. For the input display, put one compatible Microsoft Store DLL from [TwilightInputOverlay Releases](https://github.com/TOFU-meister/TwilightInputOverlay-MicrosoftStore/releases) in the same folder. Open **Input Overlay** in HSRTimer settings. Windows testing used Overlay `0.1.2-pre`.

**Optional HUD fonts** — HSRTimer works without the pack. Extract `HSRTimer-MicrosoftStore-FontPack-v1.0.zip` when available. Use **About → Open Data Folder**, then copy the five `.bundle` files into the data folder's `fonts` folder. Create it if needed and back up matching files before replacing them. Go to **Interface → Timer HUD → HUD Font → Reload font list**. Pick a font and **Weight: Normal** or **Weight: Bold**, then choose **Apply & Save**. To go back, choose **Default (IMGUI) → Apply & Save**. Close the game before removing the pack's files.

**Automatic PB Backup** — Turn it on in **General → Automatic PB Backup**. It is off by default. When enabled, updates to existing IL subsegment and marker PBs can retain the previous records and use more disk space. Turning it off keeps existing backups and regular PB saving/loading. Marker editing has its own backup behavior.

## 简体中文

适用于 Windows x64 上的 Human: Fall Flat Microsoft Store／Xbox PC 版（Unity 2019.4.24f1 IL2CPP）。Steam 版游戏请使用 Steam 对应的 HSRTimer。

1. 在 Xbox 应用中打开游戏的 **… → 管理 → 文件 → 浏览**，找到游戏 `.exe` 所在的文件夹。[详细安装说明](../../README.md#インストール方法)
2. 首次安装时，退出游戏，解压 [BepInEx 6.0.0-be.793 Unity IL2CPP Windows x64](https://builds.bepinex.dev/projects/bepinex_be/793/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.793%2Bdbc82f0.zip)。将 `BepInEx` 和 `winhttp.dll` 所在那一层的全部内容复制到游戏 `.exe` 旁边。从 Xbox 启动一次游戏，看到菜单后退出。
3. 发布后，从 [HSRTimer Releases](https://github.com/TOFU-meister/HSRTimer-MicrosoftStore/releases) 的 **Assets** 下载 `HSRTimer-v0.1.3-pre.dll`，直接放入 `BepInEx\plugins`。如果没有该文件夹，请新建。
4. 启动游戏，按 **Home**（默认键）打开设置。更新时先退出游戏，移除旧 HSRTimer DLL，也检查子文件夹，最后只保留一个新版 DLL。原有设置、布局和 PB 文件可以保留。
5. 如需输入显示，从 [TwilightInputOverlay Releases](https://github.com/TOFU-meister/TwilightInputOverlay-MicrosoftStore/releases) 获取兼容的 Microsoft Store 版 DLL，在同一个文件夹放置一份，然后在 HSRTimer 设置打开 **Input Overlay**。Windows 实机搭配验证使用了 Overlay `0.1.2-pre`。

**可选 HUD 字体** — 安装 HSRTimer 无需字体包。发布后解压 `HSRTimer-MicrosoftStore-FontPack-v1.0.zip`。打开 **关于 → Open Data Folder**，把五个 `.bundle` 文件放入数据目录下的 `fonts` 文件夹。如果没有该目录，请新建；遇到同名文件请先备份。打开 **界面 → 计时器HUD → HUD 字体 → 重新加载字体列表**，选择字体和「字重: 常规／粗体」，点击 **应用并保存**。恢复默认请选 **默认 (IMGUI) → 应用并保存**，退出游戏后再移除字体包文件。

**自动备份 PB** — 在 **常规 → 自动备份 PB** 中启用，默认关闭。开启后，更新已有的 IL 分段 PB 和标记 PB 时可保留旧记录，可能占用更多空间。关闭后，已有备份仍会保留，PB 可正常保存和读取。标记编辑的备份行为独立。
