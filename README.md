# Chowayo チョワヨ

Slay the Spire 2のフールの旋律を「チョ・ワー・ヨ」にする音声・アイコンMODです。
スピキの「チョ」「ワ」「ヨ」を鳴らし、被ダメージ・死亡・勝利・敗北の声も変更します。

v0.4.1はゲームのpublic-beta v0.114.0に対応しています。更新後にMODを読み込めなくなる問題を修正しました。

v0.4.0では弓の上の旋律アイコンもスピキに変更しました。チョは青、ワはピンク、ヨはオレンジです。
元の出現・移動アニメーションと、カーソルを合わせたときの説明はそのままです。

![3色のスピキ旋律アイコンを表示した戦闘画面](https://github.com/guppi-tw/sts2-chowayo/releases/download/v0.4.0/chowayo-icons-gameplay.jpg)

[![フールをスピキ化するチョワヨMODの紹介動画](https://github.com/guppi-tw/sts2-chowayo/releases/download/v0.3.1/thumbnail-v3.jpg)](https://youtu.be/rM--4ii0n3A)

[紹介動画を見る](https://youtu.be/rM--4ii0n3A) — 約55秒のずんだもん解説。旋律・勝利・敗北ボイスをBGM付きの実演で紹介します。
動画はアイコン追加前のv0.3.1です。
[紹介動画のMP4をダウンロード](https://github.com/guppi-tw/sts2-chowayo/releases/download/v0.3.1/Chowayo-Zundamon-introduction-v3.mp4)

- 「キ」を「ヨ」に変更し、複合カードの名前をチョ→ワー→ヨの順に揃えます。
- 「チョ・ワー」「チョ・ワー・ヨ」は演出と音声もこの順で再生します。
- 旋律はかくれんぼ版の2回目を使用します。勝利時はかぼちゃ版の全編4.4秒を流します。
- 英語は Cho Wah Yo の順に変更します。日本語を含む15言語に対応し、Bilingual Spireとの併用も確認しています。
- カードの効果・数値・内部ID・旋律判定は変えません。
- 音量はゲームのマスター・効果音設定に追従します。

[実戦動画を見る](https://youtu.be/rhplrlj1ZcQ) — BGM・効果音・スピキ音声入り、1080p・60fps。
[MP4をダウンロード](https://github.com/guppi-tw/sts2-chowayo/releases/download/v0.3.1/Chowayo-Fool-gameplay-with-bgm.mp4)

## 導入

[Steam Workshopでサブスクライブ](https://steamcommunity.com/sharedfiles/filedetails/?id=3841064982)

Steam版はサブスクライブ後にダウンロードを待ち、設定 → MOD設定で「チョワヨ」を有効にします。
手動でZIP版を入れている場合は、ゲームを終了して既存の `mods/Chowayo` を別の場所へ移してから切り替えてください。

[音声・アイコン入りZIPをダウンロード](https://github.com/guppi-tw/sts2-chowayo/releases/download/v0.4.1/Chowayo-v0.4.1-with-audio.zip)

1. SteamでSlay the Spire 2のゲームのバージョンを `public-beta` にし、v0.114.0以降へ更新します。
2. ゲームを終了し、ZIPを展開します。
3. 展開した `Chowayo` フォルダをゲームの `mods` フォルダに入れます。
4. ゲームを起動し、設定 → MOD設定で「チョワヨ」を有効にします。再起動を求められたら再起動します。

Macではゲームのアプリを右クリック →「パッケージの内容を表示」→ `Contents/MacOS/mods` に配置します。

```text
~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/mods/Chowayo
```

更新時は古い `Chowayo` フォルダを退避してから入れ替えます。
解除する場合も、ゲームを終了してこのフォルダを取り除きます。
[音声なし版](https://github.com/guppi-tw/sts2-chowayo/releases/download/v0.4.1/Chowayo-v0.4.1-no-audio.zip)もあります。こちらも3色のアイコンを含み、演出と音声の順番はゲーム本来のままです。
元の旋律アイコンに戻す場合は、ゲームを終了して `Chowayo/icons` フォルダを取り除きます。

## 動作確認

2026年11月27日、Mac Apple Silicon・public-beta v0.114.0でv0.4.1を確認しました。
単独・Bilingual Spire v0.5.7併用の両方で、カード表示、15言語、3色アイコン、8回のカード使用、被弾・死亡・勝敗ボイスの実ゲーム検証が終了コード0でした。
ダメージ予測では声が鳴らず、実際のHP減少時に1回だけ鳴ります。変更された「ワー・ヨ」の捨て札選択と「ヨ・ヴァース」の対象も検証しています。
既存セーブはハッシュ一致で復元済みです。v0.4.1はゲームv0.114.0以降が対象です。

2026年11月20日、v0.4.0の旋律アイコンを同じMac環境で確認しました。
単独・二言語MOD併用で3色の画像、透過、元の表示範囲・旋律ID、出現・移動アニメーションを検証済みです。
実際の戦闘でも表示と勝利までの動作を確認しています。既存セーブはテスト後にハッシュ一致で復元しました。

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
外付けドライブなどのSteamライブラリを使う場合は、`--game-dir "/path/to/Slay the Spire 2"` でインストール先を指定できます。通常のビルドでは `-p:Sts2Path="/path/to/Slay the Spire 2"` を指定するか、`local.props` に設定します。
音声編集HTMLの生成には、開発時に使用したローカルの原音・素材メタデータが別途必要です。

## 素材の出典

[音声素材配布 ｽﾋﾟｷ編 FIRE猫Blog](https://fire-neko-blog.com/sound-supiki/)の音声を使用しています。
[各音声の切り出し範囲と加工内容](mods/Chowayo/docs/third-party-audio.md)を記録しています。
Slay the Spire 2およびトリッカルの非公式ファンMODです。音声の権利はそれぞれの権利者に帰属し、公式の承認を示すものではありません。

紹介動画のナレーションは VOICEVOX:ずんだもん、スピキ画像は [MASIN MAROの配布素材](https://x.com/MasinMaro/status/2016741081229177203)（トリッカル / Trickcal）を使用しています。
旋律アイコンも同じ配布素材をもとにした透過画像です。[画像の出典と加工内容](mods/Chowayo/docs/third-party-images.md)を記録しています。

## English

Chowayo replaces the Fool's Ki melody with Yo and arranges compound card names as **Cho Wah Yo**.
It includes Speaki voice clips for melodies, damage, death, victory and defeat. Card effects and gameplay melody tags remain unchanged.
Version 0.4.0 also replaces the melody icons beside the bow with Speaki faces: blue for Cho, pink for Wah, and orange for Yo. Native spawn/movement animations and hover descriptions are preserved. Both downloads include the icons.
The victory cue plays the full 4.4-second pumpkin song. Volume follows the game's master and SFX settings.
Version 0.4.1 fixes loading on public-beta v0.114.0 after the game's HP-loss API changed. Damage previews remain silent; actual HP loss plays the hurt voice once. Requires game v0.114.0 or later.
Tested on macOS Apple Silicon with public-beta v0.114.0, alone and with Bilingual Spire v0.5.7. Windows, Linux and the stable branch are untested.
Download the voice ZIP from Releases, extract `Chowayo` into the game's `mods` directory, and enable it in Mod Settings.
This is an unofficial fan mod. Third-party audio is credited above and in the included source notes.
