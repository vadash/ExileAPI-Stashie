using ItemFilterLibrary;
using SharpDX;
using Vector2N = System.Numerics.Vector2;

namespace Stashie
{
    public class FilterResult
    {
        public FilterResult(CustomFilter.Filter filter, ItemData itemData, Vector2 clickPosition)
        {
            Filter = filter;
            ItemData = itemData;
            StashIndex = filter.StashIndexNode.Index;
            ClickPosition = clickPosition;
            SkipSwitchTab = filter.Affinity ?? false;
            ShiftForStashing = filter.Shifting ?? false;
        }

        public CustomFilter.Filter Filter { get; }
        public ItemData ItemData { get; }
        public int StashIndex { get; }
        public Vector2 ClickPosition { get; }
        public bool SkipSwitchTab { get; }
        public bool ShiftForStashing { get; }
    }
}