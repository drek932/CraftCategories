using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using UnityEngine;

namespace CraftCategories
{
    /// <summary>Hooks into the game's crafting menu. All logic lives in CraftingPanelUI and RecipeFilter.</summary>
    internal static class CraftingPatches
    {
        private static void OnPanelOpening()
        {
            try
            {
                if (!ModCatalog.Refresh())
                    Main.Log.Warning("Crafting menu opened, but the game's recipe list is not available yet");
                else
                    Config.SyncWithCatalog();
            }
            catch (System.Exception e)
            {
                Main.Log.Error("Could not read the game's recipe list: " + e);
            }
        }

        // Panel_Crafting.Enable has two overloads — hook both.

        // True while Panel_Crafting.Enable runs: the game calls ApplyFilter from inside it, and then the fallback below must stay idle.
        private static bool insideEnable;

        private static void BeforeEnable(bool enable)
        {
            if (!enable) return;
            insideEnable = true;
            OnPanelOpening();
        }

        private static void AfterEnable(Panel_Crafting panel, bool enable)
        {
            if (!enable) return;
            insideEnable = false;
            CraftingPanelUI.OnPanelOpened(panel, "Enable");
        }

        [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.Enable), new[] { typeof(bool) })]
        private static class Enable
        {
            private static void Prefix(bool enable) => BeforeEnable(enable);
            private static void Postfix(Panel_Crafting __instance, bool enable) => AfterEnable(__instance, enable);
        }

        [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.Enable), new[] { typeof(bool), typeof(bool) })]
        private static class EnableFromPanel
        {
            private static void Prefix(bool enable) => BeforeEnable(enable);
            private static void Postfix(Panel_Crafting __instance, bool enable) => AfterEnable(__instance, enable);
        }

        /// <summary>
        /// Fallback in case a game version opens the crafting menu without calling Enable:
        /// ApplyFilter builds the recipe list on every opening. Runs before it, so recipes are already sorted by mod.
        /// Does nothing once the UI is built, so it doesn't interfere with the normal path.
        /// </summary>
        [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.ApplyFilter))]
        private static class ApplyFilter
        {
            private static void Prefix(Panel_Crafting __instance)
            {
                if (insideEnable || !CraftingPanelUI.NeedsBuild(__instance)) return;
                OnPanelOpening();
                CraftingPanelUI.OnPanelOpened(__instance, "ApplyFilter");
            }
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
            private static void Postfix(Panel_Crafting __instance, BlueprintData bpi, ref bool __result)
            {
                if (__result && bpi != null)
                    __result = RecipeFilter.IsVisible(bpi, CraftingPanelUI.ActiveMod, __instance.CanCraftBlueprint);
            }
        }

        /// <summary>Category selected (mouse or gamepad) — scroll the column to it.</summary>
        [HarmonyPatch(typeof(CategoryButtonNavigation), nameof(CategoryButtonNavigation.SetCurrentIndex))]
        private static class NavigationIndexChanged
        {
            private static void Postfix(CategoryButtonNavigation __instance, int index) =>
                CraftingPanelUI.OnNavigationIndexChanged(__instance, index);
        }

        /// <summary>
        /// On every scene change the game calls SetActive(false) on each object in the category navigation's
        /// m_NotificationFlags and throws a NullReferenceException on a destroyed one (no null check there).
        /// Safety net: replace destroyed entries with an inactive placeholder before that. Replace, not remove,
        /// so the list keeps its length. Normally there is nothing to replace (see ModCategoryButtons.Clear).
        /// </summary>
        [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.ResetNotificationsData))]
        private static class ResetNotificationsData
        {
            private static void Prefix(Panel_Crafting __instance)
            {
                try
                {
                    var nav = __instance.m_CategoryNavigation;
                    var flags = nav != null ? nav.m_NotificationFlags : null;
                    if (flags == null) return;

                    int replaced = 0;
                    for (int i = 0; i < flags.Count; i++)
                    {
                        if (flags[i] != null) continue; // Unity null check: also true for destroyed objects
                        var placeholder = new GameObject("CC_NotificationPlaceholder");
                        placeholder.transform.SetParent(nav.transform, false);
                        placeholder.SetActive(false);
                        flags[i] = placeholder;
                        replaced++;
                    }
                    if (replaced > 0)
                        Main.Log.Warning($"Replaced {replaced} destroyed notification flag(s) in the crafting menu before scene change");
                }
                catch (System.Exception e)
                {
                    Main.Log.Error("Could not check the crafting menu notification flags: " + e);
                }
            }
        }
    }
}
