#!/usr/bin/env python3
"""Verify Chowayo in a headless game, alone and with Bilingual Spire."""
import hashlib
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--audio", action="store_true", help="Play melody cards in a real combat and record mixer output")
parser.add_argument("--game-dir", type=Path,
                    default=Path.home() / "Library/Application Support/Steam/steamapps/common/Slay the Spire 2",
                    help="Game install directory (also supports external Steam libraries)")
options = parser.parse_args()

root = Path(__file__).resolve().parents[2]
project = Path(__file__).resolve().parent / "Chowayo.csproj"
game_dir = options.game_dir.expanduser().resolve()
game = game_dir / "SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2"
if not game.is_file():
    raise SystemExit(f"Game not found: {game}. Set --game-dir to its install directory.")
if subprocess.run(["pgrep", "-f", str(game)], capture_output=True).returncode == 0:
    raise SystemExit("Close Slay the Spire 2 before running this test.")

def hashes(directory):
    return {str(p.relative_to(directory)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in directory.rglob("*") if p.is_file()} if directory.exists() else {}

data = Path.home() / "Library/Application Support/SlayTheSpire2"
default = data / "default"
marker = default / ".chowayo-test"
if marker.exists() or (default / ".bilingual-spire-test").exists():
    raise SystemExit("Restore the previous interrupted test's backup before continuing.")
artifacts = root / ".artifacts/chowayo"
artifacts.mkdir(parents=True, exist_ok=True)
test_dir = Path(tempfile.mkdtemp(prefix="test-", dir=artifacts))
backup = test_dir / "original-default"
before = hashes(default)
steam_before = hashes(data / "steam")
build = ["dotnet", "build", str(project), f"-p:Sts2Path={game_dir}"]
subprocess.run([*build, "-t:DeployMod"], cwd=root, check=True)

if default.exists():
    default.rename(backup)
temporary_bilingual = None
try:
    settings_dir = default / "1"
    settings_dir.mkdir(parents=True)
    marker.write_text(str(backup))
    local_bilingual = game.parent / "mods/BilingualSpire/BilingualSpire.dll"
    workshop_bilingual = game_dir.parents[1] / "workshop/content/2868840/3840138909"
    if not local_bilingual.exists() and (workshop_bilingual / "BilingualSpire.dll").exists():
        candidate = game.parent / "mods/ChowayoBilingualTest"
        candidate.mkdir()
        temporary_bilingual = candidate
        for name in ("BilingualSpire.dll", "BilingualSpire.json"):
            shutil.copy2(workshop_bilingual / name, temporary_bilingual / name)
    mods = []
    for folder, source in [(game.parent / "mods", "ModsDirectory"), (game.parent / "mods_STEAMTEST", "SteamWorkshop")]:
        if folder.exists():
            for manifest in folder.rglob("*.json"):
                if manifest.name.startswith("._"):
                    continue
                entry = json.loads(manifest.read_text())
                if "id" in entry and "has_dll" in entry:
                    mods.append({"id": entry["id"], "source": source, "is_enabled": False})
    if not any(m["id"] == "BilingualSpire" for m in mods):
        raise RuntimeError("Install Bilingual Spire before testing compatibility.")
    for bilingual in (False, True):
        phase = "bilingual" if bilingual else "standalone"
        for mod in mods:
            mod["is_enabled"] = mod["id"] == "Chowayo" or (bilingual and mod["id"] == "BilingualSpire")
        settings = {
            "schema_version": 8, "language": "jpn", "seen_ea_disclaimer": True, "skip_intro_logo": True,
            "mod_settings": {"mods_enabled": True, "mod_list": mods},
        }
        (settings_dir / "settings.save").write_text(json.dumps(settings))
        env = dict(os.environ, CHOWAYO_SMOKE_TEST="1", CHOWAYO_TEST_BILINGUAL=str(int(bilingual)),
                   BILINGUAL_SPIRE_CONFIG_PATH=str(test_dir / "bilingual-settings.json"))
        if options.audio:
            env["CHOWAYO_TEST_AUDIO"] = "1"
            env["CHOWAYO_AUDIO_RECORDING"] = str(test_dir / f"{phase}-combat-voices.wav")
        for key in ("SteamAppId", "SteamGameId", "BILINGUAL_SPIRE_SMOKE_TEST"):
            env.pop(key, None)
        log = test_dir / f"{phase}.log"
        args = [str(game), "--headless", "--force-steam", "off", "--max-fps", "60",
                "--quit-after", "6000", "--log-file", str(log)]
        with (test_dir / f"{phase}-console.log").open("w") as console:
            result = subprocess.run(args, cwd=game.parent, env=env, stdout=console,
                                    stderr=subprocess.STDOUT, timeout=110)
        output = log.read_text(errors="replace") if log.exists() else ""
        print(f"{phase}: exit code {result.returncode}; {log}", flush=True)
        for line in output.splitlines():
            if "CHOWAYO TEST" in line or "CHOWAYO AUDIO" in line or "CHOWAYO ICON" in line:
                print(line, flush=True)
        if (result.returncode != 0 or "CHOWAYO TEST PASS:" not in output or "CHOWAYO ICON PASS:" not in output or
                (options.audio and "CHOWAYO AUDIO PASS:" not in output) or
                "[ERROR]" in output or "ERROR: System." in output or
                "Steam initialization skipped" not in output or "Steamworks initialization succeeded" in output):
            raise RuntimeError(f"Game verification failed: {log}")
finally:
    if default.exists():
        default.rename(test_dir / "test-default")
    if backup.exists():
        backup.rename(default)
    if temporary_bilingual is not None:
        for name in ("BilingualSpire.dll", "BilingualSpire.json"):
            (temporary_bilingual / name).unlink(missing_ok=True)
        temporary_bilingual.rmdir()
    subprocess.run([*build, "-c", "Release", "-t:DeployMod"], cwd=root, check=True)
    if hashes(default) != before or hashes(data / "steam") != steam_before:
        raise RuntimeError("Save hashes did not match after restoration.")
    print("PASS: Steam saves unchanged; original non-Steam saves restored exactly; Release installed.", flush=True)
