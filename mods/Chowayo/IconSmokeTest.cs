#if DEBUG
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Chowayo;

internal static class IconSmokeTest
{
    internal static async Task Run()
    {
        var game = NGame.Instance!;
        var icons = new List<NFoolCombatSymbol>();
        try
        {
            foreach (var (tag, color) in new[]
            {
                (CardTag.Cho, "3988FF"), (CardTag.Wah, "FF55B7"), (CardTag.Ki, "FF9A35"),
            })
            {
                var icon = NFoolCombatSymbol.Create(tag);
                icons.Add(icon);
                game.AddChild(icon);
                var symbol = icon.GetNode<TextureRect>("%Symbol");
                Require(symbol.Texture is ImageTexture, $"{tag}: custom image loaded by native UI");
                using var image = symbol.Texture.GetImage();
                Require(image.GetPixel(0, 0).A == 0 && image.GetPixel(image.GetWidth() / 2, image.GetHeight() / 2).A > .9f,
                    $"{tag}: transparent background and opaque face");
                Require(symbol.Material is ShaderMaterial material && material.GetShaderParameter("accent").AsColor() == new Color(color),
                    $"{tag}: distinct palette");
                Require(icon.symbolType == tag && icon.Size.X < 200 && icon.Size.Y < 200,
                    $"{tag}: native melody identity and hit bounds preserved");
                Require(symbol.Modulate.A < .1f, $"{tag}: native fade starts transparent");
                icon.MoveTo(new Vector2(150, 150));
            }
            await game.ToSignal(game.GetTree().CreateTimer(.7), SceneTreeTimer.SignalName.Timeout);
            foreach (var icon in icons)
            {
                var symbol = icon.GetNode<TextureRect>("%Symbol");
                Require(symbol.Modulate.A > .99f && symbol.Scale.IsEqualApprox(Vector2.One), "Spawn animation completed");
                Require(icon.GlobalPosition.DistanceTo(new Vector2(150, 150)) < 1, "Native movement completed");
            }
            Require(new LocString("static_hover_tips", "KI_MELODY.title").GetRawText().Contains("ヨ"), "Yo hover title remains localized");
            ModEntry.Logger.Info("CHOWAYO ICON PASS: 3 native melody icons; transparent image; blue/pink/orange; native bounds, identity, spawn and movement");
        }
        finally
        {
            foreach (var icon in icons) icon.QueueFree();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
