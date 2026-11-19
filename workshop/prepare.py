#!/usr/bin/env python3
"""Stage a checked release ZIP for the official STS2 Workshop uploader."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("archive", type=Path)
parser.add_argument("--visibility", choices=["private", "public"], default="private")
parser.add_argument("--change-note", default="Initial release v0.3.1 with Speaki voices")
args = parser.parse_args()
expected = args.archive.with_suffix(".zip.sha256").read_text().split()[0]
assert hashlib.sha256(args.archive.read_bytes()).hexdigest() == expected, "ZIP checksum mismatch"
stage = root / ".artifacts/workshop/Chowayo"
content = stage / "content"
allowed = {"Chowayo.dll", "Chowayo.json", "README.ja.md", "SHA256SUMS.txt", "audio-guidelines.md", "docs/third-party-audio.md", "icons/speaki.png", "docs/third-party-images.md"}
allowed.update(f"audio/{name}.wav" for name in ("cho", "wa", "yo", "hurt", "death", "victory", "defeat"))
with zipfile.ZipFile(args.archive) as z:
    assert set(z.namelist()) == {"Chowayo/" + name for name in allowed}, "Unexpected release content"
    files = {name: z.read("Chowayo/" + name) for name in allowed}
manifest = json.loads(files["Chowayo.json"])
assert manifest["id"] == "Chowayo"
for line in files["SHA256SUMS.txt"].decode().splitlines():
    digest, name = line.split("  ", 1)
    assert hashlib.sha256(files[name]).hexdigest() == digest, name
if content.exists():
    assert all(p.relative_to(content).as_posix() in allowed for p in content.rglob("*") if p.is_file()), "Unexpected staged files"
for name, data in files.items():
    path = content / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
source = root / "workshop"
image = source / "image.png"
assert image.stat().st_size < 1_000_000 and image.read_bytes()[:8] == b"\x89PNG\r\n\x1a\n"
shutil.copy2(image, stage / "image.png")
metadata = json.loads((source / "metadata.json").read_text())
metadata.update(description=(source / "description.bbcode").read_text().replace("@MOD_VERSION@", manifest["version"].removeprefix("v")), visibility=args.visibility, changeNote=args.change_note)
(stage / "workshop.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n")
item_id = source / "item-id.txt"
saved = stage / "mod_id.txt"
if item_id.exists():
    value = item_id.read_text().strip()
    assert value.isdecimal() and value != "3840138909", "Invalid Chowayo item ID"
    assert not saved.exists() or saved.read_text().strip() == value, "Different Workshop item"
    saved.write_text(value + "\n")
print(f"Prepared {manifest['version']} ({args.visibility}): {stage}")
