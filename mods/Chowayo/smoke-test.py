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
parser.add_argument("--mod-settings", type=Path, help="Reproduce enabled mods and load order from this settings.save in the isolated test profile")
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

def ignore_missing_sidecar(function, path, error):
    if not isinstance(error[1], FileNotFoundError):
        raise error[1]

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
(test_dir / "original-save-hashes.json").write_text(json.dumps({"default": before, "steam": steam_before}))
build = ["dotnet", "build", str(project), f"-p:Sts2Path={game_dir}"]
subprocess.run([*build, "-t:DeployMod"], cwd=root, check=True)

if default.exists():
    default.rename(backup)
temporary_bilingual = None
temporary_installed = None
try:
    settings_dir = default / "1"
    settings_dir.mkdir(parents=True)
    marker.write_text(str(backup))
    local_bilingual = game.parent / "mods/BilingualSpire/BilingualSpire.dll"
    workshop_bilingual = game_dir.parents[1] / "workshop/content/2868840/3840138909"
    configured_mods = None
    if options.mod_settings:
        configured_mods = [m for m in json.loads(options.mod_settings.read_text())["mod_settings"]["mod_list"] if m["is_enabled"]]
        configured_ids = {m["id"] for m in configured_mods}
        if "Chowayo" not in configured_ids:
            raise RuntimeError("Chowayo must be enabled in the supplied mod settings.")
        candidate = game.parent / "mods/ChowayoInstalledTest"
        candidate.mkdir()
        temporary_installed = candidate
        for item in workshop_bilingual.parent.iterdir():
            if not item.is_dir():
                continue
            for manifest in item.rglob("*.json"):
                if manifest.name.startswith("._"):
                    continue
                entry = json.loads(manifest.read_text(encoding="utf-8-sig"))
                if isinstance(entry, dict) and entry.get("id") in configured_ids - {"Chowayo"}:
                    shutil.copytree(item, candidate / item.name, ignore=shutil.ignore_patterns("._*"))
                    break
    elif not local_bilingual.exists() and (workshop_bilingual / "BilingualSpire.dll").exists():
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
                entry = json.loads(manifest.read_text(encoding="utf-8-sig"))
                if "id" in entry and "has_dll" in entry:
                    mods.append({"id": entry["id"], "source": source, "is_enabled": False})
    if not any(m["id"] == "BilingualSpire" for m in mods):
        raise RuntimeError("Install Bilingual Spire before testing compatibility.")
    phases = [("standalone", False), ("bilingual", True)]
    if configured_mods is not None:
        phases = [("installed", "BilingualSpire" in configured_ids)]
        order = {m["id"]: i for i, m in enumerate(configured_mods)}
        missing = configured_ids - {m["id"] for m in mods}
        if missing:
            raise RuntimeError(f"Enabled mods were not found: {sorted(missing)}")
        mods.sort(key=lambda m: order.get(m["id"], len(order)))
    for phase, bilingual in phases:
        for mod in mods:
            mod["is_enabled"] = (mod["id"] in configured_ids if configured_mods is not None else
                                 mod["id"] == "Chowayo" or (bilingual and mod["id"] == "BilingualSpire"))
        settings = {
            "schema_version": 8, "language": "jpn", "seen_ea_disclaimer": True, "skip_intro_logo": True,
            "mod_settings": {"mods_enabled": True, "mod_list": mods},
        }
        (settings_dir / "settings.save").write_text(json.dumps(settings))
        bilingual_config = test_dir / f"{phase}-bilingual-settings.json"
        bilingual_config.write_text(json.dumps({"First": "eng", "Second": "jpn", "Mode": "Second"}))
        env = dict(os.environ, CHOWAYO_SMOKE_TEST="1", CHOWAYO_TEST_BILINGUAL=str(int(bilingual)),
                   BILINGUAL_SPIRE_CONFIG_PATH=str(bilingual_config))
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
    if temporary_installed is not None:
        shutil.rmtree(temporary_installed, onerror=ignore_missing_sidecar)
    subprocess.run([*build, "-c", "Release", "-t:DeployMod"], cwd=root, check=True)
    if hashes(default) != before or hashes(data / "steam") != steam_before:
        raise RuntimeError("Save hashes did not match after restoration.")
    print("PASS: Steam saves unchanged; original non-Steam saves restored exactly; Release installed.", flush=True)
