using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ModSettings;

namespace CraftCategories
{
    /// <summary>
    /// Base class for the generated settings: forwards ModSettings events to SettingsMenu.
    /// </summary>
    public abstract class DynamicSettingsBase : ModSettingsBase
    {
        protected override void OnConfirm() => SettingsMenu.Apply(this);

        protected override void OnChange(FieldInfo field, object oldValue, object newValue) =>
            SettingsMenu.OnFieldChanged(this, field, oldValue, newValue);
    }

    /// <summary>
    /// The "Craft Categories" menu in ModSettings.
    ///
    /// ModSettings builds its menu only from the fields of a class, but the list of mods is only known at startup.
    /// So the settings class is generated on the fly (Reflection.Emit): for every installed mod there is a
    /// section with "Where to show recipes", "Category position", "Show recipe list" and one field per recipe.
    /// </summary>
    // public: the class generated in the dynamic assembly calls FillValues.
    public static class SettingsMenu
    {
        private const string MenuName = "Craft Categories";

        private enum Kind
        {
            // General settings
            ShowModNameOnHover,

            // Per-mod settings
            Placement,
            Order,
            ShowRecipeList,
            Recipe,
        }

        /// <summary>What a generated field means.</summary>
        private sealed class FieldMeta
        {
            public Kind Kind;
            public string Mod;    // null = general setting
            public string Recipe; // for Kind.Recipe
        }

        private sealed class ModSection
        {
            public string Mod;
            public Config.ModConfig Config;
            public List<(string bpName, string display)> Recipes;
        }

        private static readonly Dictionary<string, FieldMeta> fields = new();
        private static readonly Dictionary<string, List<string>> recipeFieldsByMod = new();
        private static readonly List<string> orderFields = new();

        // ---------- building the menu ----------

        public static void Register()
        {
            var sections = CollectInstalledMods();
            var settingsType = GenerateSettingsType(sections);
            var settings = (DynamicSettingsBase)Activator.CreateInstance(settingsType);
            settings.AddToModSettings(MenuName);

            // Recipe lists start collapsed.
            foreach (var mod in recipeFieldsByMod.Keys)
                SetRecipeListVisible(settings, mod, false);

            Main.Log.Msg($"Settings menu: {sections.Count} mods, {sections.Sum(s => s.Recipes.Count)} recipes");
        }

        /// <summary>
        /// Only currently installed mods and only their current recipes, in category order.
        /// Entries for removed mods and recipes stay in config.json but are not shown in the menu.
        /// </summary>
        private static List<ModSection> CollectInstalledMods()
        {
            var sections = new List<ModSection>();
            foreach (var (mod, mc) in Config.Current.Mods)
            {
                if (BlueprintSources.ItemsByMod.TryGetValue(mod, out var archiveRecipes))
                    sections.Add(new ModSection { Mod = mod, Config = mc, Recipes = archiveRecipes.Select(kv => (kv.Key, kv.Value)).ToList() });
                else if (BlueprintSources.IsSourcePresent(mc.Source))
                    sections.Add(new ModSection { Mod = mod, Config = mc, Recipes = mc.Items.Select(kv => (kv.Key, kv.Value.Name ?? kv.Key)).ToList() });
            }

            Config.NormalizeOrder(sections.Select(s => s.Mod));
            return sections.OrderBy(s => s.Config.Order).ToList();
        }

        private static Type GenerateSettingsType(List<ModSection> sections)
        {
            var tb = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CraftCategories.GeneratedSettings"), AssemblyBuilderAccess.Run)
                .DefineDynamicModule("Main")
                .DefineType("CraftCategoriesSettings", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, typeof(DynamicSettingsBase));

            DefineGeneralSection(tb);
            for (int i = 0; i < sections.Count; i++)
                DefineModSection(tb, $"m{i}", sections[i], sections.Count);

            DefineConstructor(tb);
            return tb.CreateType();
        }

        /// <summary>General settings — at the very top of the menu.</summary>
        private static void DefineGeneralSection(TypeBuilder tb)
        {
            var hover = DefineField(tb, "g_hoverName", typeof(bool), new FieldMeta { Kind = Kind.ShowModNameOnHover },
                Loc.Key("HOVER_NAME"), Loc.Key("HOVER_NAME_DESC"));
            hover.SetCustomAttribute(TextAttribute<SectionAttribute>(Loc.Key("GENERAL"), localize: true));
        }

        private static void DefineModSection(TypeBuilder tb, string prefix, ModSection s, int modCount)
        {
            // Where to show recipes (first field of the section — the header with the mod name is attached to it).
            var placement = DefineField(tb, prefix + "_placement", typeof(int), new FieldMeta { Kind = Kind.Placement, Mod = s.Mod },
                Loc.Key("PLACEMENT"), Loc.Key("PLACEMENT_DESC"));
            placement.SetCustomAttribute(TextAttribute<SectionAttribute>(s.Mod, localize: false));
            placement.SetCustomAttribute(ChoiceAttribute("PLACEMENT_MOD", "PLACEMENT_GAME", "PLACEMENT_BOTH"));

            // Category position — only if there is something to reorder.
            if (modCount > 1)
            {
                var order = DefineField(tb, prefix + "_order", typeof(int), new FieldMeta { Kind = Kind.Order, Mod = s.Mod },
                    Loc.Key("ORDER"), Loc.Key("ORDER_DESC"));
                order.SetCustomAttribute(new CustomAttributeBuilder(
                    typeof(SliderAttribute).GetConstructor(new[] { typeof(float), typeof(float) }),
                    new object[] { 1f, (float)modCount }));
                orderFields.Add(order.Name);
            }

            // Expand toggle and the recipes themselves.
            DefineField(tb, prefix + "_list", typeof(bool), new FieldMeta { Kind = Kind.ShowRecipeList, Mod = s.Mod },
                Loc.Key("RECIPES", s.Recipes.Count), Loc.Key("RECIPES_DESC"));

            var recipeFields = recipeFieldsByMod[s.Mod] = new List<string>();
            for (int j = 0; j < s.Recipes.Count; j++)
            {
                var (bpName, display) = s.Recipes[j];
                // The recipe name is taken from the mod as is; only the description is translated.
                var f = DefineField(tb, $"{prefix}_r{j}", typeof(bool), new FieldMeta { Kind = Kind.Recipe, Mod = s.Mod, Recipe = bpName },
                    "    " + display, Loc.Key("ITEM_DESC", bpName), localizeName: false);
                recipeFields.Add(f.Name);
            }
        }

        private static FieldBuilder DefineField(TypeBuilder tb, string name, Type type, FieldMeta meta,
            string title, string description, bool localizeName = true)
        {
            var f = tb.DefineField(name, type, FieldAttributes.Public);
            f.SetCustomAttribute(TextAttribute<NameAttribute>(title, localizeName));
            // The description is an untranslated key: it is translated when shown (see ModSettingsPatches).
            f.SetCustomAttribute(TextAttribute<DescriptionAttribute>(description, localize: false));
            fields[name] = meta;
            return f;
        }

        /// <summary>
        /// A ModSettings attribute with a single text. With localize = true the text is a "CRAFTCAT_…" key
        /// that the game translates through Localization.Get (see LocalizationPatches).
        /// </summary>
        private static CustomAttributeBuilder TextAttribute<T>(string text, bool localize) =>
            new(typeof(T).GetConstructor(new[] { typeof(string) }), new object[] { text },
                new[] { typeof(T).GetProperty("Localize") }, new object[] { localize });

        private static CustomAttributeBuilder ChoiceAttribute(params string[] locKeys) =>
            new(typeof(ModSettings.ChoiceAttribute).GetConstructor(new[] { typeof(string[]) }),
                new object[] { locKeys.Select(k => Loc.Key(k)).ToArray() },
                new[] { typeof(ModSettings.ChoiceAttribute).GetProperty("Localize") }, new object[] { true });

        /// <summary>
        /// Constructor: first fill the fields with values from config.json, then call the base constructor,
        /// so ModSettings remembers exactly these as the confirmed values.
        /// </summary>
        private static void DefineConstructor(TypeBuilder tb)
        {
            var il = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes).GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(SettingsMenu).GetMethod(nameof(FillValues), BindingFlags.Public | BindingFlags.Static));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(DynamicSettingsBase).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null));
            il.Emit(OpCodes.Ret);
        }

        // ---------- values ----------

        /// <summary>Config -> menu fields. Called from the generated constructor.</summary>
        public static void FillValues(object settings)
        {
            foreach (var (field, meta, mc) in OurFields(settings))
            {
                object value = meta.Kind switch
                {
                    Kind.ShowModNameOnHover => Config.Current.ShowModNameOnHover,
                    Kind.Placement => (int)mc.Placement,
                    Kind.Order => Math.Max(1, mc.Order),
                    Kind.Recipe => !mc.Items.TryGetValue(meta.Recipe, out var ic) || ic.Show,
                    _ => false,
                };
                field.SetValue(settings, value);
            }
        }

        /// <summary>Menu fields -> Config. Called when the player presses "Apply".</summary>
        public static void Apply(DynamicSettingsBase settings)
        {
            foreach (var (field, meta, mc) in OurFields(settings))
            {
                switch (meta.Kind)
                {
                    case Kind.ShowModNameOnHover: Config.Current.ShowModNameOnHover = (bool)field.GetValue(settings); break;
                    case Kind.Placement: mc.Placement = (Placement)(int)field.GetValue(settings); break;
                    case Kind.Order: mc.Order = (int)field.GetValue(settings); break;
                    case Kind.Recipe when mc.Items.TryGetValue(meta.Recipe, out var ic): ic.Show = (bool)field.GetValue(settings); break;
                }
            }
            Config.Save();
        }

        /// <summary>Menu fields with their meaning. For per-mod settings — together with the mod's Config entry (null for general ones).</summary>
        private static IEnumerable<(FieldInfo field, FieldMeta meta, Config.ModConfig mc)> OurFields(object settings)
        {
            foreach (var f in settings.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!fields.TryGetValue(f.Name, out var meta)) continue;
                var mc = Config.Current.ForMod(meta.Mod);
                if (meta.Mod == null || mc != null) yield return (f, meta, mc);
            }
        }

        // ---------- reacting to menu changes (before "Apply") ----------

        public static void OnFieldChanged(DynamicSettingsBase settings, FieldInfo field, object oldValue, object newValue)
        {
            if (!fields.TryGetValue(field.Name, out var meta)) return;

            if (meta.Kind == Kind.ShowRecipeList)
                SetRecipeListVisible(settings, meta.Mod, newValue is true);
            else if (meta.Kind == Kind.Order && oldValue is int from && newValue is int to && from != to)
                ShiftOtherPositions(settings, field.Name, from, to);
        }

        private static void SetRecipeListVisible(DynamicSettingsBase settings, string mod, bool visible)
        {
            foreach (var name in recipeFieldsByMod[mod]) settings.SetFieldVisible(name, visible);
        }

        /// <summary>
        /// A category moved from position "from" to "to": the mods in between shift by one,
        /// so the numbers always stay a permutation of 1..N.
        /// </summary>
        private static void ShiftOtherPositions(DynamicSettingsBase settings, string movedField, int from, int to)
        {
            foreach (var name in orderFields.Where(n => n != movedField))
            {
                var f = settings.GetType().GetField(name);
                int v = (int)f.GetValue(settings);
                if (from < to && v > from && v <= to) f.SetValue(settings, v - 1);
                else if (to < from && v >= to && v < from) f.SetValue(settings, v + 1);
            }
            settings.RefreshGUI();
        }
    }
}
