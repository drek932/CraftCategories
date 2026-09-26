using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppTLD.Gear;

namespace CraftCategories
{
    /// <summary>
    /// Groups all game recipes by the mod that added them.
    /// </summary>
    internal static class ModCatalog
    {
        /// <summary>Key of the group for recipes with an unknown source (shown as Loc "OTHER_MODS").</summary>
        public const string UnknownMod = "Other mods";

        public static string DisplayName(string mod) => mod == UnknownMod ? Loc.T("OTHER_MODS") : mod;

        public sealed class ModEntry
        {
            public string Name;
            public string Source; // see Config.ModConfig.Source; null — source unknown
            public readonly List<BlueprintData> Blueprints = new();
        }

        // BlueprintData pointer -> mod name. A missing key means a game recipe.
        private static readonly Dictionary<IntPtr, string> modOf = new();
        private static readonly List<ModEntry> mods = new();
        private static int builtForCount = -1;

        public static IReadOnlyList<ModEntry> Mods => mods;

        /// <summary>For the troubleshooting line in the log: all recipes, and how many of them are in the game's own list.</summary>
        public static int TotalRecipes { get; private set; }
        public static int RecipesInGameList { get; private set; }

        /// <summary>Rebuilds the catalog if the number of recipes has changed.</summary>
        public static bool Refresh()
        {
            var bm = BlueprintManager.Instance;
            if (bm == null || bm.m_AllBlueprints == null) return false;
            if (bm.m_AllBlueprints.Count == builtForCount) return true;

            modOf.Clear();
            mods.Clear();

            // The game's own recipes are loaded from its resources (addressables). With some mod setups modded recipes
            // end up in that list too, so it only decides for recipes that no known mod claims by name.
            var gameList = new HashSet<IntPtr>();
            if (bm.m_AddressableBlueprints != null)
                foreach (var bp in bm.m_AddressableBlueprints) if (bp != null) gameList.Add(bp.Pointer);

            TotalRecipes = bm.m_AllBlueprints.Count;
            RecipesInGameList = gameList.Count;

            var byMod = new Dictionary<string, ModEntry>();
            foreach (var bp in bm.m_AllBlueprints)
            {
                if (bp == null) continue;

                var source = FindSource(bp, inGameList: gameList.Contains(bp.Pointer));
                if (source == null && gameList.Contains(bp.Pointer)) continue; // a game recipe

                var mod = source?.ModName ?? UnknownMod;

                modOf[bp.Pointer] = mod;
                if (!byMod.TryGetValue(mod, out var entry))
                    byMod[mod] = entry = new ModEntry { Name = mod, Source = source?.Origin };
                entry.Blueprints.Add(bp);
            }

            // Known mods alphabetically, "Other mods" last.
            mods.AddRange(byMod.Values
                .OrderBy(m => m.Name == UnknownMod ? 1 : 0)
                .ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase));
            foreach (var m in mods) m.Blueprints.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            builtForCount = bm.m_AllBlueprints.Count;
            return true;
        }

        /// <summary>
        /// By recipe name first. Then — only for recipes outside the game's list and only if unambiguous — by the item
        /// it crafts (for game recipes this would wrongly move e.g. the game's arrowhead recipe into a mod that also makes arrowheads).
        /// </summary>
        private static BlueprintSources.Source FindSource(BlueprintData bp, bool inGameList)
        {
            if (BlueprintSources.FindByName(bp.name) is { } byName) return byName;
            if (inGameList) return null;

            var result = bp.m_CraftedResultGear != null ? bp.m_CraftedResultGear.name : null;
            return BlueprintSources.FindByResult(result) is { Count: 1 } byResult ? byResult[0] : null;
        }

        /// <summary>Name of the mod that added the recipe, or null for game recipes.</summary>
        public static string ModOf(BlueprintData bp) =>
            bp != null && modOf.TryGetValue(bp.Pointer, out var m) ? m : null;
    }
}
