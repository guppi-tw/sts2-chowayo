using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

namespace ChowayoDemoRecording;

[ModInitializer(nameof(Initialize))]
public static class DemoRecording
{
    private const int Fps = 60;
    private static readonly List<(string name, ulong frame)> Voices = [];
    private record FrameSample(ulong frame, ulong usec, int drawn, bool canDraw, string? animation, float? trackTime);

    public static void Initialize()
    {
        if (System.Environment.GetEnvironmentVariable("CHOWAYO_DEMO_RECORDING") is { } folder)
            _ = Record(folder);
    }

    private static async Task Record(string folder)
    {
        var game = NGame.Instance!;
        try
        {
            await game.GameStartupComplete;
            LocManager.Instance.SetLanguage("jpn");
            SaveManager.Instance.PrefsSave.MuteInBackground = false;
            SaveManager.Instance.SettingsSave.VolumeMaster = 1;
            SaveManager.Instance.SettingsSave.VolumeSfx = 1;
            SaveManager.Instance.SettingsSave.LimitFpsInBackground = false;
            // Keep the native draw loop active; forced draws alone do not advance all animations.
            game.GetWindow().AlwaysOnTop = true;
            if (System.Environment.GetEnvironmentVariable("CHOWAYO_SYSTEM_CAPTURE") == "1")
                game.GetWindow().Borderless = true;
            game.GetWindow().GrabFocus();
            OS.LowProcessorUsageMode = false;
            Engine.MaxFps = Fps;
            game.GetViewport().GuiDisableInput = true;
            game.GetTree().ProcessFrame += () =>
            {
                foreach (var tip in game.HoverTipsContainer?.GetChildren().OfType<CanvasItem>() ?? [])
                    tip.Hide();
            };
            game.GetTree().NodeAdded += node =>
            {
                if (node is AudioStreamPlayer && node.Name.ToString().StartsWith("Chowayo_"))
                {
                    Voices.Add((node.Name.ToString()[8..], Engine.GetProcessFrames()));
                    GD.Print($"DEMO VOICE: {node.Name} frame={Engine.GetProcessFrames()}");
                }
            };
            var network = new NetSingleplayerGameService();
            var player = Player.CreateForNewRun<Fool>(UnlockState.all, network.NetId);
            var state = RunState.CreateForTest([player], seed: "CHOWAYODEMO");
            var manager = RunManager.Instance;
            manager.SetUpTest(state, network, shouldSave: false);
            manager.CombatReplayWriter.IsEnabled = false;
            manager.Launch();
            manager.GenerateRooms();
            await PreloadManager.LoadRunAssets([player.Character]);
            await PreloadManager.LoadActAssets(state.Act);
            game.RootSceneContainer.SetCurrentScene(NRun.Create(state));
            await manager.GenerateMap();
            SaveManager.Instance.MarkFtueAsComplete("combat_rules_ftue");
            SaveManager.Instance.MarkFtueAsComplete("shuffle_ftue");
            var encounter = ModelDb.Encounter<ShrinkerBeetleWeak>().ToMutable();
            state.AppendToMapPointHistory(MapPointType.Monster, RoomType.Monster, encounter.Id);
            await manager.EnterRoom(new CombatRoom(encounter, state));
            await Until(() => CombatManager.Instance.IsInProgress, "Combat started");
            await Frames(90);
            NAudioManager.Instance!.SetMasterVol(1);
            NAudioManager.Instance.SetSfxVol(1);
            game.DebugAudio.SetMasterAudioVolume(1);
            await CardPileCmd.RemoveFromCombat(CardPile.Get(PileType.Hand, player)!.Cards.Skip(1).ToArray());
            var cards = new[] { CreateCard("WAH_CHO", true), CreateCard("WAH_CHO_KI", true),
                CreateCard("WAH_CHO_KI", true), CreateCard("WAH_CHO_KI", true) };
            await CardPileCmd.Add(cards, PileType.Hand);
            await Frames(60);
            game.GetWindow().AlwaysOnTop = true;
            if (System.Environment.GetEnvironmentVariable("CHOWAYO_SYSTEM_CAPTURE") == "1")
                game.GetWindow().Borderless = true;
            game.GetWindow().GrabFocus();
            File.WriteAllText(Path.Combine(folder, "ready"), "Ready for foreground check");
            if (System.Environment.GetEnvironmentVariable("CHOWAYO_RECORD_GATE") == "1")
            {
                for (var i = 0; i < 3600 && !File.Exists(Path.Combine(folder, "start")); i++) await Frames(1);
                Require(File.Exists(Path.Combine(folder, "start")), "Recording start gate opened");
            }
            await Until(() => DisplayServer.WindowCanDraw(), "Game window is visible before recording");
            var target = player.Creature.CombatState!.Enemies.Single();
            Require(target.CurrentHp <= 41, "The prepared four-card sequence can win naturally");
            var samples = new List<FrameSample>();
            var actor = NCombatRoom.Instance!.GetCreatureNode(player.Creature)!;
            void Sample()
            {
                using var track = actor.SpineAnimation.GetCurrentTrack();
                samples.Add(new FrameSample(Engine.GetProcessFrames(), Time.GetTicksUsec(),
                    Engine.GetFramesDrawn(), DisplayServer.WindowCanDraw(), track?.GetAnimationName(), track?.GetTrackTime()));
            }
            game.GetTree().ProcessFrame += Sample;
            await Frames(30);
            var markers = new Dictionary<string, ulong> { ["start"] = Engine.GetProcessFrames() };
            var utcStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            await Frames(18);
            await Play(cards[0], target);
            markers["first_melody"] = Engine.GetProcessFrames();
            await Play(cards[1], target);
            markers["second_melody"] = Engine.GetProcessFrames();
            await Play(cards[2], target);
            markers["third_melody"] = Engine.GetProcessFrames();
            await Play(cards[3], target);
            await Until(() => Voices.Any(v => v.name == "victory"), "Natural combat victory plays the song");
            await Frames(144);
            markers["end"] = Engine.GetProcessFrames();
            var utcEnd = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            game.GetTree().ProcessFrame -= Sample;
            File.WriteAllText(Path.Combine(folder, "frame-timing.json"), JsonSerializer.Serialize(samples));
            Require(string.Join(',', Voices.Select(v => v.name)) == "cho,wa,cho,wa,yo,cho,wa,yo,cho,wa,yo,victory", "Actual voice order");
            Require(player.PlayerCombatState!.Energy == 0, "All four cards spent their normal energy cost");
            File.WriteAllText(Path.Combine(folder, "timing.json"), JsonSerializer.Serialize(new
            {
                fps = Fps, markers, utcStart, utcEnd,
                voices = Voices.Select(v => new { v.name, v.frame }),
                cards = cards.Select(c => new { id = c.Id.Entry, title = c.Title }),
                preparedCombat = true,
                captions = false,
                pace = "continuous",
            }));
            GD.Print("CHOWAYO DEMO PASS: four native manual card plays; normal energy costs; natural victory; full song");
            if (System.Environment.GetEnvironmentVariable("CHOWAYO_SYSTEM_CAPTURE") == "1")
            {
                File.WriteAllText(Path.Combine(folder, "capture-stop"), "Combat recording complete");
                await Until(() => File.Exists(Path.Combine(folder, "capture-finished")), "System recording finalized");
            }
            game.RootSceneContainer.SetCurrentScene(new Control());
            manager.CleanUp(graceful: false);
            game.GetTree().Quit(0);

            CardModel CreateCard(string id, bool upgrade)
            {
                var card = player.Creature.CombatState!.CreateCard(ModelDb.AllCards.Single(c => c.Id.Entry == id), player);
                if (upgrade) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
                return card;
            }
        }
        catch (Exception error)
        {
            GD.PrintErr($"CHOWAYO DEMO FAIL: {error}");
            game.GetTree().Quit(1);
        }
    }

    private static async Task Play(CardModel card, Creature target)
    {
        var played = false;
        void OnPlayed() => played = true;
        card.Played += OnPlayed;
        try
        {
            Require(card.TryManualPlay(target), $"Can play {card.Id}");
            await Until(() => played, $"Finished playing {card.Id}");
        }
        finally { card.Played -= OnPlayed; }
    }

    private static async Task Until(Func<bool> condition, string message)
    {
        for (var i = 0; i < 600 && !condition(); i++) await Frames(1);
        Require(condition(), message);
    }

    private static async Task Frames(int count)
    {
        var tree = NGame.Instance!.GetTree();
        for (var i = 0; i < count * Fps / 30; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
