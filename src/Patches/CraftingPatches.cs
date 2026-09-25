using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;

namespace CraftCategories
{
    /// <summary>Hooks into the game's crafting menu. All logic lives in CraftingPanelUI and RecipeFilter.</summary>
    internal static class CraftingPatches
    {
        private static void OnPanelOpening()
        {
            if (ModCatalog.Refresh()) Config.SyncWithCatalog();
        }

        private static void OnPanelOpened(Panel_Crafting panel) => CraftingPanelUI.OnPanelOpened(panel);

        // Panel_Crafting.Enable has two overloads — hook both.

        [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.Enable), new[] { typeof(bool) })]
        private static class Enable
        {
            private static void Prefix(bool enable) { if (enable) OnPanelOpening(); }
            private static void Postfix(Panel_Crafting __instance, bool enable) { if (enable) OnPanelOpened(__instance); }
        }

        [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.Enable), new[] { typeof(bool), typeof(bool) })]
        private static class EnableFromPanel
        {
            private static void Prefix(bool enable) { if (enable) OnPanelOpening(); }
            private static void Postfix(Panel_Crafting __instance, bool enable) { if (enable) OnPanelOpened(__instance); }
        }

        /// <summary>Category change: for a mod category the game gets "All", and RecipeFilter narrows the list.</summary>
        [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.OnCategoryChanged))]
        private static class CategoryChanged
        {
            private static void Prefix(ref int index) => index = CraftingPanelUI.OnCategoryChanging(index);
            private static void Postfix() => CraftingPanelUI.OnCategoryChanged();
        }

        /// <summary>Called for every recipe while the list is built.</summary>
        [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.ItemPassesFilter))]
        private static class ItemPassesFilter
        {
            private static void Postfix(BlueprintData bpi, ref bool __result)
            {
                if (__result && bpi != null)
                    __result = RecipeFilter.IsVisible(bpi, CraftingPanelUI.ActiveMod);
            }
        }

        /// <summary>Category selected (mouse or gamepad) — scroll the column to it.</summary>
        [HarmonyPatch(typeof(CategoryButtonNavigation), nameof(CategoryButtonNavigation.SetCurrentIndex))]
        private static class NavigationIndexChanged
        {
            private static void Postfix(CategoryButtonNavigation __instance, int index) =>
                CraftingPanelUI.OnNavigationIndexChanged(__instance, index);
        }
    }
}
