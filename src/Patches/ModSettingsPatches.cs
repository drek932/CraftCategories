using HarmonyLib;
using Il2Cpp;

namespace CraftCategories
{
    /// <summary>
    /// Keeps setting descriptions in the ModSettings menu in the current game language.
    ///
    /// ModSettings translates setting names through the game's UILocalize component, where LocalizationPatches applies,
    /// so names follow the game language. Descriptions are handled differently: ModSettings writes them into the line at
    /// the bottom of the options menu and may show an already translated text again, so after switching the game
    /// language the description stayed in the previous language.
    ///
    /// So the description line itself is checked: right after ModSettings updates it, and every frame while the options
    /// menu is open. If it contains one of this mod's texts (a key or a translation in either language),
    /// it is replaced with the version for the current game language.
    /// </summary>
    internal static class ModSettingsPatches
    {
        // Last checked state, so the text is only re-examined when it or the language changes.
        private static string lastText;
        private static bool lastRussian;

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            // ModSettingsGUI is not public, so patch it by name.
            var method = AccessTools.Method("ModSettings.ModSettingsGUI:UpdateDescriptionLabel");
            if (method == null)
            {
                Main.Log.Warning("ModSettingsGUI.UpdateDescriptionLabel not found, descriptions are only checked every frame");
                return;
            }
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(ModSettingsPatches), nameof(AfterDescriptionUpdated)));
        }

        /// <summary>Called every frame (see Main.OnLateUpdate). Does nothing while the options menu is closed.</summary>
        public static void LateUpdate()
        {
            // TryGetPanel only returns an existing panel and never creates one.
            if (InterfaceManager.TryGetPanel<Panel_OptionsMenu>(out var panel) && panel != null && panel.IsEnabled())
                TranslateDescription(panel);
        }

        private static void AfterDescriptionUpdated()
        {
            if (InterfaceManager.TryGetPanel<Panel_OptionsMenu>(out var panel) && panel != null)
                TranslateDescription(panel);
        }

        private static void TranslateDescription(Panel_OptionsMenu panel)
        {
            var label = panel.m_OptionDescriptionLabel;
            if (label == null) return;

            var text = label.text;
            bool russian = Loc.IsRussian;
            if (text == lastText && russian == lastRussian) return;

            var translated = Loc.Relocalize(text);
            if (translated != null) label.text = translated;
            lastText = translated ?? text;
            lastRussian = russian;
        }
    }
}
