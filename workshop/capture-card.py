#!/usr/bin/env python3
"""Capture the real rare card in an isolated game profile, restoring all user files."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
game = Path.home() / "Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2"
if subprocess.run(["pgrep", "-f", str(game)], capture_output=True).returncode == 0:
    raise SystemExit("Close Slay the Spire 2 before capturing the card.")
installed = game.parent / "mods/Chowayo"
if not (installed / "Chowayo.dll").exists():
    raise SystemExit("Install the tested Chowayo release before capturing the card.")
project = root / "workshop/capture-card/CardCapture.csproj"
subprocess.run(["dotnet", "build", str(project), "-c", "Release"], cwd=root, check=True)
data = Path.home() / "Library/Application Support/SlayTheSpire2"
default = data / "default"
if any((default / marker).exists() for marker in (".chowayo-test", ".bilingual-spire-test", ".chowayo-capture")):
    raise SystemExit("Restore the previous interrupted test before capturing.")

def hashes(path):
    return {str(p.relative_to(path)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in path.rglob("*") if p.is_file()} if path.exists() else {}

before, steam_before, mod_before = hashes(default), hashes(data / "steam"), hashes(installed)
artifacts = root / ".artifacts/chowayo"
artifacts.mkdir(parents=True, exist_ok=True)
work = Path(tempfile.mkdtemp(prefix="card-capture-", dir=artifacts))
backup = work / "original-default"
helper = game.parent / "mods/ChowayoCardCapture"
helper.mkdir()
if default.exists():
    default.rename(backup)
try:
    shutil.copy2(project.parent / "bin/Release/net9.0/ChowayoCardCapture.dll", helper)
    (helper / "ChowayoCardCapture.json").write_text(json.dumps({
        "id": "ChowayoCardCapture", "name": "Chowayo Card Capture", "author": "guppi-tw",
        "version": "v0.1.0", "has_dll": True, "has_pck": False, "affects_gameplay": False,
        "dependencies": [],
    }))
    settings = default / "1"
    settings.mkdir(parents=True)
    (default / ".chowayo-capture").write_text(str(backup))
    mods = []
    for folder, source in [(game.parent / "mods", "ModsDirectory"), (game.parent / "mods_STEAMTEST", "SteamWorkshop")]:
        for path in folder.rglob("*.json"):
            entry = json.loads(path.read_text())
            if "id" in entry and "has_dll" in entry:
                mods.append(dict(id=entry["id"], source=source,
                                 is_enabled=entry["id"] in ("Chowayo", "ChowayoCardCapture")))
    (settings / "settings.save").write_text(json.dumps({
        "schema_version": 8, "language": "jpn", "fullscreen": False,
        "seen_ea_disclaimer": True, "skip_intro_logo": True,
        "mod_settings": {"mods_enabled": True, "mod_list": mods},
    }))
    env = dict(os.environ, CHOWAYO_CARD_CAPTURE=str(work))
    for key in ("SteamAppId", "SteamGameId", "CHOWAYO_SMOKE_TEST", "BILINGUAL_SPIRE_SMOKE_TEST"):
        env.pop(key, None)
    log = work / "capture.log"
    with (work / "console.log").open("w") as console:
        result = subprocess.run([str(game), "--force-steam", "off", "--windowed", "--resolution", "1280x720",
                                 "--max-fps", "60", "--quit-after", "3600", "--log-file", str(log)],
                                cwd=game.parent, env=env, stdout=console, stderr=subprocess.STDOUT, timeout=90)
    output = log.read_text(errors="replace")
    if (result.returncode != 0 or "CHOWAYO CAPTURE PASS:" not in output or
            "CHOWAYO CAPTURE FAIL:" in output or "Steam initialization skipped" not in output):
        raise RuntimeError(f"Capture failed (exit {result.returncode}): {log}")
    for size in (1024, 512):
        image = work / f"chowayo-card-{size}.png"
        assert image.exists() and image.stat().st_size > 1000
    print(f"PASS: game exit 0; native rare-card capture saved to {work}")
finally:
    if default.exists():
        default.rename(work / "capture-default")
    if backup.exists():
        backup.rename(default)
    shutil.rmtree(helper)
    if hashes(default) != before or hashes(data / "steam") != steam_before or hashes(installed) != mod_before:
        raise RuntimeError("Original saves or installed Chowayo files changed")
    print("PASS: original saves and installed Chowayo files unchanged; capture helper removed")
