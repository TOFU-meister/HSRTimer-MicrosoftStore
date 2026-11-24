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

## 第10-2段階の実装

設定読み込み後、永続HUDコンポーネントの最初のUpdateで1回だけ、メインスレッドから復元を試します。標準IMGUI選択ではBundleを開きません。タイマー行と警告バナーに外部Fontと保存したウェイトを適用し、サイズはその時点のレイアウトを使用します。実描画成功時は `Saved HUD font drawn` を記録します。

診断での一時適用が優先です。一時適用の解除では保存済みフォントのBundleスロットを残し、他の診断用リソースを解放します。保存と診断で同じBundleなら共通のスロットを使います。ゲーム終了時は共有スロットも解放します。シーン遷移とOne Key Retryで毎回ロードする処理は追加していません。生存監視による無効化後は既存の間隔と上限で再取得し、再取得が成功すれば保存したアセットに結び直します。

パスは `fonts` 直下のファイル名を正規化し、既存のパスポリシーでリンク・ジャンクションを拒否します。実際に開く前に再検証します。UnityのファイルパスAPIとヘッダー検査では同一ハンドルを共有できないため、別プロセスが検査とロードの間に差し替える競合までは排除できません。失敗時は標準IMGUIで続行し、有効な構文の保存値は保持します。

### Windowsでの確認

1. ゲームを終了します。起動ログの `Store data directory is writable` に出たデータフォルダーを使います。既存の `settings.ini` を上書きしない名前にバックアップします。PowerShellで `$ini` に対象のフルパスを設定し、`$backup = "$ini.stage10-2-$([guid]::NewGuid()).bak"`、`Copy-Item -LiteralPath $ini -Destination $backup -ErrorAction Stop` を実行します。初回でINIがなければ、標準設定で一度起動し設定を保存してからバックアップします。
2. 第9段階で独立生成したBundleを `fonts` に置きます。既存の `[HudFont]` セクションだけを上の例で更新します。セクションがなければ追記します。サイズを変える場合は既存HUDのサイズ設定を使います。
3. ゲームを起動し、`Saved HUD font bundle ... ready` と `Saved HUD font drawn` のウェイト・サイズ、表示を確認します。ゲーム終了後に `style = bold` に変更して再確認します。ステージ移動・One Key Retryではフォントが維持され、起動時ロードが繰り返されないことを確認します。
4. 診断で異なるフォントへ一時適用し、**Restore saved HUD font / release probe fonts** で保存済みフォントに戻ることを確認します。診断に同じBundleを読み込ませた場合は既存スロットが使われます。
5. ゲーム終了中に `bundle = missing-test.bundle` または `asset = assets/fonts/missing-test.otf` として起動します。標準IMGUIで表示され、保存値が残ることを確認します。破損を試す場合は、元Bundleとは別名の試験ファイルを使います。
6. 終了後、試験中のINIも必要なら別名に控えてから、`Copy-Item -LiteralPath $backup -Destination $ini -ErrorAction Stop` で復元します。試験中に追加した他の設定を残したい場合は、バックアップから `[HudFont]` セクションだけを戻します。
