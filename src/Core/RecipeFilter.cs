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
        public static bool IsVisible(BlueprintData bp, string activeMod)
        {
            var cfg = Config.Current;
            var mod = ModCatalog.ModOf(bp);

            if (mod != null && !cfg.IsItemShown(mod, bp)) return false; // recipe hidden in settings

            if (activeMod != null) return mod == activeMod;              // mod category: only its own recipes
            if (mod == null) return true;                                // game category: game recipes always
            return cfg.PlacementOf(mod) != Placement.ModCategory;        // mod recipes — if configured so
        }

        /// <summary>How many recipes the mod category will contain.</summary>
        public static int CountInModCategory(ModCatalog.ModEntry mod) =>
            mod.Blueprints.Count(bp => Config.Current.IsItemShown(mod.Name, bp));
    }
}
