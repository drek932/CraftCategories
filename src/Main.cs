using System;
using MelonLoader;

[assembly: MelonInfo(typeof(CraftCategories.Main), "CraftCategories", "0.5.2", "drek932")]
[assembly: MelonGame("Hinterland", "TheLongDark")]
// Load before other mods to catch their recipe registration.
[assembly: MelonPriority(-1000)]
[assembly: MelonOptionalDependencies("ModSettings")]

namespace CraftCategories
{
    /// <summary>
    /// Entry point. Project layout:
    ///  Core/      — where recipes come from (BlueprintSources, ModCatalog), settings (Config), visibility rule (RecipeFilter), texts (Loc);
    ///  Settings/  — the ModSettings menu;
    ///  UI/        — everything added to the crafting menu;
    ///  Patches/   — hooks into game functions (Harmony).
    /// </summary>
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;

            Config.Load();
            BlueprintSources.ScanModComponentArchives();
            Config.SyncWithArchives();
            BlueprintSources.PatchModComponentApi(HarmonyInstance);
            Log.Msg($"Recipes found in .modcomponent files: {BlueprintSources.KnownCount}");

            try
            {
                RegisterSettings(HarmonyInstance);
            }
            catch (Exception e)
            {
                Log.Warning("Settings menu was not created (is ModSettings installed?): " + e.Message);
            }
        }

        // Separate method so a missing ModSettings.dll throws here instead of when the mod is loaded.
        private static void RegisterSettings(HarmonyLib.Harmony harmony)
        {
            SettingsMenu.Register();
            ModSettingsPatches.Apply(harmony);
        }

        public override void OnUpdate() => CraftingPanelUI.Update();

        public override void OnLateUpdate() => CraftingPanelUI.LateUpdate();
    }
}
