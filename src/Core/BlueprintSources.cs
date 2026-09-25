using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using MelonLoader.Utils;

namespace CraftCategories
{
    /// <summary>
    /// Determines which mod added a recipe.
    /// Sources: contents of .modcomponent archives and hooks on the recipe registration API used by DLL mods.
    /// </summary>
    internal static class BlueprintSources
    {
        public sealed class Source
        {
            public string ModName;
            public string Origin; // how it was found: "modcomponent:<file>" or "dll:<assembly>"
        }

        private static readonly Regex NameRx = new(@"""Name""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
        private static readonly Regex ResultRx = new(@"""CraftedResult""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);

        private static readonly Dictionary<string, Source> byName = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<Source>> byResult = new(StringComparer.OrdinalIgnoreCase);

        // Mod -> (recipe name in game -> name from json). Known at startup, before the game loads recipes.
        private static readonly SortedDictionary<string, SortedDictionary<string, string>> itemsByMod = new(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> modSources = new(StringComparer.OrdinalIgnoreCase);

        public static int KnownCount => byName.Count / 2;

        public static string SourceOfMod(string mod) => modSources.TryGetValue(mod, out var s) ? s : null;

        /// <summary>Whether a mod with this source is currently installed (see Config.ModConfig.Source).</summary>
        public static bool IsSourcePresent(string source)
        {
            if (string.IsNullOrEmpty(source)) return false;
            if (source.StartsWith("modcomponent:"))
                return File.Exists(Path.Combine(MelonEnvironment.ModsDirectory, source.Substring("modcomponent:".Length)));
            if (source.StartsWith("dll:"))
            {
                var asm = source.Substring("dll:".Length);
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    if (string.Equals(a.GetName().Name, asm, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static IReadOnlyDictionary<string, SortedDictionary<string, string>> ItemsByMod => itemsByMod;

        public static void ScanModComponentArchives()
        {
            var modsDir = MelonEnvironment.ModsDirectory;
            foreach (var file in Directory.GetFiles(modsDir, "*.modcomponent"))
            {
                try
                {
                    using var zip = ZipFile.OpenRead(file);
                    var modName = Path.GetFileNameWithoutExtension(file);
                    var info = zip.GetEntry("BuildInfo.json");
                    if (info != null)
                    {
                        var m = NameRx.Match(ReadEntry(info));
                        if (m.Success) modName = m.Groups[1].Value;
                    }

                    var origin = "modcomponent:" + Path.GetFileName(file);
                    foreach (var entry in zip.Entries)
                    {
                        if (!entry.FullName.StartsWith("blueprints/", StringComparison.OrdinalIgnoreCase) ||
                            !entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            continue;
                        Register(ReadEntry(entry), modName, origin);
                    }
                }
                catch (Exception e)
                {
                    Main.Log.Warning($"Could not read {Path.GetFileName(file)}: {e.Message}");
                }
            }
        }

        private static string ReadEntry(ZipArchiveEntry entry)
        {
            using var r = new StreamReader(entry.Open());
            return r.ReadToEnd();
        }

        private static void Register(string json, string modName, string origin)
        {
            var src = new Source { ModName = modName, Origin = origin };
            var name = NameRx.Match(json);
            if (name.Success)
            {
                // ModComponent creates BlueprintData named "BP_" + Name from json.
                byName[name.Groups[1].Value] = src;
                byName["BP_" + name.Groups[1].Value] = src;

                modSources.TryAdd(modName, origin);
                if (!itemsByMod.TryGetValue(modName, out var items))
                    itemsByMod[modName] = items = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                items["BP_" + name.Groups[1].Value] = name.Groups[1].Value;
            }
            var result = ResultRx.Match(json);
            if (result.Success)
            {
                if (!byResult.TryGetValue(result.Groups[1].Value, out var list))
                    byResult[result.Groups[1].Value] = list = new List<Source>();
                list.Add(src);
            }
        }

        public static Source FindByName(string name) =>
            name != null && byName.TryGetValue(name, out var s) ? s : null;

        public static IReadOnlyList<Source> FindByResult(string gearName) =>
            gearName != null && byResult.TryGetValue(gearName, out var l) ? l : null;

        // ---------- Hooks on recipe registration by DLL mods ----------

        public static void PatchModComponentApi(HarmonyLib.Harmony harmony)
        {
            var prefix = new HarmonyMethod(typeof(BlueprintSources), nameof(CaptureCallerPrefix));

            var mcApi = AccessTools.Method("CraftingRevisions.BlueprintManager:AddBlueprintFromJson", new[] { typeof(string) });
            if (mcApi != null) harmony.Patch(mcApi, prefix);
            else Main.Log.Warning("ModComponent AddBlueprintFromJson not found, recipes added by DLL mods will go to \"Other mods\"");

            var gameApi = AccessTools.Method(typeof(Il2CppTLD.Gear.BlueprintManager), nameof(Il2CppTLD.Gear.BlueprintManager.LoadUserBlueprint));
            if (gameApi != null) harmony.Patch(gameApi, prefix);
        }

        private static readonly HashSet<string> ignoredAssemblies = new(StringComparer.OrdinalIgnoreCase)
        {
            "CraftCategories", "ModComponent", "0Harmony", "MelonLoader", "Il2CppInterop.Runtime",
            "Il2CppInterop.HarmonySupport", "Assembly-CSharp", "mscorlib", "System.Private.CoreLib",
        };

        // The first parameter of the hooked methods is the recipe json text.
        private static void CaptureCallerPrefix(string __0)
        {
            try
            {
                if (__0 is not string json) return;
                var caller = FindExternalCaller();
                if (caller == null) return; // called by ModComponent itself — source already known from archives
                Register(json, caller, "dll:" + caller);
            }
            catch (Exception e)
            {
                Main.Log?.Warning("Could not detect the mod that registered a recipe: " + e.Message);
            }
        }

        private static string FindExternalCaller()
        {
            foreach (var frame in new StackTrace(2, false).GetFrames())
            {
                var asm = frame.GetMethod()?.DeclaringType?.Assembly;
                if (asm == null || asm.IsDynamic) continue;
                var n = asm.GetName().Name;
                if (ignoredAssemblies.Contains(n) || n.StartsWith("System.") || n.StartsWith("Il2Cpp")) continue;
                return n;
            }
            return null;
        }
    }
}
