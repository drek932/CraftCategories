using System;
using Il2Cpp;
using UnityEngine;

namespace CraftCategories
{
    /// <summary>
    /// Connects the mod's UI parts in the crafting menu: the scrollable column, mod buttons, hover hint and header.
    /// Receives events from the patches (Patches/CraftingPatches.cs) and keeps track of the selected mod category.
    /// </summary>
    internal static class CraftingPanelUI
    {
        private static Panel_Crafting panel;
        private static CategoryColumn column;
        private static ModCategoryButtons modButtons;
        private static CategoryTooltip tooltip;
        private static HeaderTitle header;
        private static int builtConfigVersion = -1;

        /// <summary>Mod whose category is currently selected, or null if a game category is selected.</summary>
        public static string ActiveMod { get; private set; }

        // ---------- building ----------

        /// <summary>Called every time the crafting menu is opened.</summary>
        public static void OnPanelOpened(Panel_Crafting opened)
        {
            try
            {
                bool isNewPanel = panel == null || panel.Pointer != opened.Pointer || column == null || !column.IsAlive;
                if (isNewPanel) Build(opened);
                else if (builtConfigVersion != Config.Version) RebuildModButtons();

                tooltip?.Hide();
                column.ScrollToTop();
                OnNavigationIndexChanged(opened.m_CategoryNavigation, opened.m_CategoryNavigation.m_CurrentIndex);
            }
            catch (Exception e)
            {
                Main.Log.Error("Could not add categories to the crafting menu: " + e);
            }
        }

        private static void Build(Panel_Crafting opened)
        {
            panel = opened;
            ActiveMod = null;

            var nav = opened.m_CategoryNavigation;
            var allButton = nav.m_NavigationButtons[0];
            var grid = allButton.transform.parent;

            var headerLabel = opened.transform.Find("Root/CraftingSection/BlueprintList/Label_Header")?.GetComponent<UILabel>();
            header = new HeaderTitle(headerLabel);

            // Mod buttons are created before the column: the column remembers the game buttons' position before moving them.
            modButtons = new ModCategoryButtons(opened, grid);
            modButtons.Clicked += OnModButtonClicked;
            modButtons.Hovered += OnModButtonHovered;

            column = new CategoryColumn(opened, grid, Math.Max(3, Config.Current.VisibleCategoryRows));

            tooltip = headerLabel != null ? new CategoryTooltip(opened, headerLabel) : null;

            RebuildModButtons();
        }

        private static void RebuildModButtons()
        {
            ActiveMod = null;
            modButtons.Rebuild();
            column.Refresh();
            builtConfigVersion = Config.Version;
        }

        // ---------- mod button events ----------

        private static void OnModButtonClicked(ModCategoryButtons.Entry entry)
        {
            try
            {
                var nav = panel.m_CategoryNavigation;
                nav.OnNavigationChanged(entry.Button);
                if (ActiveMod == entry.Mod.Name) return;

                // The game's navigation did not switch the category itself — switch it directly.
                int index = nav.m_NavigationButtons.IndexOf(entry.Button);
                nav.SetCurrentIndex(index, true);
                if (ActiveMod != entry.Mod.Name) panel.OnCategoryChanged(index);
                if (ActiveMod != entry.Mod.Name) Main.Log.Warning($"Could not select category {entry.Mod.Name} (index {index})");
            }
            catch (Exception e)
            {
                Main.Log.Error($"Error while selecting category {entry.Mod.Name}: {e}");
            }
        }

        private static void OnModButtonHovered(ModCategoryButtons.Entry entry, bool over)
        {
            if (tooltip == null) return;
            if (!over || !Config.Current.ShowModNameOnHover)
            {
                tooltip.Hide();
                return;
            }
            tooltip.Show(ModCatalog.DisplayName(entry.Mod.Name), entry.Button.transform);
        }

        // ---------- game events (from patches) ----------

        /// <summary>
        /// Before the game changes category. For a mod button the game gets "All" (index 0),
        /// and we narrow the list ourselves via RecipeFilter.
        /// </summary>
        public static int OnCategoryChanging(int index)
        {
            var entry = modButtons?.AtNavigationIndex(index);
            ActiveMod = entry?.Mod.Name;
            return entry != null ? 0 : index;
        }

        public static void OnCategoryChanged() => header?.Update(ActiveMod);

        /// <summary>The selected category (including via gamepad) must be visible in the column.</summary>
        public static void OnNavigationIndexChanged(CategoryButtonNavigation nav, int index)
        {
            if (panel == null || column == null || nav.Pointer != panel.m_CategoryNavigation.Pointer) return;
            var buttons = nav.m_NavigationButtons;
            if (index < 0 || index >= buttons.Count) return;

            var button = buttons[index].transform;
            if (nav.m_SelectedHighlight != null) nav.m_SelectedHighlight.transform.position = button.position;
            column.ScrollIntoView(button);
        }

        // ---------- every frame ----------

        public static void Update()
        {
            if (panel == null || column == null || !column.IsAlive) return;
            if (!panel.IsEnabled()) return;
            column.HandleMouseWheel(panel);
        }

        /// <summary>The game may overwrite the header itself, so keep it up to date every frame.</summary>
        public static void LateUpdate()
        {
            if (panel == null || header == null || !panel.IsEnabled()) return;
            header.Update(ActiveMod);
        }
    }
}
