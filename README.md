# Chowayo チョワヨ

Slay the Spire 2のフールの旋律を「チョ・ワー・ヨ」にする音声MODです。
スピキの「チョ」「ワ」「ヨ」を鳴らし、被ダメージ・死亡・勝利・敗北の声も変更します。

- 「キ」を「ヨ」に変更し、複合カードの名前をチョ→ワー→ヨの順に揃えます。
- 「チョ・ワー」「チョ・ワー・ヨ」は演出と音声もこの順で再生します。
- 旋律はかくれんぼ版の2回目を使用します。勝利時はかぼちゃ版の全編4.4秒を流します。
- 英語は Cho Wah Yo の順に変更します。日本語を含む15言語に対応し、Bilingual Spireとの併用も確認しています。
- カードの効果・数値・内部ID・旋律判定は変えません。
- 音量はゲームのマスター・効果音設定に追従します。

## 導入

[Steam Workshopでサブスクライブ](https://steamcommunity.com/sharedfiles/filedetails/?id=3841064982)

Steam版はサブスクライブ後にダウンロードを待ち、設定 → MOD設定で「チョワヨ」を有効にします。
手動でZIP版を入れている場合は、ゲームを終了して既存の `mods/Chowayo` を別の場所へ移してから切り替えてください。

[音声入りZIPをダウンロード](https://github.com/guppi-tw/sts2-chowayo/releases/download/v0.3.1/Chowayo-v0.3.1-with-audio.zip)

1. SteamでSlay the Spire 2のゲームのバージョンを `public-beta` にします。
2. ゲームを終了し、ZIPを展開します。
3. 展開した `Chowayo` フォルダをゲームの `mods` フォルダに入れます。
4. ゲームを起動し、設定 → MOD設定で「チョワヨ」を有効にします。再起動を求められたら再起動します。

Macではゲームのアプリを右クリック →「パッケージの内容を表示」→ `Contents/MacOS/mods` に配置します。

```text
~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/mods/Chowayo
```

更新時は古い `Chowayo` フォルダを退避してから入れ替えます。
解除する場合も、ゲームを終了してこのフォルダを取り除きます。
[音声なし版](https://github.com/guppi-tw/sts2-chowayo/releases/download/v0.3.1/Chowayo-v0.3.1-no-audio.zip)もあります。音声なし版では演出と音声の順番はゲーム本来のままです。

## 動作確認

2026年11月17日、Mac Apple Silicon・public-beta v0.113.1で確認しています。
単独・Bilingual Spire v0.5.7併用とも実ゲームのヘッドレス検証に成功しました。
11枚のカード表示、15言語の名称、実戦での再生順、音量ミュート、被ダメージ・死亡、勝敗の音声を確認しています。
勝利曲は部屋を移動しても最後まで流れます。Windows・Linux・通常ブランチは未検証です。

## ビルド

Mac版ゲームと.NET 9 SDKが必要です。

```sh
dotnet build mods/Chowayo/Chowayo.csproj -c Release
python3 mods/Chowayo/package.py
```

音声はGitのソース履歴には収録していません。リリースの `Chowayo/audio` にある7つのWAVを `mods/Chowayo/local-audio` に置き、`python3 mods/Chowayo/package.py --with-audio` を実行すると音声入りZIPを作成できます。
実ゲーム検証はゲームを終了してから `python3 mods/Chowayo/smoke-test.py --audio` を実行します。併用検証にはBilingual Spireの導入も必要です。
音声編集HTMLの生成には、開発時に使用したローカルの原音・素材メタデータが別途必要です。

## 音声の出典

[音声素材配布 ｽﾋﾟｷ編 FIRE猫Blog](https://fire-neko-blog.com/sound-supiki/)の音声を使用しています。
[各音声の切り出し範囲と加工内容](mods/Chowayo/docs/third-party-audio.md)を記録しています。
Slay the Spire 2およびトリッカルの非公式ファンMODです。音声の権利はそれぞれの権利者に帰属し、公式の承認を示すものではありません。

## English

Chowayo replaces the Fool's Ki melody with Yo and arranges compound card names as **Cho Wah Yo**.
It includes Speaki voice clips for melodies, damage, death, victory and defeat. Card effects and gameplay melody tags remain unchanged.
The victory cue plays the full 4.4-second pumpkin song. Volume follows the game's master and SFX settings.
Tested on macOS Apple Silicon with public-beta v0.113.1, alone and with Bilingual Spire v0.5.7. Windows, Linux and the stable branch are untested.
Download the voice ZIP from Releases, extract `Chowayo` into the game's `mods` directory, and enable it in Mod Settings.
This is an unofficial fan mod. Third-party audio is credited above and in the included source notes.
