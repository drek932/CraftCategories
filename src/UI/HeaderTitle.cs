using Il2Cpp;

namespace CraftCategories
{
    /// <summary>
    /// Header above the recipe list: while a mod category is selected it shows the mod name,
    /// and restores the original text when a game category is selected again.
    /// </summary>
    internal sealed class HeaderTitle
    {
        private readonly UILabel label;
        private string originalText;

        public HeaderTitle(UILabel label) => this.label = label;

        public void Update(string activeMod)
        {
            if (label == null) return;

            if (activeMod != null)
            {
                var title = ModCatalog.DisplayName(activeMod);
                if (label.text == title) return;
                originalText ??= label.text;
                label.text = title;
            }
            else if (originalText != null)
            {
                label.text = originalText;
                originalText = null;
            }
        }
    }
}
