using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace Chowayo;

internal static class MelodyAudio
{
    private static readonly Dictionary<string, AudioStreamWav> Streams = new();
    private static readonly Dictionary<AudioStreamPlayer, float> Players = new();
    internal static Task Ready { get; set; } = Task.CompletedTask;
    internal static bool Enabled => Streams.ContainsKey("cho") && Streams.ContainsKey("wa") && Streams.ContainsKey("yo");
    internal static readonly AsyncLocal<bool> ReorderCompound = new();
    private static float _master;
    private static float _sfx;
#if DEBUG
    internal static readonly List<string> Played = new();
    internal static StringName TestBus = "Master";
#endif

    internal static async Task Initialize()
    {
        await NGame.Instance!.GameStartupComplete;
        var directory = Path.Combine(Path.GetDirectoryName(typeof(ModEntry).Assembly.Location)!, "audio");
        try
        {
            foreach (var name in new[] { "cho", "wa", "yo", "hurt", "death", "victory", "defeat" })
            {
                var path = Path.Combine(directory, $"{name}.wav");
                if (!File.Exists(path)) continue;
                var stream = AudioStreamWav.LoadFromFile(path);
                if (stream is null || stream.GetLength() <= 0) throw new InvalidDataException($"Invalid {name}.wav");
                stream.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
                Streams.Add(name, stream);
            }
            _master = SaveManager.Instance.SettingsSave.VolumeMaster;
            _sfx = SaveManager.Instance.SettingsSave.VolumeSfx;
            ModEntry.Logger.Info($"Local voice files loaded: {string.Join(" / ", Streams.Keys)}; complete melody set={Enabled}");
            CombatManager.Instance.CombatWon += room =>
            {
                if (!NonInteractiveMode.IsActive && LocalContext.GetMe(room.CombatState)?.Character is Fool)
                    TryPlay("victory", 1, NRun.Instance);
            };
        }
        catch (Exception error)
        {
            foreach (var stream in Streams.Values) stream.Dispose();
            Streams.Clear();
            ModEntry.Logger.Warn($"Keeping original melody sounds: {error.Message}");
        }
    }

    internal static bool TryPlay(string name, float volume, Node? parent = null)
    {
        parent ??= NCombatRoom.Instance;
        if (!Streams.TryGetValue(name, out var stream) || parent is null || !GodotObject.IsInstanceValid(parent)) return false;
        AudioStreamPlayer? player = null;
        try
        {
            player = new AudioStreamPlayer { Stream = stream, Name = $"Chowayo_{name}" };
#if DEBUG
            player.Bus = TestBus;
#endif
            parent.AddChild(player);
            Players.Add(player, volume);
            player.VolumeLinear = Gain(volume);
            var owned = player;
            player.TreeExiting += () => Players.Remove(owned);
            player.Finished += () => owned.QueueFree();
            player.Play();
#if DEBUG
            Played.Add(name);
#endif
            return true;
        }
        catch (Exception error)
        {
            if (player is not null && GodotObject.IsInstanceValid(player))
            {
                Players.Remove(player);
                player.QueueFree();
            }
            ModEntry.Logger.Warn($"Could not play local {name} voice: {error.Message}");
            return false;
        }
    }

    private static float Gain(float volume) => Math.Clamp(volume, 0, 1) * _master * _master * _sfx * _sfx;

    internal static void SetVolume(bool master, float volume)
    {
        if (master) _master = Math.Clamp(volume, 0, 1);
        else _sfx = Math.Clamp(volume, 0, 1);
        foreach (var (player, gain) in Players)
            if (GodotObject.IsInstanceValid(player)) player.VolumeLinear = Gain(gain);
    }
}

[HarmonyPatch(typeof(NGameOverScreen), nameof(NGameOverScreen._Ready))]
internal static class FoolDefeatVoicePatch
{
    private static void Postfix(NGameOverScreen __instance, Player ____localPlayer, RunState ____runState)
    {
        if (!NonInteractiveMode.IsActive && ____localPlayer.Character is Fool &&
            !RunManager.Instance.IsAbandoned && !(____runState.CurrentRoom?.IsVictoryRoom ?? false))
            MelodyAudio.TryPlay("defeat", 1, __instance);
    }
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), [typeof(string), typeof(float)])]
internal static class MelodySoundPatch
{
    private static bool Prefix(string sfx, float volume)
    {
        if (NonInteractiveMode.IsActive || CombatManager.Instance.IsEnding) return true;
        var name = sfx switch
        {
            "event:/sfx/characters/fugitive/fugitive_cho" => "cho",
            "event:/sfx/characters/fugitive/fugitive_wa" => "wa",
            "event:/sfx/characters/fugitive/fugitive_ki" => "yo",
            _ => null,
        };
        return name is null || !MelodyAudio.Enabled || !MelodyAudio.TryPlay(name, volume);
    }
}

// These async methods capture the scope for their own VFX sequence. Restoring it
// when they return their Task keeps other cards and nested plays independent.
[HarmonyPatch]
internal static class CompoundMelodyScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(WahCho), "OnPlay");
        yield return AccessTools.DeclaredMethod(typeof(WahChoKi), "OnPlay");
    }
    private static void Prefix(out bool __state)
    {
        __state = MelodyAudio.ReorderCompound.Value;
        MelodyAudio.ReorderCompound.Value = MelodyAudio.Enabled;
    }
    private static void Finalizer(bool __state) => MelodyAudio.ReorderCompound.Value = __state;
}

[HarmonyPatch(typeof(MelodyCmd), nameof(MelodyCmd.PlayMelodyVfx))]
internal static class MelodyVisualOrderPatch
{
    private static void Prefix(ref CardTag cardTag)
    {
        if (!MelodyAudio.ReorderCompound.Value) return;
        cardTag = cardTag switch { CardTag.Wah => CardTag.Cho, CardTag.Cho => CardTag.Wah, _ => cardTag };
    }
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.SetMasterVol))]
internal static class MasterVolumePatch
{
    private static void Postfix(float volume) => MelodyAudio.SetVolume(master: true, volume);
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.SetSfxVol))]
internal static class SfxVolumePatch
{
    private static void Postfix(float volume) => MelodyAudio.SetVolume(master: false, volume);
}

[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
internal static class FoolHurtVoicePatch
{
    private static void Postfix(Creature __instance, HpLossResult __result, bool dryRun)
    {
        if (!dryRun && !NonInteractiveMode.IsActive && __instance.Player?.Character is Fool &&
            __result.hpLost > 0 && !__instance.IsDead && CombatManager.Instance.IsInProgress)
            MelodyAudio.TryPlay("hurt", 1);
    }
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.PlayDeath), [typeof(Player)])]
internal static class FoolDeathVoicePatch
{
    private static bool Prefix(Player player) => NonInteractiveMode.IsActive || player.Character is not Fool ||
        !MelodyAudio.TryPlay("death", 1);
}
