using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Il2CppTLD.Gear;
using MelonLoader.Utils;

namespace CraftCategories
{
    /// <summary>Where a mod's recipes are shown.</summary>
    public enum Placement
    {
        /// <summary>Only in the mod's own category.</summary>
        ModCategory,

        /// <summary>Only in the game's standard categories (no separate category).</summary>
        GameCategories,

        /// <summary>Both in the mod's category and in the game's standard categories.</summary>
        Both,
    }

    /// <summary>
    /// Mod settings: UserData/CraftCategories/config.json.
    /// Newly found mods and recipes are added to the file automatically.
    /// </summary>
    internal sealed class Config
    {
        /// <summary>How many category buttons are visible without scrolling.</summary>
        public int VisibleCategoryRows { get; set; } = 8;

        /// <summary>Show the mod name when hovering over its category button.</summary>
        public bool ShowModNameOnHover { get; set; } = true;

        public SortedDictionary<string, ModConfig> Mods { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public sealed class ModConfig
        {
            public Placement Placement { get; set; } = Placement.ModCategory;

            /// <summary>Position of the mod's category (1 = right below the game's categories). 0 = not assigned yet.</summary>
            public int Order { get; set; }

            /// <summary>
            /// Where the mod comes from: "modcomponent:&lt;file&gt;" or "dll:&lt;assembly&gt;". Used at startup to check whether
            /// the mod is still installed, so removed mods are hidden from the menu (their settings are kept in case of reinstall).
            /// </summary>
            public string Source { get; set; }

            /// <summary>The mod's recipes: recipe name -> settings.</summary>
            public SortedDictionary<string, ItemConfig> Items { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        public sealed class ItemConfig
        {
            public bool Show { get; set; } = true;

            /// <summary>For reference only: a readable recipe name.</summary>
            public string Name { get; set; }
        }

        // ---------- load / save ----------

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter() },
        };

        public static Config Current { get; private set; } = new();

        /// <summary>Incremented on every settings change so the UI knows it has to rebuild.</summary>
        public static int Version { get; private set; }

        private static string DataDirectory => Path.Combine(MelonEnvironment.UserDataDirectory, "CraftCategories");
        private static string FilePath => Path.Combine(DataDirectory, "config.json");

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    Current = JsonSerializer.Deserialize<Config>(File.ReadAllText(FilePath), JsonOptions) ?? new Config();
            }
            catch (Exception e)
            {
                Main.Log.Error($"Could not read {FilePath}, using default settings: {e.Message}");
                Current = new Config();
            }

            // Deserialized dictionaries lose case-insensitive comparison — restore it.
            Current.Mods = new SortedDictionary<string, ModConfig>(Current.Mods ?? new(), StringComparer.OrdinalIgnoreCase);
            foreach (var mc in Current.Mods.Values)
                mc.Items = new SortedDictionary<string, ItemConfig>(mc.Items ?? new(), StringComparer.OrdinalIgnoreCase);
            Version++;
        }

        public static void Save()
        {
            Version++;
            try
            {
                Directory.CreateDirectory(DataDirectory);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, JsonOptions));
            }
            catch (Exception e)
            {
                Main.Log.Error($"Could not save {FilePath}: {e.Message}");
            }
        }

        // ---------- adding mods ----------

        /// <summary>At startup: adds mods and recipes found in .modcomponent archives.</summary>
        public static void SyncWithArchives()
        {
            bool changed = false;
            foreach (var (mod, items) in BlueprintSources.ItemsByMod)
            {
                var mc = GetOrAddMod(mod, ref changed);
                changed |= SetSource(mc, BlueprintSources.SourceOfMod(mod));
                foreach (var (bpName, display) in items) changed |= AddItem(mc, bpName, display);
            }
            if (changed) Save();
        }

        /// <summary>In game: adds mods and recipes from the loaded catalog (including ones added by DLL mods).</summary>
        public static void SyncWithCatalog()
        {
            bool changed = false;
            foreach (var mod in ModCatalog.Mods)
            {
                var mc = GetOrAddMod(mod.Name, ref changed);
                if (mc.Source == null) changed |= SetSource(mc, mod.Source);
                foreach (var bp in mod.Blueprints)
                    changed |= AddItem(mc, bp.name, bp.m_CraftedResultGear != null ? bp.m_CraftedResultGear.name : null);
            }
            if (changed) Save();

            // Mods first found in game (e.g. from a DLL) get a position at the end of the list.
            NormalizeOrder(ModCatalog.Mods.Select(m => m.Name));
        }

        /// <summary>
        /// Assigns positions to installed mods: new ones (no position yet) go to the end,
        /// then the numbers are compacted to 1..N without gaps. Positions of removed mods are left untouched.
        /// </summary>
        public static void NormalizeOrder(IEnumerable<string> presentMods)
        {
            var present = presentMods
                .Select(name => (name, mc: Current.ForMod(name)))
                .Where(p => p.mc != null)
                .ToList();
            if (present.Count == 0) return;

            bool changed = false;
            int next = present.Max(p => p.mc.Order);
            foreach (var p in present.Where(p => p.mc.Order <= 0))
            {
                p.mc.Order = ++next; // new mods go to the end, in discovery order
                changed = true;
            }

            int position = 1;
            foreach (var p in present.OrderBy(p => p.mc.Order).ThenBy(p => p.name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (p.mc.Order != position) { p.mc.Order = position; changed = true; }
                position++;
            }
            if (changed) Save();
        }

        private static ModConfig GetOrAddMod(string mod, ref bool changed)
        {
            if (Current.Mods.TryGetValue(mod, out var mc)) return mc;
            Current.Mods[mod] = mc = new ModConfig();
            changed = true;
            Main.Log.Msg($"Found crafting mod: {mod}");
            return mc;
        }

        private static bool SetSource(ModConfig mc, string source)
        {
            if (source == null || mc.Source == source) return false;
            mc.Source = source;
            return true;
        }

        private static bool AddItem(ModConfig mc, string bpName, string displayName)
        {
            if (mc.Items.TryGetValue(bpName, out var ic))
            {
                if (ic.Name != null || displayName == null) return false;
                ic.Name = displayName;
                return true;
            }
            mc.Items[bpName] = new ItemConfig { Name = displayName };
            return true;
        }

        // ---------- queries ----------

        public ModConfig ForMod(string mod) => mod != null && Mods.TryGetValue(mod, out var mc) ? mc : null;

        public Placement PlacementOf(string mod) => ForMod(mod)?.Placement ?? Placement.ModCategory;

        public int OrderOf(string mod) => ForMod(mod) is { Order: > 0 } mc ? mc.Order : int.MaxValue;

        public bool IsItemShown(string mod, BlueprintData bp)
        {
            var mc = ForMod(mod);
            return mc == null || !mc.Items.TryGetValue(bp.name, out var ic) || ic.Show;
        }
    }
}
