# Steam Workshop

[Chowayo チョワヨ — Speaki Voice Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3841064982)

`item-id.txt` に公開項目IDを保存しています。更新するときも同じIDを使います。
内容はGitHub Releasesの音声入りZIPと同一です。音声なし版はGitHub Releasesから取得できます。

```sh
python3 workshop/prepare.py .artifacts/chowayo/packages/Chowayo-v0.3.1-with-audio.zip --visibility public --change-note 'Update to v0.3.1'
```

次にMega Crit公式のSTS2 Mod Uploaderを、Steamへログインした状態で実行します。

```sh
./ModUploader upload -w /absolute/path/to/sts2-chowayo/.artifacts/workshop/Chowayo
```

`prepare.py` はZIPのハッシュ、同梱ファイル、画像のサイズ、保存済み項目IDを確認します。
アップロードの終了コード・成功ログ・公開状態を確認し、Steamから取得した内容とリリースZIPを比較してください。

サムネイルは実ゲームのレアカード「チョ・ワー・ヨ」を撮影した512×512画像です。出典と再撮影手順は [image-source.md](image-source.md) に記録しています。

2026年11月17日、公式アップローダーが終了コード0で成功し、Steam APIで公開状態を確認しました。
未ログインのブラウザでも説明とサブスクライブボタンを表示できました。
GitHubから再取得したZIPとSteamからダウンロードした全13ファイルがバイト単位で一致しました。DLLと7音声は実ゲーム検証済みの配布元とも一致しています。
確認用のSteamダウンロードでサブスクライブ状態は変更していません。

2026年11月17日にサムネイルを実カードの画像に変更しました。撮影プログラムは終了コード0で完了し、既存セーブ・導入済みMODのハッシュ一致を確認しています。
