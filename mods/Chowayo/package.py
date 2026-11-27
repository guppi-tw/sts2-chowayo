#!/usr/bin/env python3
"""Build an installable Chowayo ZIP; optionally include the local voice files."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import wave
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--with-audio", action="store_true", help="Include all seven local voice WAVs")
    parser.add_argument("--verified-dll", type=Path, help="Package an existing tested Release DLL instead of rebuilding")
    args = parser.parse_args()
    mod = Path(__file__).resolve().parent
    root = mod.parents[1]
    manifest = json.loads((mod / "Chowayo.json").read_text())
    version = manifest["version"]
    if args.verified_dll is None:
        subprocess.run(["dotnet", "build", str(mod / "Chowayo.csproj"), "-c", "Release"],
                       cwd=root, check=True)
    dll = args.verified_dll or mod / "bin/Release/net9.0/Chowayo.dll"
    if dll.name != "Chowayo.dll" or dll.read_bytes()[:2] != b"MZ":
        raise ValueError("Expected a Chowayo.dll assembly")
    files = {
        "Chowayo/Chowayo.dll": dll.read_bytes(),
        "Chowayo/Chowayo.json": (mod / "Chowayo.json").read_bytes(),
        "Chowayo/icons/speaki.png": (mod / "icons/speaki.png").read_bytes(),
        "Chowayo/docs/third-party-images.md": (mod / "docs/third-party-images.md").read_bytes(),
    }
    if args.with_audio:
        for name in ("cho", "wa", "yo", "hurt", "death", "victory", "defeat"):
            path = mod / "local-audio" / f"{name}.wav"
            with wave.open(str(path)) as wav:
                if wav.getnframes() == 0 or wav.getsampwidth() != 2 or wav.getframerate() != 48000:
                    raise ValueError(f"Invalid PCM16 48kHz voice file: {path}")
            files[f"Chowayo/audio/{name}.wav"] = path.read_bytes()
        files["Chowayo/docs/third-party-audio.md"] = (mod / "docs/third-party-audio.md").read_bytes()
        files["Chowayo/audio-guidelines.md"] = (mod / "audio-guidelines.md").read_bytes()
    flavor = "音声入り" if args.with_audio else "音声なし"
    behavior = (
        "かくれんぼ版のチョ・ワ・ヨ、被ダメージ・死亡・勝敗の音声を収録しています。\n"
        "勝利時はかぼちゃ版の全編4.4秒を再生します。ゲームのマスター・効果音音量に追従します。\n"
        "音声の出典と加工内容は docs/third-party-audio.md に記録しています。"
        if args.with_audio else
        "カード名と説明の名称を変更します。音声と演出の順番はゲーム本来のままです。"
    )
    readme = f"""# チョワヨ {version}

{flavor}。Slay the Spire 2のフール用MODです。
日本語は「チョ・ワー・ヨ」、英語は「Cho Wah Yo」の順にカード名と説明を揃えます。
旋律アイコンは3色のスピキです。チョが青、ワがピンク、ヨがオレンジになります。
カードの効果・数値・旋律判定は変更しません。Bilingual Spireとの併用に対応しています。

{behavior}

画像素材の出典・加工内容は docs/third-party-images.md を参照してください。
元のアイコンに戻す場合は、ゲームを終了して icons フォルダを取り除きます。

## 導入

1. ゲームをpublic-beta v0.114.0以降に更新して終了し、ZIPを展開します。
2. 展開した Chowayo フォルダをゲームの mods フォルダに入れます。
3. ゲームを起動し、設定 → MOD設定で「チョワヨ」を有効にします。
4. 再起動を求められたらゲームを再起動します。

Macの標準配置先は次のとおりです。

`~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/mods/Chowayo`

Steamの「ローカルファイルを閲覧」からゲームのアプリを表示し、右クリック →「パッケージの内容を表示」→ Contents → MacOS と進めます。
mods フォルダがなければ作成します。Chowayo の中にさらに Chowayo を入れないでください。
更新するときは既存の Chowayo フォルダを別の場所に退避してから、新しいフォルダを入れます。
上書きだけで音声なし版に変えると古い audio フォルダが残るため、フォルダごと入れ替えてください。
解除するときはゲームを終了して Chowayo フォルダを取り除きます。

## 確認環境

Mac ARM64版 public-beta v0.114.0で、単独・Bilingual Spire v0.5.7併用を確認しています。
v0.4.1はゲームのHP減少処理の変更に対応しており、ゲームv0.114.0以降が必要です。
Windows・Linuxは未検証です。Bilingual Spire本体は同梱していません。
ゲーム本体のDLL・セーブ・設定ファイルは同梱していません。
"""
    files["Chowayo/README.ja.md"] = readme.encode("utf-8")
    sums = "".join(f"{hashlib.sha256(data).hexdigest()}  {name.removeprefix('Chowayo/')}\n"
                   for name, data in sorted(files.items()))
    files["Chowayo/SHA256SUMS.txt"] = sums.encode("utf-8")
    output = root / ".artifacts/chowayo/packages"
    output.mkdir(parents=True, exist_ok=True)
    suffix = "with-audio" if args.with_audio else "no-audio"
    archive = output / f"Chowayo-{version}-{suffix}.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
        for name, data in sorted(files.items()):
            entry = zipfile.ZipInfo(name, date_time=(2026, 1, 1, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            entry.create_system = 3
            entry.external_attr = 0o100644 << 16
            bundle.writestr(entry, data)
    with zipfile.ZipFile(archive) as bundle:
        if bundle.testzip() is not None or set(bundle.namelist()) != set(files):
            raise RuntimeError(f"ZIP verification failed: {archive}")
        for name, data in files.items():
            if bundle.read(name) != data:
                raise RuntimeError(f"ZIP content mismatch: {name}")
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    archive.with_suffix(".zip.sha256").write_text(f"{digest}  {archive.name}\n")
    print(f"Created and verified {archive} ({archive.stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()
