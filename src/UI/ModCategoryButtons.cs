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
        }

        private readonly Panel_Crafting panel;
        private readonly Transform grid;
        private readonly int vanillaCount; // how many buttons the game had
        private readonly float firstY;      // position of the first mod button
        private readonly List<Entry> entries = new();

        public event Action<Entry> Clicked;
        public event Action<Entry, bool> Hovered;

        public ModCategoryButtons(Panel_Crafting panel, Transform grid)
        {
            this.panel = panel;
            this.grid = grid;
            vanillaCount = Navigation.m_NavigationButtons.Count;

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

            var nav = Navigation;
            TrimToVanilla(nav.m_NavigationButtons);
            TrimToVanilla(nav.m_CategoryButtons);
            TrimToVanilla(nav.m_NotificationFlags);

            foreach (var e in entries)
            {
                if (e.Button == null) continue;
                e.Button.transform.SetParent(null, false); // remove from the column now, Destroy happens at the end of the frame
                Object.Destroy(e.Button.gameObject);
            }
            entries.Clear();

            if (nav.m_CurrentIndex >= vanillaCount) nav.SetCurrentIndex(0, true);
        }

        private void TrimToVanilla<T>(Il2CppSystem.Collections.Generic.List<T> list)
        {
            if (list != null && list.Count > vanillaCount)
                list.RemoveRange(vanillaCount, list.Count - vanillaCount);
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

            // Register in the game's navigation (the lists must have the same length).
            nav.m_NavigationButtons.Add(entry.Button);
            nav.m_CategoryButtons?.Add(new CategoryButton(entry.Button));
            var flag = go.transform.Find("NotificationIconPrefab_V2");
            if (flag != null)
            {
                flag.gameObject.SetActive(false);
                nav.m_NotificationFlags?.Add(flag.gameObject);
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
