using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Stashie
{
    public class StashieSettings : ISettings
    {
        public List<string> AllStashNames = new();
        public readonly Dictionary<string, ListIndexNode> CustomFilterOptions = new();
        
        [Menu("Filter File")]
        public ListNode FilterFile { get; set; } = new ListNode();

        [Menu("Stash Hotkey")]
        public HotkeyNode DropHotkey { get; set; } = Keys.F3;
        
        [Menu("Minimum Interpolation Delay", "Minimum Delay in Milliseconds")]
        public RangeNode<int> MinimumInterpolationDelay { get; set; } = new(0, 0, 1000);
    
        [Menu("Maximum Interpolation Delay", "Maximum Delay in Milliseconds")]
        public RangeNode<int> MaximumInterpolationDelay { get; set; } = new(200, 0, 1000);
        
        [Menu("Maximum Interpolation Distance")]
        public RangeNode<int> MaximumInterpolationDistance { get; set; } = new(600, 0, 2560);
        
        [Menu("Extra Delay")]
        public RangeNode<int> ExtraDelay { get; set; } = new(0, 0, 2000);
        
        [Menu("HoverItem Delay")]
        public RangeNode<int> HoverItemDelay { get; set; } = new(19, 0, 2000);
        
        [Menu("StashItem Delay")]
        public RangeNode<int> StashItemDelay { get; set; } = new(19, 0, 2000);

        [Menu("Go to Stash Tab on Completion")]
        public ToggleNode VisitTabWhenDone { get; set; } = new ToggleNode(false);

        [Menu("Tab (Index)")]
        public RangeNode<int> TabToVisitWhenDone { get; set; } = new(0, 0, 40);

        [Menu("Go to Initial Tab on Completion")]
        public ToggleNode BackToOriginalTab { get; set; } = new ToggleNode(true);

        [Menu("Force Arrow Key Switching")]
        public ToggleNode AlwaysUseArrow { get; set; } = new ToggleNode(true);

        public ToggleNode Enable { get; set; } = new ToggleNode(false);

        public int[,] IgnoredCells { get; set; } =
        {
            {1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            {1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}
        };

    }
}