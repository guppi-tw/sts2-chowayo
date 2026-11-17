#!/usr/bin/env python3
"""Record a prepared real combat with the installed release, without changing user saves."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import time

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--realtime-check", action="store_true", help="Measure normal gameplay without MovieWriter")
parser.add_argument("--wait-for-start", action="store_true", help="Wait for a start file after foreground inspection")
parser.add_argument("--system-capture", action="store_true", help="Capture normal gameplay with all game audio on macOS")
options = parser.parse_args()
game = Path.home() / "Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2"
if subprocess.run(["pgrep", "-f", str(game)], capture_output=True).returncode == 0:
    raise SystemExit("Close Slay the Spire 2 before recording.")
installed = game.parent / "mods/Chowayo"
if not (installed / "Chowayo.dll").exists():
    raise SystemExit("Install the tested Chowayo release before recording.")
for tool in ("ffmpeg", "ffprobe"):
    if not shutil.which(tool):
        raise SystemExit(f"{tool} is required.")
project = root / "workshop/record-demo/DemoRecording.csproj"
subprocess.run(["dotnet", "build", str(project), "-c", "Release"], cwd=root, check=True)
capture_binary = root / ".artifacts/chowayo/CaptureGame"
if options.system_capture:
    capture_binary.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(["xcrun", "swiftc", "-swift-version", "5", "-parse-as-library",
                    str(project.parent / "CaptureGame.swift"), "-o", str(capture_binary)], check=True)
if subprocess.run(["pgrep", "-f", str(game)], capture_output=True).returncode == 0:
    raise SystemExit("A game instance started during preparation; close it before recording.")
data = Path.home() / "Library/Application Support/SlayTheSpire2"
default = data / "default"
if any((default / marker).exists() for marker in (".chowayo-test", ".bilingual-spire-test", ".chowayo-capture", ".chowayo-recording")):
    raise SystemExit("Restore the previous interrupted test before recording.")


def hashes(path):
    return {str(p.relative_to(path)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in path.rglob("*") if p.is_file()} if path.exists() else {}


before, steam_before, mod_before = hashes(default), hashes(data / "steam"), hashes(installed)
artifacts = root / ".artifacts/chowayo"
artifacts.mkdir(parents=True, exist_ok=True)
work = Path(tempfile.mkdtemp(prefix="demo-recording-", dir=artifacts))
backup = work / "original-default"
helper = game.parent / "mods/ChowayoDemoRecording"
helper.mkdir()
if default.exists():
    default.rename(backup)
print(f"Recording artifacts: {work}", flush=True)
game_process = None
capture_process = None
try:
    shutil.copy2(project.parent / "bin/Release/net9.0/ChowayoDemoRecording.dll", helper)
    (helper / "ChowayoDemoRecording.json").write_text(json.dumps({
        "id": "ChowayoDemoRecording", "name": "Chowayo Demo Recording", "author": "guppi-tw",
        "version": "v0.1.0", "has_dll": True, "has_pck": False, "affects_gameplay": False,
        "dependencies": [],
    }))
    settings = default / "1"
    settings.mkdir(parents=True)
    (default / ".chowayo-recording").write_text(str(backup))
    mods = []
    for folder, source in [(game.parent / "mods", "ModsDirectory"), (game.parent / "mods_STEAMTEST", "SteamWorkshop")]:
        for path in folder.rglob("*.json"):
            entry = json.loads(path.read_text())
            if "id" in entry and "has_dll" in entry:
                mods.append(dict(id=entry["id"], source=source,
                                 is_enabled=entry["id"] in ("Chowayo", "ChowayoDemoRecording")))
    (settings / "settings.save").write_text(json.dumps({
        "schema_version": 8, "language": "jpn", "fullscreen": False,
        "seen_ea_disclaimer": True, "skip_intro_logo": True,
        "mod_settings": {"mods_enabled": True, "mod_list": mods},
    }))
    env = dict(os.environ, CHOWAYO_DEMO_RECORDING=str(work))
    if options.wait_for_start or options.system_capture:
        env["CHOWAYO_RECORD_GATE"] = "1"
    else:
        env.pop("CHOWAYO_RECORD_GATE", None)
    if options.system_capture:
        env["CHOWAYO_SYSTEM_CAPTURE"] = "1"
    else:
        env.pop("CHOWAYO_SYSTEM_CAPTURE", None)
    for key in ("SteamAppId", "SteamGameId", "CHOWAYO_SMOKE_TEST", "BILINGUAL_SPIRE_SMOKE_TEST", "CHOWAYO_CARD_CAPTURE"):
        env.pop(key, None)
    log = work / "recording.log"
    recording_args = (["--max-fps", "60"] if options.realtime_check or options.system_capture else
                      ["--write-movie", str(work / "game.avi"), "--fixed-fps", "60", "--disable-vsync"])
    with (work / "console.log").open("w") as console:
        game_process = subprocess.Popen([
            str(game), "--force-steam", "off", "--windowed", "--resolution", "1280x720",
            *recording_args,
            "--quit-after", "18000", "--log-file", str(log),
        ], cwd=game.parent, env=env, stdout=console, stderr=subprocess.STDOUT)
        def wait_for_file(name, process, timeout=90):
            deadline = time.monotonic() + timeout
            while not (work / name).exists():
                if process.poll() is not None or time.monotonic() >= deadline:
                    raise RuntimeError(f"Process stopped or timed out waiting for {name}: {work}")
                time.sleep(.1)
        if options.system_capture:
            wait_for_file("ready", game_process)
            with (work / "capture-console.log").open("w") as capture_log:
                capture_process = subprocess.Popen([str(capture_binary), str(work), str(game_process.pid)],
                                                    stdout=capture_log, stderr=subprocess.STDOUT)
            wait_for_file("capture-ready.json", capture_process)
            print(f"System recording ready: {work}", flush=True)
            if not options.wait_for_start:
                (work / "start").write_text("System capture ready")
        game_process.wait(timeout=240)
        if capture_process is not None and capture_process.wait(timeout=30) != 0:
            raise RuntimeError(f"System recording failed: {work / 'capture-console.log'}")
    output = log.read_text(errors="replace")
    if (game_process.returncode != 0 or "CHOWAYO DEMO PASS:" not in output or
            "CHOWAYO DEMO FAIL:" in output or "Steam initialization skipped" not in output):
        raise RuntimeError(f"Recording failed (exit {game_process.returncode}): {log}")
finally:
    for process in (capture_process, game_process):
        if process is not None and process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
    if default.exists():
        default.rename(work / "recording-default")
    if backup.exists():
        backup.rename(default)
    shutil.rmtree(helper)
    if hashes(default) != before or hashes(data / "steam") != steam_before or hashes(installed) != mod_before:
        raise RuntimeError("Original saves or installed Chowayo files changed")
    print("PASS: original saves and installed Chowayo files unchanged; recording helper removed", flush=True)

timing = json.loads((work / "timing.json").read_text())
samples = json.loads((work / "frame-timing.json").read_text())
active = [s for s in samples if timing["markers"]["start"] <= s["frame"] <= timing["markers"]["end"]]
draw_ratio = (active[-1]["drawn"] - active[0]["drawn"]) / (active[-1]["frame"] - active[0]["frame"])
wall_fps = (len(active) - 1) / ((active[-1]["usec"] - active[0]["usec"]) / 1_000_000)
print(f"Native draw ratio: {draw_ratio:.3f}; wall-clock gameplay FPS: {wall_fps:.1f}", flush=True)
if draw_ratio < .98 or not all(s["canDraw"] for s in active):
    raise RuntimeError(f"Game drawing was interrupted: {work / 'frame-timing.json'}")
if options.realtime_check and not options.system_capture:
    print(f"PASS: normal gameplay checked without MovieWriter: {work}")
    raise SystemExit(0)
start = timing["markers"]["start"] / timing["fps"]
duration = (timing["markers"]["end"] - timing["markers"]["start"]) / timing["fps"]
movie = work / "game.avi"
output = artifacts / "Chowayo-Fool-gameplay-60fps.mp4"
if options.system_capture:
    capture = json.loads((work / "capture-finished.json").read_text())
    start = timing["utcStart"] - capture["firstFrameUTC"]
    duration = timing["utcEnd"] - timing["utcStart"]
    assert start >= 0 and capture["audioBuffers"] > 10
    movie = work / "game-system.mov"
    output = artifacts / "Chowayo-Fool-gameplay-with-bgm.mp4"
subprocess.run([
    "ffmpeg", "-hide_banner", "-loglevel", "warning", "-y", "-ss", str(start), "-i", str(movie),
    "-t", str(duration), "-map", "0:v:0", "-map", "0:a:0", "-c:v", "libx264", "-crf", "19",
    "-preset", "medium", "-pix_fmt", "yuv420p", "-vf", "fps=60", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", str(output),
], check=True)
subprocess.run(["ffmpeg", "-v", "error", "-i", str(output), "-f", "null", "-"], check=True)
probe = subprocess.run([
    "ffprobe", "-v", "error", "-show_entries", "stream=codec_name,width,height,r_frame_rate:format=duration,size",
    "-of", "json", str(output),
], check=True, capture_output=True, text=True)
print(probe.stdout)
print(f"PASS: recorded and decoded {output}")
