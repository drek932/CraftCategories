using Il2Cpp;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CraftCategories
{
    /// <summary>
    /// Hint to the right of a mod category button — just the mod name, no background.
    /// Font and style are taken from the recipe list header so the text looks like the game's.
    /// </summary>
    internal sealed class CategoryTooltip
    {
        private const float OffsetX = 40f; // to the right of the button center
        private const int DepthAboveList = 50;

        private readonly GameObject root;
        private readonly UILabel label;

        public CategoryTooltip(Panel_Crafting panel, UILabel fontSource)
        {
            var nav = panel.m_CategoryNavigation.transform;

            root = new GameObject("CC_Tooltip") { layer = nav.gameObject.layer };
            root.transform.SetParent(nav, false);

            // Own panel above the rest of the window, otherwise the recipe list would cover the hint.
            var uiPanel = root.AddComponent<UIPanel>();
            var parentPanel = panel.GetComponent<UIPanel>();
            uiPanel.depth = (parentPanel != null ? parentPanel.depth : 0) + DepthAboveList;

            var labelGo = Object.Instantiate(fontSource.gameObject, root.transform);
            labelGo.name = "Label";
            var localize = labelGo.GetComponent<UILocalize>();
            if (localize != null) Object.DestroyImmediate(localize); // otherwise the game restores the header text

            label = labelGo.GetComponent<UILabel>();
            // The header's anchors would pull the copy back to the header's place.
            label.leftAnchor.target = null;
            label.rightAnchor.target = null;
            label.bottomAnchor.target = null;
            label.topAnchor.target = null;
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            label.pivot = UIWidget.Pivot.Left;
            label.transform.localPosition = Vector3.zero;

            root.SetActive(false);
        }

        public void Show(string text, Transform button)
        {
            label.text = text;
            root.transform.position = button.position;
            root.transform.localPosition += new Vector3(OffsetX, 0, 0);
            root.SetActive(true);
        }

        public void Hide() => root.SetActive(false);
    }
}
