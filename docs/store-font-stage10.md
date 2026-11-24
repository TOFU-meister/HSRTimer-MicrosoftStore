# 第10-1段階 — HUDフォント設定の保存基盤

Store版の設定は通常 Documents 配下の `HSRTimer-MicrosoftStore` にあります。Documents を使用できない場合は既存の LocalAppData フォールバックを使います。HUDフォントの選択は `ConfigService` が読み書きする `settings.ini` の `[HudFont]` に保存します。サイズは Interface → Timer HUD の既存設定で、`layout.ini` の `[text]` にある `font_size` を使用します（初期値18、UI範囲8–72）。

```ini
[HudFont]
mode = external
bundle = notosanscjkjp-regular-embedded.bundle
asset = assets/fonts/notosanscjkjp-regular.otf
style = normal
```

`bundle` は `fonts` 直下のファイル名、`asset` はUnityが返すBundle内部の名前です。内部名のスラッシュは識別子の一部です。リセット後は `mode = imgui` となり、外部識別子が空になります。古い設定でセクションがない場合、不明なmode、無効な識別子は標準IMGUIになります。外部選択でstyleがない場合や認識できない場合はNormalです。構文上有効な選択はファイルが未配置でも保持します。第9段階の診断画面で選んだFontやサイズはそのセッション中だけ有効です。

## 第10-2段階の復元設計

1. 保存された選択を設定読み込み後に取得します。Unityオブジェクトの生成・切り替えはUnityのメインスレッドで行います。
2. 外部選択のときに限って、検証済みのファイル名を既存の `fonts` ルートに結合します。ルートと候補を `StoreDataPolicy.EnsureSafePath` で検査して、実際に開く直前にもreparse pointを拒否します。Unityが列挙した内部名と一致するFontを既存のBundleロード処理で選びます。
3. 読み込み済みのBundleスロットを再利用し、上限と所有権を既存のBundle管理に集約します。同じBundleを診断と復元が重複ロードしない設計にします。描画には既存のHUDサイズと保存されたNormal/Boldを使用します。
4. 未配置・不明なアセット・破損では標準IMGUIで起動し、保存された選択は残します。回復は操作または制限付きの既存の復旧処理で行い、毎フレーム試行しません。診断の一時適用を優先し、解除後は永続設定に基づく通常表示に戻す設計とします。持続的な標準フォントへの変更は明示リセットと既存の保存操作で行います。
