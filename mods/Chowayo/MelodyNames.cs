using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using System.Text.RegularExpressions;

namespace Chowayo;

[HarmonyPatch(typeof(LocManager), "LoadTable")]
internal static class MelodyNames
{
    private static readonly Dictionary<string, string> Titles = new()
    {
        ["cards/CHO_KI.title"] = "チョ・ヨ",
        ["cards/KI_CHANT.title"] = "ヨ・チャント",
        ["cards/KI_HOP.title"] = "ヨ・ホップ",
        ["cards/KI_REST.title"] = "ヨ・レスト",
        ["cards/KI_VERSE.title"] = "ヨ・ヴァース",
        ["cards/KI_WHIP.title"] = "ヨ・ウィップ",
        ["cards/WAH_CHO.title"] = "チョ・ワー",
        ["cards/WAH_CHO_KI.title"] = "チョ・ワー・ヨ",
        ["cards/WAH_KI.title"] = "ワー・ヨ",
        ["powers/KI_VERSE_POWER.title"] = "ヨ・ヴァース",
        ["static_hover_tips/KI_MELODY.title"] = "ヨの旋律",
    };

    private record Terms(string Cho, string Wah, string Ki, string Yo, string Separator = " ", bool WholeWord = true);
    private static readonly Terms English = new("Cho", "Wah", "Ki", "Yo");
    private static readonly Dictionary<string, Terms> Languages = new()
    {
        ["eng"] = English, ["esp"] = English, ["fra"] = English, ["ind"] = English,
        ["ita"] = English, ["pol"] = English, ["ptb"] = English, ["spa"] = English,
        ["deu"] = new("Cho", "Wa", "Ki", "Yo"),
        ["kor"] = new("초", "와", "키", "요"),
        ["rus"] = new("Чу", "Вах", "Ки", "Йо"),
        ["tur"] = new("Ço", "Vah", "Ki", "Yo"),
        ["zhs"] = new("啾", "哇", "叽", "哟", WholeWord: false),
        ["zht"] = new("啾", "哇", "嘰", "喲", "", WholeWord: false),
    };

    // Scope replacements to melody copy; never rename other cards or format variables.
    private static readonly HashSet<string> MelodyEntries = new()
    {
        "cards/CHO_CHORUS.description", "cards/CHO_LIMBS.description", "cards/KI_REST.description",
        "cards/KI_VERSE.description", "cards/TUNE.description", "cards/WAH_BLAST.description",
        "cards/WAH_SYMPHONY.description", "powers/KI_VERSE_POWER.description",
        "powers/KI_VERSE_POWER.smartDescription", "powers/TUNE_POWER.description",
        "powers/TUNE_POWER.smartDescription", "potions/BOTTLED_SONG.description",
        "relics/OCARINA.description", "static_hover_tips/KI_MELODY.description",
    };
    private static readonly HashSet<string> Enumerations = new()
    {
        "cards/KI_VERSE.description", "cards/WAH_SYMPHONY.description",
        "powers/KI_VERSE_POWER.description", "powers/KI_VERSE_POWER.smartDescription",
        "potions/BOTTLED_SONG.description", "relics/OCARINA.description",
    };

    private static string TermPattern(string term, Terms words) => words.WholeWord
        ? $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}_])"
        : Regex.Escape(term);

    private static string Rename(string key, string original, Terms words)
    {
        if (string.IsNullOrEmpty(original)) return original;
        if (key == "cards/WAH_CHO.title") return words.Cho + words.Separator + words.Wah;
        if (key == "cards/WAH_CHO_KI.title") return string.Join(words.Separator, words.Cho, words.Wah, words.Yo);
        if (key == "cards/CHO_KI.title") return words.Cho + words.Separator + words.Yo;
        if (key == "cards/WAH_KI.title") return words.Wah + words.Separator + words.Yo;
        if (!Titles.ContainsKey(key) && !MelodyEntries.Contains(key)) return original;
        var text = Regex.Replace(original, TermPattern(words.Ki, words), words.Yo);
        if (Enumerations.Contains(key))
        {
            var wah = Regex.Match(text, TermPattern(words.Wah, words));
            var cho = Regex.Match(text, TermPattern(words.Cho, words));
            if (wah.Success && cho.Success && wah.Index < cho.Index)
                text = Regex.Replace(text, $"{TermPattern(words.Wah, words)}|{TermPattern(words.Cho, words)}",
                    match => match.Value == words.Wah ? words.Cho : words.Wah);
        }
        return text;
    }

    private static void Postfix(string path, Dictionary<string, string> __result)
    {
        const string prefix = "res://localization/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal)) return;
        var language = path[prefix.Length..].Split('/')[0];
        if (language != "jpn" && !Languages.ContainsKey(language)) return;
        var table = Path.GetFileNameWithoutExtension(path);
        if (table is not ("cards" or "powers" or "potions" or "relics" or "static_hover_tips")) return;

        foreach (var (key, original) in __result.ToArray())
        {
            // Replace complete melody terms only; words such as スキル are unrelated.
            var renamed = language != "jpn" ? Rename($"{table}/{key}", original, Languages[language])
                : Titles.GetValueOrDefault($"{table}/{key}") ?? original
                .Replace("「ワー」「チョ」「キ」", "「チョ」「ワー」「ヨ」", StringComparison.Ordinal)
                .Replace("キの旋律", "ヨの旋律", StringComparison.Ordinal)
                .Replace("「キ」", "「ヨ」", StringComparison.Ordinal);
            if (renamed != original) __result[key] = renamed;
        }
    }
}
