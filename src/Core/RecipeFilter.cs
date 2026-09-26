using System;
using System.Linq;
using Il2CppTLD.Gear;

namespace CraftCategories
{
    /// <summary>
    /// Recipe visibility rule for the crafting menu. The single place that decides which recipe is shown where.
    /// </summary>
    internal static class RecipeFilter
    {
        /// <param name="activeMod">Mod whose category is selected, or null if a game category is selected.</param>
        /// <param name="canCraft">Whether the player can craft the recipe right now (the game's own check).</param>
        public static bool IsVisible(BlueprintData bp, string activeMod, Func<BlueprintData, bool> canCraft)
        {
            var cfg = Config.Current;
            var mod = ModCatalog.ModOf(bp);

            if (mod != null && !cfg.IsItemShown(mod, bp)) return false; // recipe hidden in settings

            if (activeMod != null) return mod == activeMod;              // mod category: only its own recipes
            if (mod == null) return true;                                // game category: game recipes always
            if (cfg.PlacementOf(mod) != Placement.ModCategory) return true; // mod recipes — if configured so

            // Optionally: recipes the player can craft right now also appear in game categories.
            return cfg.CraftableInGameCategories && canCraft(bp);
        }

        /// <summary>How many recipes the mod category will contain.</summary>
        public static int CountInModCategory(ModCatalog.ModEntry mod) =>
            mod.Blueprints.Count(bp => Config.Current.IsItemShown(mod.Name, bp));
    }
}
