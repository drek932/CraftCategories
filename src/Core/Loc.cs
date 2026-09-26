using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CraftCategories
{
    /// <summary>
    /// Mod texts: Russian if the game language is Russian, otherwise English.
    /// Keys like "CRAFTCAT_X" or "CRAFTCAT_X|argument" are served to the game through the Localization.Get patch,
    /// so ModSettings labels switch together with the game language without a restart.
    /// </summary>
    internal static class Loc
    {
        public const string Prefix = "CRAFTCAT_";

        private static readonly Dictionary<string, (string ru, string en)> Texts = new()
        {
            // --- General settings ---
            ["GENERAL"] = ("Общие", "General"),
            ["HOVER_NAME"] = ("Название мода при наведении", "Mod name on hover"),
            ["HOVER_NAME_DESC"] = (
                "Да — при наведении мыши на кнопку категории мода рядом появляется название этого мода. Нет — подсказки нет.",
                "Yes — hovering the mouse over a mod's category button shows the mod's name next to it. No — no hint is shown."),
            ["CRAFTABLE_IN_GAME"] = ("Доступные рецепты модов в категориях игры", "Craftable mod recipes in game categories"),
            ["CRAFTABLE_IN_GAME_DESC"] = (
                "Да — рецепт мода, который можно сделать прямо сейчас (хватает материалов и инструментов), показывается ещё и в категориях игры, " +
                "включая «Все», даже если для мода выбрано «Только в категории мода». В категории мода он тоже остаётся. Нет — такие рецепты только в категории мода.",
                "Yes — a mod recipe you can craft right now (you have the materials and tools) is also shown in the game's categories, " +
                "including All, even if the mod is set to Mod category only. It also stays in the mod's category. No — such recipes stay in the mod's category only."),

            // --- Where to show recipes ---
            ["PLACEMENT"] = ("Где показывать рецепты", "Where to show recipes"),
            ["PLACEMENT_DESC"] = (
                "Категория мода — отдельная кнопка с иконкой этого мода в колонке категорий. " +
                "Категории игры — стандартные кнопки: «Все», «Огонь», «Инструменты» и т. д. «Везде» — и там, и там.",
                "Mod category — a separate button with this mod's icon in the category column. " +
                "Game categories — the standard buttons: All, Fire, Tools, etc. Everywhere — both."),
            ["PLACEMENT_MOD"] = ("Только в категории мода", "Mod category only"),
            ["PLACEMENT_GAME"] = ("Только в категориях игры", "Game categories only"),
            ["PLACEMENT_BOTH"] = ("Везде", "Everywhere"),

            // --- Category position ---
            ["ORDER"] = ("Позиция категории мода", "Mod category position"),
            ["ORDER_DESC"] = (
                "Порядок кнопки этого мода в колонке категорий: 1 — сразу под категориями игры. " +
                "Остальные моды сдвигаются автоматически. Не влияет, если рецепты показываются только в категориях игры.",
                "Order of this mod's button in the category column: 1 is right below the game's categories. " +
                "Other mods shift automatically. Has no effect when recipes are shown in game categories only."),

            // --- Individual recipes ---
            ["RECIPES"] = ("Показать список рецептов ({0})", "Show recipe list ({0})"),
            ["RECIPES_DESC"] = (
                "Раскрывает ниже все рецепты этого мода, чтобы скрыть ненужные. Сам по себе ничего не меняет в игре.",
                "Expands all of this mod's recipes below so you can hide the ones you don't need. Changes nothing in game by itself."),
            ["ITEM_DESC"] = (
                "Да — рецепт показывается в меню крафта. Нет — рецепт скрыт во всех категориях. Внутреннее имя: {0}",
                "Yes — the recipe is shown in the crafting menu. No — the recipe is hidden in all categories. Internal name: {0}"),

            // --- Crafting menu ---
            ["OTHER_MODS"] = ("Прочие моды", "Other mods"),
        };

        public static bool IsRussian =>
            CurrentGameLanguage() is { } lang && lang.IndexOf("russian", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// The language the game is actually using. Localization.s_Language is what SelectLanguage sets and what
        /// the game uses for its own translations; the Language property is only a fallback.
        /// </summary>
        private static string CurrentGameLanguage()
        {
            try
            {
                var lang = Il2Cpp.Localization.s_Language;
                if (!string.IsNullOrEmpty(lang)) return lang;
            }
            catch
            {
                // ignored, try the property below
            }

            try
            {
                return Il2Cpp.Localization.Language;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Text for a short key ("PLACEMENT") with arguments substituted.</summary>
        public static string T(string key, params object[] args)
        {
            if (!Texts.TryGetValue(key, out var t)) return key;
            var text = IsRussian ? t.ru : t.en;
            return args.Length > 0 ? string.Format(text, args) : text;
        }

        /// <summary>Key for ModSettings: "CRAFTCAT_KEY" or "CRAFTCAT_KEY|arg".</summary>
        public static string Key(string key, object arg = null) => Prefix + key + (arg != null ? "|" + arg : "");

        /// <summary>
        /// Brings a text of this mod to the current game language. Accepts a "CRAFTCAT_…" key or an already translated
        /// text in either language (used for texts that ModSettings caches and shows again without translating).
        /// Returns null if the text is not ours or is already in the current language.
        /// </summary>
        public static string Relocalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (Resolve(text) is { } fromKey) return fromKey;

            bool russian = IsRussian;
            foreach (var (key, t) in Texts)
            {
                var wrong = russian ? t.en : t.ru;
                var right = russian ? t.ru : t.en;
                if (!wrong.Contains("{"))
                {
                    if (text == wrong) return right;
                    continue;
                }
                var match = TemplateRegex(wrong).Match(text);
                if (match.Success)
                    return string.Format(right, match.Groups.Cast<Group>().Skip(1).Select(g => (object)g.Value).ToArray());
            }
            return null;
        }

        private static readonly Dictionary<string, Regex> templateRegexes = new();

        /// <summary>"Show recipe {0} in…" -> regex that captures {0}.</summary>
        private static Regex TemplateRegex(string template)
        {
            if (templateRegexes.TryGetValue(template, out var rx)) return rx;
            var pattern = "^" + Regex.Replace(Regex.Escape(template), @"\\\{\d+}", "(.*?)") + "$";
            return templateRegexes[template] = new Regex(pattern, RegexOptions.Singleline);
        }

        /// <summary>Parses a key from Localization.Get. Returns null if the key is not ours.</summary>
        public static string Resolve(string fullKey)
        {
            if (fullKey == null || !fullKey.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            var body = fullKey.Substring(Prefix.Length);
            int bar = body.IndexOf('|');
            return bar < 0 ? T(body) : T(body.Substring(0, bar), body.Substring(bar + 1));
        }
    }
}
