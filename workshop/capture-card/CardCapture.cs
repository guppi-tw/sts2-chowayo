using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace ChowayoCardCapture;

[ModInitializer(nameof(Initialize))]
public static class CardCapture
{
    public static void Initialize()
    {
        if (System.Environment.GetEnvironmentVariable("CHOWAYO_CARD_CAPTURE") is { } folder)
            _ = Capture(folder);
    }

    private static async Task Capture(string folder)
    {
        var game = NGame.Instance!;
        try
        {
            await game.GameStartupComplete;
            LocManager.Instance.SetLanguage("jpn");
            var model = ModelDb.AllCards.Single(c => c.Id.Entry == "WAH_CHO_KI").ToMutable();
            if (model.Title != "チョ・ワー・ヨ" || model.Rarity != CardRarity.Rare)
                throw new InvalidOperationException($"Unexpected card: {model.Title} / {model.Rarity}");
            foreach (var size in new[] { 1024, 512 })
            {
                var viewport = new SubViewport
                {
                    Size = new Vector2I(size, size), Disable3D = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                };
                game.AddChild(viewport);
                viewport.AddChild(new ColorRect { Size = new Vector2(size, size), Color = new Color("151c28") });
                var card = NCard.Create(model)!;
                viewport.AddChild(card);
                card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                card.SetPortraitTexture(model.BigPortrait);
                card.Position = new Vector2(size / 2f, size / 2f);
                card.Scale = Vector2.One * (size / 512f);
                for (var i = 0; i < 8; i++)
                    await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
                RenderingServer.ForceDraw();
                await game.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = viewport.GetTexture().GetImage();
                if (image.SavePng(Path.Combine(folder, $"chowayo-card-{size}.png")) != Error.Ok)
                    throw new IOException("Could not save card screenshot");
                viewport.QueueFree();
            }
            GD.Print("CHOWAYO CAPTURE PASS: WAH_CHO_KI, Rare, チョ・ワー・ヨ; native card renderer; 1024px and 512px");
            game.GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PrintErr($"CHOWAYO CAPTURE FAIL: {error}");
            game.GetTree().Quit(1);
        }
    }
}
