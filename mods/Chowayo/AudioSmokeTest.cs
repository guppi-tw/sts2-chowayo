#if DEBUG
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;

namespace Chowayo;

internal static class AudioSmokeTest
{
    internal static async Task Run()
    {
        Require(MelodyAudio.Enabled, "All three local voice files loaded");
        var game = NGame.Instance!;
        var network = new NetSingleplayerGameService();
        var player = Player.CreateForNewRun<Fool>(UnlockState.all, network.NetId);
        var state = RunState.CreateForTest([player], seed: "CHOWAYOAUDIO");
        var manager = RunManager.Instance;
        manager.SetUpTest(state, network, shouldSave: false);
        manager.CombatReplayWriter.IsEnabled = false;
        manager.Launch();
        manager.GenerateRooms();
        await PreloadManager.LoadRunAssets([player.Character]);
        await PreloadManager.LoadActAssets(state.Act);
        game.RootSceneContainer.SetCurrentScene(NRun.Create(state));
        var bus = AudioServer.BusCount;
        AudioServer.AddBus();
        AudioServer.SetBusName(bus, "ChowayoCapture");
        var capture = new AudioEffectCapture { BufferLength = 30 };
        AudioServer.AddBusEffect(bus, capture);
        MelodyAudio.TestBus = "ChowayoCapture";
        try
        {
            await manager.GenerateMap();
            SaveManager.Instance.MarkFtueAsComplete("combat_rules_ftue");
            var encounter = ModelDb.Encounter<BattlewornDummyEventV3Encounter>().ToMutable();
            state.AppendToMapPointHistory(MapPointType.Monster, RoomType.Monster, encounter.Id);
            await manager.EnterRoom(new CombatRoom(encounter, state));
            for (var i = 0; i < 200 && !CombatManager.Instance.IsInProgress; i++) await Wait(.05);
            Require(CombatManager.Instance.IsInProgress, "Real combat started");
            await Wait(.6);
            NAudioManager.Instance!.SetMasterVol(1);
            NAudioManager.Instance.SetSfxVol(1);
            capture.ClearBuffer();
            foreach (var (id, expected) in new[]
            {
                ("CHO_FIRM", "cho"), ("WAH_TOUCH", "wa"), ("KI_HOP", "yo"),
                ("WAH_CHO", "cho,wa"), ("CHO_KI", "cho,yo"), ("WAH_KI", "wa,yo"),
                ("WAH_CHO_KI", "cho,wa,yo"), ("WAH_TOUCH", "wa"),
            })
            {
                MelodyAudio.Played.Clear();
                var card = player.Creature.CombatState!.CreateCard(ModelDb.AllCards.First(c => c.Id.Entry == id), player);
                var originalTags = card.Tags.ToArray();
                CardModel? selectedDiscard = null;
                IDisposable? selection = null;
                if (id == "WAH_KI")
                {
                    var discard = CardPile.Get(PileType.Discard, player)!.Cards;
                    Require(discard.Count >= 2, "Wah Ki has multiple discard choices");
                    selectedDiscard = discard[0];
                    var selector = new TestCardSelector();
                    selector.PrepareToSelect(new[] { selectedDiscard });
                    selection = manager.CardSelectorStack.PushSelector(player, selector);
                }
                using (selection)
                    await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), card,
                        card.TargetType == TargetType.AnyEnemy ? player.Creature.CombatState!.Enemies.First() : null);
                if (selectedDiscard is not null)
                    Require(ReferenceEquals(CardPile.Get(PileType.Draw, player)!.Cards[0], selectedDiscard),
                        "Wah Ki puts the selected discard on top of the draw pile");
                await Wait(.45);
                Require(string.Join(',', MelodyAudio.Played) == expected,
                    $"{id} sound order: {string.Join(',', MelodyAudio.Played)} != {expected}");
                Require(card.Tags.SequenceEqual(originalTags), $"{id} gameplay tags preserved");
                Require(!MelodyAudio.ReorderCompound.Value, $"{id} async scope restored");
                ModEntry.Logger.Info($"CHOWAYO AUDIO CARD: {id} -> {expected}");
            }
            var pcm = capture.GetBuffer(capture.GetFramesAvailable());
            Require(pcm.Length > 1000 && pcm.Any(s => Math.Abs(s.X) > .001f), "Audio mixer produced non-silent PCM during real card plays");
            if (System.Environment.GetEnvironmentVariable("CHOWAYO_AUDIO_RECORDING") is { } output)
            {
                var data = new byte[pcm.Length * 4];
                for (var i = 0; i < pcm.Length; i++)
                {
                    BitConverter.GetBytes((short)(Math.Clamp(pcm[i].X, -1, 1) * 32767)).CopyTo(data, i * 4);
                    BitConverter.GetBytes((short)(Math.Clamp(pcm[i].Y, -1, 1) * 32767)).CopyTo(data, i * 4 + 2);
                }
                using var wav = new AudioStreamWav { Data = data, Format = AudioStreamWav.FormatEnum.Format16Bits,
                    MixRate = (int)AudioServer.GetMixRate(), Stereo = true };
                Require(wav.SaveToWav(output) == Error.Ok, "Saved real combat voice recording");
            }
            foreach (var master in new[] { false, true })
            {
                if (master) NAudioManager.Instance.SetMasterVol(0);
                else NAudioManager.Instance.SetSfxVol(0);
                await Wait(.1);
                capture.ClearBuffer();
                SfxCmd.Play("event:/sfx/characters/fugitive/fugitive_ki");
                await Wait(.5);
                var muted = capture.GetBuffer(capture.GetFramesAvailable());
                Require(muted.Length > 0 && muted.All(s => Math.Abs(s.X) < .00001f && Math.Abs(s.Y) < .00001f), "Native volume slider mutes custom voice");
                NAudioManager.Instance.SetMasterVol(1);
                NAudioManager.Instance.SetSfxVol(1);
            }
            await Wait(.5);
            Require(!NCombatRoom.Instance!.GetChildren().OfType<AudioStreamPlayer>().Any(), "Finished audio nodes cleaned up");
            MelodyAudio.Played.Clear();
            var hpBeforePreview = player.Creature.CurrentHp;
            var preview = player.Creature.LoseHpInternal(2, dryRun: true);
            Require(preview.hpLost == 2 && player.Creature.CurrentHp == hpBeforePreview,
                "Damage preview predicts HP loss without changing HP");
            Require(MelodyAudio.Played.Count == 0, "Damage preview does not play hurt voice");
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, 2, ValueProp.Unblockable, null, null);
            await Wait(1.1);
            Require(string.Join(',', MelodyAudio.Played) == "hurt", "Actual HP loss plays Fool hurt voice");
            MelodyAudio.Played.Clear();
            await CreatureCmd.GainBlock(player.Creature, 10, ValueProp.Unpowered, null, fast: true);
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, 2, ValueProp.Move, null, null);
            Require(MelodyAudio.Played.Count == 0, "Fully blocked damage does not play hurt voice");
            SfxCmd.PlayDeath(player);
            await Wait(1.2);
            Require(string.Join(',', MelodyAudio.Played) == "death", "Native Fool death cue uses the local voice");
            Require(!NCombatRoom.Instance.GetChildren().OfType<AudioStreamPlayer>().Any(), "Hurt/death audio nodes cleaned up");
            MelodyAudio.Played.Clear();
            capture.ClearBuffer();
            await CreatureCmd.Kill(player.Creature.CombatState!.Enemies.ToArray(), force: true);
            Require(await CombatManager.Instance.CheckWinCondition(), "Native win condition ends the combat");
            await Wait(1.1);
            Require(MelodyAudio.Played.Count(s => s == "victory") == 1, "Winning real combat plays victory once");
            Require(!MelodyAudio.Played.Contains("defeat"), "No defeat voice on victory");
            var victory = NRun.Instance!.GetChildren().OfType<AudioStreamPlayer>().Single(p => p.Name == "Chowayo_victory");
            Require(victory.Playing && Math.Abs(victory.Stream.GetLength() - 4.4) < .001,
                "Full pumpkin song is playing under the run scene");

            var secondEncounter = ModelDb.Encounter<BattlewornDummyEventV3Encounter>().ToMutable();
            state.AppendToMapPointHistory(MapPointType.Monster, RoomType.Monster, secondEncounter.Id);
            await manager.EnterRoom(new CombatRoom(secondEncounter, state));
            for (var i = 0; i < 200 && !CombatManager.Instance.IsInProgress; i++) await Wait(.05);
            Require(CombatManager.Instance.IsInProgress, "Second real combat started");
            Require(GodotObject.IsInstanceValid(victory) && victory.Playing,
                "Victory song survives leaving the combat room");
            await Wait(4.5);
            var victoryPcm = capture.GetBuffer(capture.GetFramesAvailable());
            Require(victoryPcm.Skip((int)(AudioServer.GetMixRate() * 3)).Any(s => Math.Abs(s.X) > .001f),
                "Full victory song produces PCM past three seconds");
            Require(!GodotObject.IsInstanceValid(victory), "Full victory song finishes and frees its node");
            await Wait(.6);
            MelodyAudio.Played.Clear();
            capture.ClearBuffer();
            await CreatureCmd.Kill(player.Creature, force: true);
            for (var i = 0; i < 50 && !MelodyAudio.Played.Contains("defeat"); i++) await Wait(.1);
            Require(MelodyAudio.Played.Count(s => s == "defeat") == 1, "Actual game-over screen plays defeat once");
            Require(!MelodyAudio.Played.Contains("victory"), "No victory voice on defeat");
            await Wait(2.8);
            var defeatPcm = capture.GetBuffer(capture.GetFramesAvailable());
            Require(defeatPcm.Any(s => Math.Abs(s.X) > .001f), "Defeat voice produces PCM");
            ModEntry.Logger.Info("CHOWAYO AUDIO PASS: 8 real card plays, ordered voices, actual PCM, native SFX/master mute, silent damage preview, single HP-loss voice/blocked hit/death cue, full 4.4s victory across room transition, real game-over defeat, unchanged tags, node cleanup");
        }
        finally
        {
            game.RootSceneContainer.SetCurrentScene(new Control());
            manager.CleanUp(graceful: false);
            MelodyAudio.TestBus = "Master";
            AudioServer.RemoveBus(bus);
            NAudioManager.Instance!.SetMasterVol(SaveManager.Instance.SettingsSave.VolumeMaster);
            NAudioManager.Instance.SetSfxVol(SaveManager.Instance.SettingsSave.VolumeSfx);
        }
    }

    private static async Task Wait(double seconds) => await NGame.Instance!.ToSignal(
        NGame.Instance.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
