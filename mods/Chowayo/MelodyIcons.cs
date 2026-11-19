using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Chowayo;

[HarmonyPatch(typeof(NFoolCombatSymbol), "SetVisualsAndHoverTip")]
internal static class MelodyIcons
{
    private static readonly Dictionary<CardTag, ShaderMaterial> Materials = new();
    private static ImageTexture? _texture;
    private static bool _attemptedLoad;

    private const string RecolorShader = """
        shader_type canvas_item;
        uniform vec4 accent : source_color;
        varying vec4 modulation;
        void vertex() {
            modulation = COLOR;
        }
        void fragment() {
            vec2 icon_uv = clamp((UV - vec2(0.5)) / 0.84 + vec2(0.5), vec2(0.0), vec2(1.0));
            vec4 source = texture(TEXTURE, icon_uv);
            float luminance = dot(source.rgb, vec3(0.299, 0.587, 0.114));
            vec3 ink = accent.rgb * 0.22;
            vec3 paper = mix(accent.rgb, vec3(1.0), 0.72);
            vec3 color = luminance < 0.5
                ? mix(ink, accent.rgb, smoothstep(0.18, 0.5, luminance))
                : mix(accent.rgb, paper, smoothstep(0.5, 0.98, luminance));
            COLOR = vec4(color, source.a) * modulation;
        }
        """;

    private static bool Load()
    {
        if (_attemptedLoad) return _texture is not null;
        _attemptedLoad = true;
        var path = Path.Combine(Path.GetDirectoryName(typeof(ModEntry).Assembly.Location)!,
            "icons", "speaki.png");
        if (!File.Exists(path)) return false;
        try
        {
            using var source = Image.LoadFromFile(path);
            if (source is null || source.IsEmpty()) throw new InvalidDataException("Empty Speaki icon");
            source.GenerateMipmaps();
            var shader = new Shader { Code = RecolorShader };
            foreach (var (tag, color) in new[]
            {
                (CardTag.Cho, "3988FF"), (CardTag.Wah, "FF55B7"), (CardTag.Ki, "FF9A35"),
            })
            {
                var material = new ShaderMaterial { Shader = shader };
                material.SetShaderParameter("accent", new Color(color));
                Materials.Add(tag, material);
            }
            _texture = ImageTexture.CreateFromImage(source);
            ModEntry.Logger.Info("Speaki melody icons loaded: Cho blue / Wah pink / Yo orange");
            return true;
        }
        catch (Exception error)
        {
            foreach (var material in Materials.Values) material.Dispose();
            Materials.Clear();
            ModEntry.Logger.Warn($"Keeping original melody icons: {error.Message}");
            return false;
        }
    }

    private static void Postfix(NFoolCombatSymbol __instance)
    {
        if (!Load() || !Materials.TryGetValue(__instance.symbolType, out var material)) return;
        var symbol = __instance.GetNode<TextureRect>("%Symbol");
        // Ignore the source PNG dimensions so the native layout and hover bounds stay intact.
        symbol.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        symbol.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        symbol.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
        symbol.Texture = _texture;
        symbol.Material = material;
    }
}
