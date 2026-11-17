Run `python3 workshop/record-demo.py --system-capture --wait-for-start` on macOS with
Slay the Spire 2 closed. Screen recording access, Xcode command-line tools, .NET 9
and FFmpeg are required. The installed Chowayo release is used unchanged.

The temporary helper prepares a Fool combat with upgraded Cho Wah and three
upgraded Cho Wah Yo cards alongside one original starting card. It plays the four
prepared cards consecutively through the normal manual-play action queue, without
added pauses or enlarged previews. Normal costs, effects and victory conditions
apply. The full 4.4-second victory song is included.

Wait for `System recording ready`, bring the running game to the foreground, then
create a file named `start` in the printed recording directory. Do this within
two minutes. Do not open another game instance while the helper is building or
after it has exited. Keeping the game visible lets its native draw loop advance
all animations; forced draws are not used.

ScreenCaptureKit records only the game's window and application audio. This
includes the FMOD BGM and effects as well as the MOD voices; microphone recording
is disabled. The helper and recorder share timestamps for trimming the continuous
1080p/60 FPS footage to the card sequence. No captions or overlays are added.
The output is `.artifacts/chowayo/Chowayo-Fool-gameplay-with-bgm.mp4`.

The helper runs with Steam disabled and an isolated profile. Background muting is
disabled only in that temporary profile. The runner restores the original profile
and checks hashes of saves and installed Chowayo files, even after a failure.
Frame telemetry records actual draws, visibility and Fool animation time; the
runner rejects a recording if native drawing stops. Hover tooltips are hidden.

Raw footage, frame timings and logs remain in
`.artifacts/chowayo/demo-recording-*`. These helpers are not packaged in the
release or uploaded to the Workshop. Use `--realtime-check` for telemetry without
recording. The legacy mode without `--system-capture` uses Godot MovieWriter and
omits FMOD audio, so it is unsuitable for the published gameplay demonstration.
