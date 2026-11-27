#if DEBUG
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.addons.mega_text;

namespace Chowayo;

internal static class SmokeTest
{
    internal static async Task Run()
    {
        var game = NGame.Instance!;
        try
        {
            await game.GameStartupComplete;
            await MelodyAudio.Ready;
            await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
            var bilingual = System.Environment.GetEnvironmentVariable("CHOWAYO_TEST_BILINGUAL") == "1";
            var switcher = AccessTools.TypeByName("BilingualSpire.LanguageSwitcher");
            if (bilingual)
            {
                Require(switcher is not null, "Bilingual Spire loaded");
                await (Task)AccessTools.Property(switcher, "Ready").GetValue(null)!;
            }
            foreach (var (table, key, japanese, english) in new[]
            {
                ("cards", "KI_HOP.title", "ヨ・ホップ", "Yo Hop"),
                ("cards", "WAH_CHO.title", "チョ・ワー", "Cho Wah"),
                ("cards", "WAH_CHO_KI.title", "チョ・ワー・ヨ", "Cho Wah Yo"),
                ("static_hover_tips", "KI_MELODY.title", "ヨの旋律", "Yo Melody"),
            })
            {
                var initial = new LocString(table, key).GetRawText();
                ModEntry.Logger.Info($"CHOWAYO TEST STARTUP: {table}/{key} = {initial}");
                Require(initial == japanese || initial == english, $"Cold startup {table}/{key}: {initial}");
            }
            var cards = new Dictionary<string, string>
            {
                ["KI_CHANT"] = "ヨ・チャント", ["KI_HOP"] = "ヨ・ホップ",
                ["KI_REST"] = "ヨ・レスト", ["KI_VERSE"] = "ヨ・ヴァース",
                ["KI_WHIP"] = "ヨ・ウィップ", ["CHO_KI"] = "チョ・ヨ",
                ["WAH_KI"] = "ワー・ヨ", ["WAH_CHO"] = "チョ・ワー",
                ["WAH_CHO_KI"] = "チョ・ワー・ヨ", ["WAH_TOUCH"] = "ワー・タッチ",
                ["CHO_FIRM"] = "チョ・ファーム",
            };
            using (var source = Godot.FileAccess.Open("res://localization/jpn/cards.json", Godot.FileAccess.ModeFlags.Read))
            {
                var originals = JsonSerializer.Deserialize<Dictionary<string, string>>(source.GetAsText())!;
                foreach (var model in ModelDb.AllCards.Where(c => c.Tags.Any(t => t is CardTag.Cho or CardTag.Wah or CardTag.Ki)))
                {
                    var original = originals[$"{model.Id.Entry}.title"];
                    cards.TryAdd(model.Id.Entry, original.Replace("ワー・チョ", "チョ・ワー")
                        .Replace("キ・", "ヨ・").Replace("・キ", "・ヨ"));
                }
            }
            foreach (var (id, title) in cards)
            {
                var card = ModelDb.AllCards.First(c => c.Id.Entry == id).ToMutable();
                var view = NCard.Create(card)!;
                game.AddChild(view);
                view.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                Require(card.Title == title, $"{id}: model title {card.Title}");
                Require(view.GetNode<MegaLabel>("%TitleLabel").Text == title, $"{id}: rendered title");
                var description = card.GetDescriptionForPile(PileType.None);
                Require(!description.Contains('{') && !description.Contains("キの旋律"), $"{id}: formatted description");
                view.QueueFree();
            }

            var changed = 0;
            foreach (var tableName in new[] { "cards", "powers", "potions", "relics", "static_hover_tips" })
            {
                using var source = Godot.FileAccess.Open($"res://localization/jpn/{tableName}.json", Godot.FileAccess.ModeFlags.Read);
                var original = JsonSerializer.Deserialize<Dictionary<string, string>>(source.GetAsText())!;
                var table = LocManager.Instance.GetTable(tableName);
                foreach (var (key, value) in original)
                {
                    var actual = table.GetRawText(key);
                    if (actual != value) changed++;
                    Require(!actual.Contains("キの旋律") && !actual.Contains("「キ」"), $"{tableName}/{key}: no old melody term");
                    Require(Tokens(value) == Tokens(actual), $"{tableName}/{key}: numbers, variables and tags preserved");
                    if (!value.Contains("キの旋律") && !value.Contains("「キ」") &&
                        !value.Contains("キ・") && !value.Contains("・キ") && !value.Contains("「ワー」か「チョ」") && key != "WAH_CHO.title")
                        Require(actual == value, $"{tableName}/{key}: unrelated text preserved");
                }
            }
            Require(LocManager.Instance.GetTable("static_hover_tips").GetRawText("KI_MELODY.title") == "ヨの旋律", "Melody tooltip");
            Require(LocManager.Instance.GetTable("powers").GetRawText("KI_VERSE_POWER.title") == "ヨ・ヴァース", "Power title");
            foreach (var (table, key) in new[] { ("cards", "KI_VERSE.description"),
                ("powers", "KI_VERSE_POWER.description"), ("powers", "KI_VERSE_POWER.smartDescription") })
                Require(new LocString(table, key).GetRawText().Contains("「チョ」か「ワー」"),
                    $"Japanese two-note description order: {table}/{key}");

            var triple = ModelDb.AllCards.First(c => c.Id.Entry == "WAH_CHO_KI").ToMutable();
            triple.UpgradeInternal();
            triple.FinalizeUpgradeInternal();
            Require(triple.Title == "チョ・ワー・ヨ+", "Upgrade suffix");
            LocManager.Instance.SetLanguage("eng");
            Require(new LocString("cards", "KI_HOP.title").GetRawText() == "Yo Hop", "English Yo title");
            Require(new LocString("cards", "WAH_CHO.title").GetRawText() == "Cho Wah", "English two-note order");
            Require(new LocString("cards", "WAH_CHO_KI.title").GetRawText() == "Cho Wah Yo", "English three-note order");
            Require(new LocString("static_hover_tips", "KI_MELODY.title").GetRawText() == "Yo Melody", "English tooltip");
            var verseDescription = new LocString("cards", "KI_VERSE.description").GetRawText();
            Require(verseDescription.Contains("“Cho” or “Wah”") && !verseDescription.Contains("“Yo”"),
                "English description order preserves the current two-note effect");
            foreach (var (locale, expected) in new Dictionary<string, string>
            {
                ["deu"] = "Cho Wa Yo", ["eng"] = "Cho Wah Yo", ["esp"] = "Cho Wah Yo",
                ["fra"] = "Cho Wah Yo", ["ind"] = "Cho Wah Yo", ["ita"] = "Cho Wah Yo",
                ["kor"] = "초 와 요", ["pol"] = "Cho Wah Yo", ["ptb"] = "Cho Wah Yo",
                ["rus"] = "Чу Ва Йо", ["spa"] = "Cho Wah Yo", ["tur"] = "Ço Vah Yo",
                ["zhs"] = "啾 哇 哟", ["zht"] = "啾哇喲",
            })
            {
                LocManager.Instance.SetLanguage(locale);
                Require(new LocString("cards", "WAH_CHO_KI.title").GetRawText() == expected, $"{locale}: three-note title");
                if (locale == "rus")
                {
                    var description = new LocString("potions", "BOTTLED_SONG.description").GetRawText();
                    Require(description.Contains("Чу") && description.Contains("Ва") && description.Contains("Йо") &&
                        description.IndexOf("Чу", StringComparison.Ordinal) < description.IndexOf("Ва", StringComparison.Ordinal) &&
                        description.IndexOf("Ва", StringComparison.Ordinal) < description.IndexOf("Йо", StringComparison.Ordinal),
                        "Russian potion lists Cho, Wah, Yo in order");
                }
                foreach (var tableName in new[] { "cards", "powers", "potions", "relics", "static_hover_tips" })
                {
                    using var source = Godot.FileAccess.Open($"res://localization/{locale}/{tableName}.json", Godot.FileAccess.ModeFlags.Read);
                    var originals = JsonSerializer.Deserialize<Dictionary<string, string>>(source.GetAsText())!;
                    var table = LocManager.Instance.GetTable(tableName);
                    foreach (var (key, value) in originals)
                        Require(Tokens(value) == Tokens(table.GetRawText(key)), $"{locale}/{tableName}/{key}: format preserved");
                }
            }
            LocManager.Instance.SetLanguage("jpn");
            Require(triple.Title == "チョ・ワー・ヨ+", "Language reload");

            if (bilingual)
            {
                AccessTools.Method(switcher, "SetPair").Invoke(null, ["eng", "jpn"]);
                AccessTools.Method(switcher, "ShowBilingual").Invoke(null, null);
                var view = NCard.Create(triple)!;
                game.AddChild(view);
                view.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                var title = view.GetNode<MegaLabel>("%TitleLabel").Text;
                Require(title.Contains("Cho Wah Yo+") && title.Contains("チョ・ワー・ヨ+") && !title.Contains("ワー・チョ・キ"), "Bilingual secondary locale");
                view.QueueFree();
            }
            LocManager.Instance.SetLanguage("jpn");
            await IconSmokeTest.Run();
            if (System.Environment.GetEnvironmentVariable("CHOWAYO_TEST_AUDIO") == "1")
                await AudioSmokeTest.Run();
            ModEntry.Logger.Info($"CHOWAYO TEST PASS: {cards.Count} card views, {changed} changed entries, tooltips, powers, format tokens, upgrades, locale reload; bilingual={bilingual}");
            game.GetTree().Quit(0);
        }
        catch (Exception error)
        {
            ModEntry.Logger.Error($"CHOWAYO TEST FAIL: {error}");
            game.GetTree().Quit(1);
        }
    }

    private static string Tokens(string value) => string.Join("|", Regex.Matches(value, @"\{[^{}]*\}|\[[^\]]*\]|[0-9]+").Select(m => m.Value));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
