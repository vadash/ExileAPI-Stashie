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
        public RangeNode<int> MaximumInterpolationDelay { get; set; } = new(1000, 0, 1000);
        
        [Menu("Maximum Interpolation Distance")]
        public RangeNode<int> MaximumInterpolationDistance { get; set; } = new(2560, 0, 2560);
        
        [Menu("Delay Mean", "Mean of the Gaussian Distribution in Milliseconds")]
        public RangeNode<int> DelayMean { get; set; } = new(100, 0, 1000);
    
        [Menu("Delay Standard Deviation", "Standard Deviation of the Gaussian Distribution in Milliseconds")]
        public RangeNode<int> DelayStandardDeviation { get; set; } = new(50, 0, 1000);
    
        [Menu("Minimum Delay", "Minimum Delay in Milliseconds")]
        public RangeNode<int> MinimumDelay { get; set; } = new(50, 0, 1000);
    
        [Menu("Maximum Delay", "Maximum Delay in Milliseconds")]
        public RangeNode<int> MaximumDelay { get; set; } = new(150, 0, 1000);

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