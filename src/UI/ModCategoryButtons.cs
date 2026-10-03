using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CraftCategories
{
    /// <summary>
    /// Mod category buttons below the game's buttons. Created by copying the "All" button
    /// and registered in the game's navigation so clicks, selection and gamepad work.
    /// </summary>
    internal sealed class ModCategoryButtons
    {
        private const int IconSize = 46;

        public sealed class Entry
        {
            public ModCatalog.ModEntry Mod;
            public UIButton Button;

            // What we added to the game's navigation lists, so exactly these can be removed again.
            public CategoryButton CategoryButton;
            public GameObject NotificationFlag;
        }

        private readonly Panel_Crafting panel;
        private readonly Transform grid;
        private readonly int vanillaCount; // how many buttons the game had
        private readonly float firstY;      // position of the first mod button
        private readonly List<Entry> entries = new();

        public event Action<Entry> Clicked;
        public event Action<Entry, bool> Hovered;

        /// <summary>Sizes of the game's own navigation lists, for the troubleshooting line in the log.</summary>
        public string VanillaListSizes { get; }

        public ModCategoryButtons(Panel_Crafting panel, Transform grid)
        {
            this.panel = panel;
            this.grid = grid;
            var nav = Navigation;
            vanillaCount = nav.m_NavigationButtons.Count;
            VanillaListSizes = $"{vanillaCount} buttons, {nav.m_NotificationFlags?.Count ?? 0} notification flags, " +
                               $"{nav.m_CategoryButtons?.Count ?? 0} category buttons";

            float lowestY = 0;
            for (int i = 0; i < grid.childCount; i++)
            {
                var child = grid.GetChild(i);
                if (child.gameObject.activeSelf) lowestY = Mathf.Min(lowestY, child.localPosition.y);
            }
            firstY = lowestY - CategoryColumn.ButtonStep;
        }

        private CategoryButtonNavigation Navigation => panel.m_CategoryNavigation;

        public int Count => entries.Count;

        /// <summary>Mod at the given index of the game's navigation, or null if it is a game button.</summary>
        public Entry AtNavigationIndex(int index)
        {
            int i = index - vanillaCount;
            return i >= 0 && i < entries.Count ? entries[i] : null;
        }

        // ---------- create / remove ----------

        /// <summary>Recreates the buttons from current settings: only mods with a category, in position order.</summary>
        public void Rebuild()
        {
            Clear();

            var cfg = Config.Current;
            var mods = ModCatalog.Mods
                .Where(m => cfg.PlacementOf(m.Name) != Placement.GameCategories)
                .Where(m => RecipeFilter.CountInModCategory(m) > 0)
                .OrderBy(m => cfg.OrderOf(m.Name))
                .ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase);

            foreach (var mod in mods)
                entries.Add(CreateButton(mod, firstY - CategoryColumn.ButtonStep * entries.Count));
        }

        private void Clear()
        {
            if (entries.Count == 0) return;

            // Remove exactly what we added (by reference). The game's lists don't all have the same length,
            // so cutting them at "vanilla button count" left a destroyed flag behind, and the game then threw a
            // NullReferenceException in Panel_Crafting.ResetNotificationsData on every scene change.
            var nav = Navigation;
            foreach (var e in entries)
            {
                nav.m_NavigationButtons.Remove(e.Button);
                if (e.CategoryButton != null) nav.m_CategoryButtons?.Remove(e.CategoryButton);
                if (e.NotificationFlag != null) nav.m_NotificationFlags?.Remove(e.NotificationFlag);

                if (e.Button == null) continue;
                e.Button.transform.SetParent(null, false); // remove from the column now, Destroy happens at the end of the frame
                Object.Destroy(e.Button.gameObject);
            }
            entries.Clear();

            if (nav.m_CurrentIndex >= vanillaCount) nav.SetCurrentIndex(0, true);
        }

        private Entry CreateButton(ModCatalog.ModEntry mod, float y)
        {
            var nav = Navigation;
            var go = Object.Instantiate(nav.m_NavigationButtons[0].gameObject, grid);
            go.name = "CC_Mod_" + mod.Name;
            go.transform.localPosition = new Vector3(0, y, 0);
            go.SetActive(true);

            var entry = new Entry { Mod = mod, Button = go.GetComponent<UIButton>() };

            // Click: our own handler instead of the "All" button handler.
            entry.Button.onClick.Clear();
            EventDelegate.Add(entry.Button.onClick, DelegateSupport.ConvertDelegate<EventDelegate.Callback>(
                new Action(() => Clicked?.Invoke(entry))));

            // Mouse hover — for the hint.
            UIEventListener.Get(go).onHover = DelegateSupport.ConvertDelegate<UIEventListener.BoolDelegate>(
                new Action<GameObject, bool>((_, over) => Hovered?.Invoke(entry, over)));

            SetIcon(go, mod);

            // Register in the game's navigation.
            nav.m_NavigationButtons.Add(entry.Button);
            if (nav.m_CategoryButtons != null)
            {
                entry.CategoryButton = new CategoryButton(entry.Button);
                nav.m_CategoryButtons.Add(entry.CategoryButton);
            }
            var flag = go.transform.Find("NotificationIconPrefab_V2");
            if (flag != null && nav.m_NotificationFlags != null)
            {
                flag.gameObject.SetActive(false);
                entry.NotificationFlag = flag.gameObject;
                nav.m_NotificationFlags.Add(entry.NotificationFlag);
            }
            return entry;
        }

        /// <summary>Category icon — the picture of the first mod item that has one.</summary>
        private static void SetIcon(GameObject buttonGo, ModCatalog.ModEntry mod)
        {
            var icon = FindIcon(mod);
            if (icon == null) return; // keep the "All" icon — better than an empty button

            var sprite = buttonGo.GetComponent<UISprite>();
            if (sprite != null)
            {
                // The sprite must not be disabled: UICamera ignores clicks on a collider with an invisible widget.
                // So shrink it to a dot under the icon and keep the collider at its original size.
                sprite.autoResizeBoxCollider = false;
                sprite.width = 2;
                sprite.height = 2;
            }

            var iconGo = new GameObject("CC_Icon") { layer = buttonGo.layer };
            iconGo.transform.SetParent(buttonGo.transform, false);
            var texture = iconGo.AddComponent<UITexture>();
            texture.mainTexture = icon;
            texture.width = IconSize;
            texture.height = IconSize;
            texture.depth = sprite != null ? sprite.depth + 1 : 13;
        }

        private static Texture2D FindIcon(ModCatalog.ModEntry mod)
        {
            foreach (var bp in mod.Blueprints)
            {
                if (bp.m_CraftedResultGear == null) continue;
                try
                {
                    var icon = Utils.GetInventoryIconTexture(bp.m_CraftedResultGear);
                    if (icon != null) return icon;
                }
                catch
                {
                    // Some mod items have no icon — try the next one.
                }
            }
            return null;
        }
    }
}
