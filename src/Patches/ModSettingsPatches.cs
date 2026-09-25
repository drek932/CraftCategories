using HarmonyLib;
using Il2Cpp;

namespace CraftCategories
{
    /// <summary>
    /// Translates setting descriptions in the ModSettings menu.
    ///
    /// ModSettings translates setting names through the game's UILocalize component, where LocalizationPatches applies.
    /// Descriptions, however, are translated once when the menu is built, via a direct call that bypasses our hook,
    /// so they came out empty. Instead, descriptions are passed as untranslated keys ("CRAFTCAT_…") and translated
    /// when shown — after ModSettings has written the description into the line at the bottom of the menu.
    /// This also keeps descriptions in the current game language.
    /// </summary>
    internal static class ModSettingsPatches
    {
        public static void Apply(HarmonyLib.Harmony harmony)
        {
            // ModSettingsGUI is not public, so patch it by name.
            var method = AccessTools.Method("ModSettings.ModSettingsGUI:UpdateDescriptionLabel");
            if (method == null)
            {
                Main.Log.Warning("ModSettingsGUI.UpdateDescriptionLabel not found, setting descriptions will not be translated");
                return;
            }
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(ModSettingsPatches), nameof(TranslateDescription)));
        }

        private static void TranslateDescription()
        {
            var label = InterfaceManager.GetPanel<Panel_OptionsMenu>()?.m_OptionDescriptionLabel;
            if (label == null) return;
            var text = Loc.Resolve(label.text);
            if (text != null) label.text = text;
        }
    }
}
