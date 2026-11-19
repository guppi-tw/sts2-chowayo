using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace Chowayo;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    internal static readonly Logger Logger = new("Chowayo", LogType.Generic);

    public static void Initialize()
    {
        new Harmony("guppi-tw.Chowayo").PatchAll(typeof(ModEntry).Assembly);
        Logger.Info("Chowayo v0.4.0 loaded successfully");
        MelodyAudio.Ready = MelodyAudio.Initialize();
#if DEBUG
        if (Environment.GetEnvironmentVariable("CHOWAYO_SMOKE_TEST") == "1")
            _ = SmokeTest.Run();
#endif
    }
}
