using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace CraftCategories
{
    /// <summary>
    /// Scrollable category column: a fixed-height clipped window with a scrollbar on the left.
    ///
    /// Scrolling:
    ///  • by dragging the thumb or clicking the track;
    ///  • with the mouse wheel — only when the recipe list on the right has no scrollbar of its own
    ///    (one "line" per wheel step, like the game's recipe list);
    ///  • automatically, when a category outside the window is selected (e.g. with a gamepad).
    /// </summary>
    internal sealed class CategoryColumn
    {
        public const float ButtonStep = 62f; // spacing between buttons in the game's column
        private const float ButtonHalf = ButtonStep / 2f;
        private const float Margin = 3f;
        private const float ScrollBarOffsetX = -44f; // scrollbar to the left of the column
        private const float ThumbScale = 0.5f;       // thumb half of its "true" size
        private const float MinThumbHeight = 24f;

        private readonly Transform grid;
        private readonly UIPanel clipPanel;
        private readonly Vector3 basePosition;
        private readonly float clipHeight;
        private readonly float clipCenterY;

        private GameObject barRoot;
        private UISprite thumb;
        private float thumbHeight;
        private float grabOffset;

        private float scroll;      // 0 = top
        private float scrollRange; // maximum scroll

        /// <summary>
        /// Moves the button column (grid) and the selection highlight into a new clipped panel.
        /// </summary>
        public CategoryColumn(Panel_Crafting panel, Transform grid, int visibleRows)
        {
            this.grid = grid;
            var nav = panel.m_CategoryNavigation;

            clipHeight = visibleRows * ButtonStep + 2 * Margin;
            clipCenterY = ButtonHalf + Margin - clipHeight / 2f;

            var holder = new GameObject("CC_CategoryScroll") { layer = grid.gameObject.layer };
            holder.transform.SetParent(nav.transform, false);
            holder.transform.localPosition = grid.localPosition;
            basePosition = holder.transform.localPosition;

            // Anything outside the window is neither drawn nor clickable.
            clipPanel = holder.AddComponent<UIPanel>();
            var parentPanel = panel.GetComponent<UIPanel>();
            clipPanel.depth = (parentPanel != null ? parentPanel.depth : 0) + 1;
            clipPanel.clipping = UIDrawCall.Clipping.SoftClip;
            clipPanel.baseClipRegion = new Vector4(0, clipCenterY, 90, clipHeight);
            clipPanel.clipSoftness = new Vector2(0, 4);

            grid.SetParent(holder.transform, true);
            if (nav.m_SelectedHighlight != null) nav.m_SelectedHighlight.transform.SetParent(holder.transform, true);

            CreateScrollBar(panel, nav.transform, holder.layer);

            // Widgets must find their UIPanel again after the parent change.
            NGUITools.MarkParentAsChanged(holder);
        }

        public bool IsAlive => clipPanel != null;

        // ---------- scrollbar ----------

        /// <summary>Own scrollbar built from the recipe list scrollbar sprites, so it looks like the game's.</summary>
        private void CreateScrollBar(Panel_Crafting panel, Transform parent, int layer)
        {
            var srcBar = panel.m_ScrollBehaviour?.m_ScrollBar;
            var srcTrack = srcBar?.backgroundWidget?.TryCast<UISprite>();
            var srcThumb = srcBar?.foregroundWidget?.TryCast<UISprite>();
            if (srcTrack == null || srcThumb == null)
            {
                Main.Log.Warning("Scrollbar sprites not found, the category scrollbar was not created");
                return;
            }

            barRoot = new GameObject("CC_CategoryScrollBar") { layer = layer };
            barRoot.transform.SetParent(parent, false);
            barRoot.transform.localPosition = basePosition + new Vector3(ScrollBarOffsetX, clipCenterY, 0);

            var track = CreateBarSprite("Track", srcTrack);
            thumb = CreateBarSprite("Thumb", srcThumb);

            foreach (var go in new[] { track.gameObject, thumb.gameObject })
            {
                var listener = UIEventListener.Get(go);
                listener.onPress = DelegateSupport.ConvertDelegate<UIEventListener.BoolDelegate>(new Action<GameObject, bool>(OnBarPress));
                listener.onDrag = DelegateSupport.ConvertDelegate<UIEventListener.VectorDelegate>(new Action<GameObject, Vector2>((_, _) => DragTo(PointerY())));
            }
        }

        private UISprite CreateBarSprite(string name, UISprite src)
        {
            var go = new GameObject(name) { layer = barRoot.layer };
            go.transform.SetParent(barRoot.transform, false);

            var sprite = go.AddComponent<UISprite>();
            sprite.atlas = src.atlas;
            sprite.spriteName = src.spriteName;
            sprite.type = src.type;
            sprite.color = src.color;
            sprite.depth = src.depth;
            sprite.width = src.width;
            sprite.height = (int)clipHeight;

            // Collider wider than the bar itself, so it is easier to hit.
            go.AddComponent<BoxCollider>().size = new Vector3(src.width + 16, clipHeight, 0);
            return sprite;
        }

        /// <summary>Grabbed the thumb — remember the grab point; clicked the track — the thumb jumps under the cursor.</summary>
        private void OnBarPress(GameObject go, bool pressed)
        {
            if (!pressed) return;
            float y = PointerY();
            float thumbY = thumb.transform.localPosition.y;
            bool onThumb = go == thumb.gameObject && Mathf.Abs(y - thumbY) <= thumbHeight / 2f;
            grabOffset = onThumb ? y - thumbY : 0f;
            DragTo(y);
        }

        /// <summary>Vertical cursor position in the scrollbar's coordinate space.</summary>
        private float PointerY()
        {
            var cam = UICamera.currentCamera;
            if (cam == null) return 0;
            var p = UICamera.lastTouchPosition;
            return barRoot.transform.InverseTransformPoint(cam.ScreenToWorldPoint(new Vector3(p.x, p.y, 0))).y;
        }

        private void DragTo(float pointerY)
        {
            float free = clipHeight - thumbHeight;
            if (free <= 0) return;
            float thumbTop = clipHeight / 2f - thumbHeight / 2f;
            float fraction = (thumbTop - (pointerY - grabOffset)) / free;
            SetScroll(Mathf.Clamp01(fraction) * scrollRange);
        }

        // ---------- scrolling ----------

        public void ScrollToTop() => SetScroll(0);

        private void SetScroll(float value)
        {
            scroll = Mathf.Clamp(value, 0, scrollRange);
            clipPanel.transform.localPosition = basePosition + new Vector3(0, scroll, 0);
            clipPanel.clipOffset = new Vector2(0, -scroll);

            if (thumb != null)
            {
                float fraction = scrollRange > 0 ? scroll / scrollRange : 0;
                float y = clipHeight / 2f - thumbHeight / 2f - fraction * (clipHeight - thumbHeight);
                thumb.transform.localPosition = new Vector3(0, y, 0);
            }
        }

        /// <summary>Recalculates the scroll range and thumb size after the set of buttons changes.</summary>
        public void Refresh()
        {
            NGUITools.MarkParentAsChanged(grid.gameObject);

            float lowestButtonY = 0;
            for (int i = 0; i < grid.childCount; i++)
            {
                var child = grid.GetChild(i);
                if (child.gameObject.activeSelf) lowestButtonY = Mathf.Min(lowestButtonY, child.localPosition.y);
            }
            float contentHeight = -lowestButtonY + ButtonStep + 2 * Margin;
            scrollRange = Mathf.Max(0, contentHeight - clipHeight);

            if (barRoot != null)
            {
                barRoot.SetActive(scrollRange > 0);
                thumbHeight = Mathf.Max(MinThumbHeight, clipHeight * (clipHeight / contentHeight) * ThumbScale);
                thumb.height = (int)thumbHeight;
                var collider = thumb.GetComponent<BoxCollider>();
                collider.size = new Vector3(collider.size.x, thumbHeight, 0);
            }
            SetScroll(scroll);
        }

        /// <summary>Scrolls so that the button is inside the visible window.</summary>
        public void ScrollIntoView(Transform button)
        {
            float y = clipPanel.transform.InverseTransformPoint(button.position).y;
            float visibleCenter = clipCenterY - scroll;
            float half = clipHeight / 2f - ButtonHalf - Margin;
            if (y > visibleCenter + half) SetScroll(clipCenterY - (y - half));
            else if (y < visibleCenter - half) SetScroll(clipCenterY - (y + half));
        }

        /// <summary>Mouse wheel. Called every frame while the crafting menu is open.</summary>
        public void HandleMouseWheel(Panel_Crafting panel)
        {
            if (scrollRange <= 0 || !clipPanel.gameObject.activeInHierarchy) return;
            if (panel.m_ScrollBehaviour != null && panel.m_ScrollBehaviour.CanScroll) return; // the wheel belongs to the recipe list

            float wheel = InputManager.GetAxisScrollWheel(panel);
            if (wheel > 0) SetScroll(scroll - ButtonStep);
            else if (wheel < 0) SetScroll(scroll + ButtonStep);
        }
    }
}
