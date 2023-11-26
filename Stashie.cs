using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ImGuiNET;
using ItemFilterLibrary;
using SharpDX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;
using Vector2N = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace Stashie
{
    public class StashieCore : BaseSettingsPlugin<StashieSettings>
    {
        private const string StashTabsNameChecker = "Stash Tabs Name Checker";
        private const string CoroutineName = "Drop To Stash";
        private readonly Stopwatch _debugTimer = new Stopwatch();
        private Vector2 _clickWindowOffset;
        private List<CustomFilter> _currentFilter;
        private List<FilterResult> _dropItems;
        private List<ListIndexNode> _settingsListNodes;
        private uint _coroutineIteration;
        private Coroutine _coroutineWorker;
        private Action _filterTabs;
        private string[] _stashTabNamesByIndex;
        private Coroutine _stashTabNamesCoroutine;
        private int _visibleStashIndex = -1;
        private const int MaxShownSidebarStashTabs = 31;
        private int _stashCount;

        public StashieCore()
        {
            Name = "Stashie";
        }

        public override void ReceiveEvent(string eventId, object args)
        {
            if (!Settings.Enable.Value)
            {
                return;
            }

            switch (eventId)
            {
                case "Switch_to_Tab":
                    HandleSwitchToTabEvent(args);
                    break;
            }
        }

        private void HandleSwitchToTabEvent(object tab)
        {
            switch (tab)
            {
                case int index:
                    _coroutineWorker = new Coroutine(ProcessSwitchToTab(index), this, CoroutineName);
                    break;
                case string name:
                    if (!_renamedAllStashNames.Contains(name))
                    {
                        DebugWindow.LogMsg($"{Name}: Cannot find Stash Tab: '{name}'.");
                        break;
                    }

                    var tempIndex = _renamedAllStashNames.IndexOf(name);
                    _coroutineWorker = new Coroutine(ProcessSwitchToTab(tempIndex), this, CoroutineName);
                    DebugWindow.LogMsg($"{Name}: Switching to Stash Tab: {tempIndex} ('{name}').");
                    break;
                default:
                    DebugWindow.LogMsg($"{Name}: The received argument is not a string or an integer.");
                    break;
            }

            Core.ParallelRunner.Run(_coroutineWorker);
        }

        public override bool Initialise()
        {
            Settings.Enable.OnValueChanged += (_, b) =>
            {
                if (b)
                {
                    if (Core.ParallelRunner.FindByName(StashTabsNameChecker) == null) InitStashTabNameCoRoutine();
                    _stashTabNamesCoroutine?.Resume();
                }
                else
                {
                    _stashTabNamesCoroutine?.Pause();
                }

                SetupOrClose();
            };

            InitStashTabNameCoRoutine();
            SetupOrClose();

            Input.RegisterKey(Settings.DropHotkey);

            Settings.DropHotkey.OnValueChanged += () => { Input.RegisterKey(Settings.DropHotkey); };
            _stashCount = (int)GameController.Game.IngameState.IngameUi.StashElement.TotalStashes;
            Settings.FilterFile.OnValueSelected = _ => LoadCustomFilters();

            return true;
        }

        public override void AreaChange(AreaInstance area)
        {
            if (_stashTabNamesCoroutine == null) return;
            if (_stashTabNamesCoroutine.Running)
            {
                if (!area.IsHideout && !area.IsTown &&
                    !area.DisplayName.Contains("Azurite Mine") &&
                    !area.DisplayName.Contains("Tane's Laboratory"))
                    _stashTabNamesCoroutine?.Pause();
            }
            else
            {
                if (area.IsHideout ||
                    area.IsTown ||
                    area.DisplayName.Contains("Azurite Mine") ||
                    area.DisplayName.Contains("Tane's Laboratory"))
                    _stashTabNamesCoroutine?.Resume();
            }
        }

        private void InitStashTabNameCoRoutine()
        {
            _stashTabNamesCoroutine = new Coroutine(StashTabNamesUpdater_Thread(), this, StashTabsNameChecker);
            Core.ParallelRunner.Run(_stashTabNamesCoroutine);
        }

        public override void DrawSettings()
        {
            DrawReloadConfigButton();
            DrawIgnoredCellsSettings();
            base.DrawSettings();

            _filterTabs?.Invoke();
        }
        
        private void LoadCustomFilters()
        {
            var configFileDirectory = Path.Combine(ConfigDirectory);

            if (!Directory.Exists(configFileDirectory))
            {
                Directory.CreateDirectory(configFileDirectory);
                return;
            }

            var dirInfo = new DirectoryInfo(configFileDirectory);
            Settings.FilterFile.Values = dirInfo.GetFiles("*.ifl").Select(x => Path.GetFileNameWithoutExtension(x.Name)).ToList();
            if (Settings.FilterFile.Values.Any() && !Settings.FilterFile.Values.Contains(Settings.FilterFile.Value))
            {
                Settings.FilterFile.Value = Settings.FilterFile.Values.First();
            }

            if (!string.IsNullOrWhiteSpace(Settings.FilterFile.Value))
            {
                var filterFilePath = Path.Combine(configFileDirectory, $"{Settings.FilterFile.Value}.ifl");
                if (File.Exists(filterFilePath))
                {
                    _currentFilter = FilterParser.Load($"{Settings.FilterFile.Value}.ifl", filterFilePath);

                    foreach (var customFilter in _currentFilter)
                    {
                        foreach (var filter in customFilter.Filters)
                        {
                            if (!Settings.CustomFilterOptions.TryGetValue(customFilter.ParentMenuName+filter.FilterName, out var indexNodeS))
                            {
                                indexNodeS = new ListIndexNode { Value = "Ignore", Index = -1 };
                                Settings.CustomFilterOptions.Add(customFilter.ParentMenuName + filter.FilterName, indexNodeS);

                            }
                            filter.StashIndexNode = indexNodeS;
                            _settingsListNodes.Add(indexNodeS);
                        }
                    }
                }
                else
                {
                    _currentFilter = null;
                    LogError("Item Filter not found.");
                }
            }
        }

        private void SaveIgnoredSLotsFromInventoryTemplate()
        {
            Settings.IgnoredCells = new[,]
            {
                { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
            };
            try
            {
                var inventoryServer = GameController.IngameState.Data.ServerData.PlayerInventories[0];

                foreach (var item in inventoryServer.Inventory.InventorySlotItems)
                {
                    var baseC = item.Item.GetComponent<Base>();
                    var itemSizeX = baseC.ItemCellsSizeX;
                    var itemSizeY = baseC.ItemCellsSizeY;
                    var inventPosX = item.PosX;
                    var inventPosY = item.PosY;
                    for (var y = 0; y < itemSizeY; y++)
                    for (var x = 0; x < itemSizeX; x++)
                        Settings.IgnoredCells[y + inventPosY, x + inventPosX] = 1;
                }
            }
            catch (Exception e)
            {
                LogError($"{e}", 5);
            }
        }

        private void DrawReloadConfigButton()
        {
            if (ImGui.Button("Reload Config"))
            {
                LoadCustomFilters();
                GenerateMenu();
                DebugWindow.LogMsg($"{Name}: Reloaded Stashie Config", 2, Color.LimeGreen);
            }
        }

        private void DrawIgnoredCellsSettings()
        {
            try
            {
                if (ImGui.Button("Copy Inventory")) SaveIgnoredSLotsFromInventoryTemplate();

                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(
                        $"Checked = Ignored.");
            }
            catch (Exception e)
            {
                DebugWindow.LogError(e.ToString(), 10);
            }

            var number = 1;
            for (var i = 0; i < 5; i++)
            for (var j = 0; j < 12; j++)
            {
                var toggled = Convert.ToBoolean(Settings.IgnoredCells[i, j]);
                if (ImGui.Checkbox($"##{number}IgnoredCells", ref toggled)) Settings.IgnoredCells[i, j] ^= 1;

                if ((number - 1) % 12 < 11) ImGui.SameLine();

                number += 1;
            }
        }

        private void GenerateMenu()
        {
            _stashTabNamesByIndex = _renamedAllStashNames.ToArray();

            _filterTabs = null;
            
            foreach (var parent in _currentFilter)
                _filterTabs += () =>
                {
                    ImGui.TextColored(new Vector4(0f, 1f, 0.022f, 1f), parent.ParentMenuName);
                    ImGui.Separator();
                    foreach (var filter in parent.Filters)
                        if (Settings.CustomFilterOptions.TryGetValue(parent.ParentMenuName+filter.FilterName, out var indexNode))
                        {
                            var formattableString = $"{filter.FilterName} => {_renamedAllStashNames[indexNode.Index + 1]}##{parent.ParentMenuName + filter.FilterName}";

                            ImGui.Columns(2, formattableString, true);
                            ImGui.SetColumnWidth(0, 320);
                            ImGui.SetColumnWidth(1, 300);

                            if (ImGui.InvisibleButton(formattableString, new Vector2N(300, 20)))
                                ImGui.OpenPopup(formattableString);
                            
                            ImGui.SameLine();
                            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 300);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);
                            ImGui.Text(filter.FilterName);

                            ImGui.SameLine();
                            ImGui.NextColumn();
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);

                            var item = indexNode.Index + 1;
                            var filterName = filter.FilterName;

                            if (string.IsNullOrWhiteSpace(filterName))
                                filterName = "Null";

                            if (ImGui.Combo($"##{parent.ParentMenuName + filter.FilterName}", ref item, _stashTabNamesByIndex,
                                    _stashTabNamesByIndex.Length))
                            {
                                indexNode.Value = _stashTabNamesByIndex[item];
                                OnSettingsStashNameChanged(indexNode, _stashTabNamesByIndex[item]);
                            }

                            ImGui.NextColumn();
                            ImGui.Columns(1, "", false);
                            var pop = true;

                            if (!ImGui.BeginPopupModal(formattableString, ref pop,
                                    ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize)) continue;
                            var x = 0;

                            foreach (var name in _renamedAllStashNames)
                            {
                                x++;

                                if (ImGui.Button($"{name}", new Vector2N(100, 20)))
                                {
                                    indexNode.Value = name;
                                    OnSettingsStashNameChanged(indexNode, name);
                                    ImGui.CloseCurrentPopup();
                                }

                                if (x % 10 != 0)
                                    ImGui.SameLine();
                            }

                            ImGui.Spacing();
                            if (ImGui.Button("Close", new Vector2N(100, 20)))
                                ImGui.CloseCurrentPopup();

                            ImGui.EndPopup();
                        }
                        else
                        {
                            indexNode = new ListIndexNode { Value = "Ignore", Index = -1 };
                        }
                };
        }

        public override Job Tick()
        {
            if (!StashingRequirementsMet() && Core.ParallelRunner.FindByName("Stashie_DropItemsToStash") != null)
            {
                StopCoroutine("Stashie_DropItemsToStash");
                return null;
            }

            if (Settings.DropHotkey.PressedOnce())
            {
                if (Core.ParallelRunner.FindByName("Stashie_DropItemsToStash") == null)
                {
                    StartDropItemsToStashCoroutine();
                }
                else
                {
                    StopCoroutine("Stashie_DropItemsToStash");
                }
            }

            return null;
        }

        private void StartDropItemsToStashCoroutine()
        {
            _debugTimer.Reset();
            _debugTimer.Start();
            Core.ParallelRunner.Run(new Coroutine(DropToStashRoutine(), this, "Stashie_DropItemsToStash"));
        }

        private void StopCoroutine(string routineName)
        {
            var routine = Core.ParallelRunner.FindByName(routineName);
            routine?.Done();
            _debugTimer.Stop();
            _debugTimer.Reset();
            CleanUp();
        }

        private IEnumerator DropToStashRoutine()
        {
            var originCursorPosition = Mouse.GetCursorPosition();
            var originTab = GetIndexOfCurrentVisibleTab();

            yield return ParseItems();
            for (int tries = 0; tries < 3 && _dropItems.Count > 0; ++tries)
            {
                if (_dropItems.Count > 0) yield return StashItemsIncrementer();
                yield return ParseItems();
                yield return new WaitTime(Settings.ExtraDelay +  GameController.IngameState.ServerData.Latency + Random.Shared.Next(25, 50));
            }

            if (Settings.VisitTabWhenDone.Value)
            {
                if (Settings.BackToOriginalTab.Value)
                {
                    yield return SwitchToTab(originTab);
                }
                else
                {
                    yield return SwitchToTab(Settings.TabToVisitWhenDone.Value);
                }
            }

            Mouse.MoveMouse(originCursorPosition, Settings.MaximumInterpolationDistance,
                Settings.MinimumInterpolationDelay, Settings.MaximumInterpolationDelay);

            StopCoroutine("Stashie_DropItemsToStash");
        }

        private void CleanUp()
        {
            Input.KeyUp(Keys.LControlKey);
            Input.KeyUp(Keys.Shift);
        }

        private bool StashingRequirementsMet()
        {
            return GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible &&
                   GameController.Game.IngameState.IngameUi.StashElement.IsVisibleLocal;
        }

        private IEnumerator ProcessSwitchToTab(int index)
        {
            _debugTimer.Restart();
            yield return SwitchToTab(index);
            _coroutineWorker = Core.ParallelRunner.FindByName(CoroutineName);
            _coroutineWorker?.Done();

            _debugTimer.Restart();
            _debugTimer.Stop();
        }

        private IEnumerator ParseItems()
        {
            var inventory = GameController.Game.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
            var invItems = inventory.InventorySlotItems;

            yield return new WaitFunctionTimed(() => invItems != null, true, 500,
                "ServerInventory->InventSlotItems is null!");

            var sortedInvItems = invItems.OrderBy(item => item.PosX).ThenBy(item => item.PosY).ToList();

            _dropItems = new List<FilterResult>();
            _clickWindowOffset = GameController.Window.GetWindowRectangle().TopLeft;
            foreach (var invItem in sortedInvItems)
            {
                if (invItem.Item == null || invItem.Address == 0) continue;
                if (CheckIgnoreCells(invItem)) continue;

                ItemData testItem = new ItemData(invItem.Item, GameController);
                var result = CheckFilters(testItem, CalculateClickPosition(invItem));
                if (result != null)
                    _dropItems.Add(result);
            }
        }

        private Vector2 CalculateClickPosition(InventSlotItem invItem)
        {
            var inventoryPanelRectF = GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory]
                .GetClientRect();
            var cellWidth = inventoryPanelRectF.Width / 12;
            var cellHeight = inventoryPanelRectF.Height / 5;

            Vector2 baseClickPosition = new Vector2(
                inventoryPanelRectF.Location.X + (cellWidth / 2) + (invItem.PosX * cellWidth),
                inventoryPanelRectF.Location.Y + (cellHeight / 2) + (invItem.PosY * cellHeight)
            );

            float randomXOffset = Random.Shared.Next(-10, 11);
            float randomYOffset = Random.Shared.Next(-10, 11);

            Vector2 randomizedClickPosition = new Vector2(
                baseClickPosition.X + randomXOffset,
                baseClickPosition.Y + randomYOffset
            );

            return randomizedClickPosition;
        }

        private bool CheckIgnoreCells(InventSlotItem inventItem)
        {
            var inventPosX = inventItem.PosX;
            var inventPosY = inventItem.PosY;

            if (inventPosX < 0 || inventPosX >= 12) return true;

            if (inventPosY < 0 || inventPosY >= 5) return true;

            return Settings.IgnoredCells[inventPosY, inventPosX] != 0;
        }

        private FilterResult CheckFilters(ItemData itemData, Vector2 clickPosition)
        {
            foreach (var filter in _currentFilter)
            {
                foreach ( var subFilter in filter.Filters)
                {
                    try
                    {
                        if (!subFilter.AllowProcess) continue;

                        if (filter.CompareItem(itemData, subFilter.CompiledQuery)) 
                            return new FilterResult(subFilter, itemData, clickPosition);
                    }
                    catch (Exception e)
                    {
                        DebugWindow.LogError($"Filters Error: {e}");
                    }
                }
            }
            return null;
        }

        private IEnumerator StashItemsIncrementer()
        {
            _coroutineIteration++;

            yield return StashItems();
        }

        private IEnumerator StashItems()
        {
            PublishEvent("Stashie_Start_Drop_Items", null);

            _visibleStashIndex = GetIndexOfCurrentVisibleTab();
            if (_visibleStashIndex < 0)
            {
                LogMessage($"{Name}: Invalid VisibleStashIndex: {_visibleStashIndex}.");
                yield break;
            }

            var itemsSortedByStash = _dropItems
                .OrderBy(x => x.SkipSwitchTab || x.StashIndex == _visibleStashIndex ? 0 : 1).ThenBy(x => x.StashIndex)
                .ToList();

            Keyboard.KeyDown(Keys.LControlKey);
            LogMessage($"{Name}: Stashing {itemsSortedByStash.Count} Items.");
            foreach (var filterResult in itemsSortedByStash)
            {
                _coroutineIteration++;
                _coroutineWorker?.UpdateTicks(_coroutineIteration);

                if (!filterResult.SkipSwitchTab)
                    yield return SwitchToTab(filterResult.StashIndex);

                yield return new WaitFunctionTimed(
                    () => GameController.IngameState.IngameUi.StashElement.AllInventories[_visibleStashIndex] != null,
                    true, 2000, $"Stash Tab Error. Index: {_visibleStashIndex}");
                yield return new WaitFunctionTimed(
                    () => GetTypeOfCurrentVisibleStash() != InventoryType.InvalidInventory,
                    true, 2000, $"Invalid Inventory Type. Index: {_visibleStashIndex}");

                yield return StashItem(filterResult);

                _debugTimer.Restart();
                PublishEvent("Stashie_Finish_Drop_Items_to_Stash_Tab", null);
            }
        }

        private IEnumerator StashItem(FilterResult filterResult)
        {
            Mouse.MoveMouse(filterResult.ClickPosition + _clickWindowOffset, Settings.MaximumInterpolationDistance,
                Settings.MinimumInterpolationDelay, Settings.MaximumInterpolationDelay);

            yield return new WaitTime(Delay.GetDelay(Settings.MinimumDelay, Settings.MaximumDelay, Settings.DelayMean, Settings.DelayStandardDeviation));

            bool isShiftUsed = false;
            if (filterResult.ShiftForStashing)
            {
                Keyboard.KeyDown(Keys.ShiftKey);
                isShiftUsed = true;
            }

            Input.Click(MouseButtons.Left);
            if (isShiftUsed)
            {
                Keyboard.KeyUp(Keys.ShiftKey);
            }

            yield return new WaitTime(Delay.GetDelay(Settings.MinimumDelay, Settings.MaximumDelay, Settings.DelayMean, Settings.DelayStandardDeviation));
        }

        #region Switching Stash Tabs

        private IEnumerator SwitchToTab(int tabIndex)
        {
            _visibleStashIndex = GetIndexOfCurrentVisibleTab();
            var travelDistance = Math.Abs(tabIndex - _visibleStashIndex);
            if (travelDistance == 0) yield break;

            if (Settings.AlwaysUseArrow.Value || travelDistance < 2 || !SliderPresent())
                yield return SwitchToTabViaArrowKeys(tabIndex);
            else
                yield return SwitchToTabViaDropdownMenu(tabIndex);

            yield return new WaitTime(Delay.GetDelay(Settings.MinimumDelay, Settings.MaximumDelay, Settings.DelayMean, Settings.DelayStandardDeviation));
        }

        private IEnumerator SwitchToTabViaArrowKeys(int tabIndex, int numberOfTries = 1)
        {
            if (numberOfTries >= 3)
            {
                yield break;
            }

            var indexOfCurrentVisibleTab = GetIndexOfCurrentVisibleTab();
            var travelDistance = tabIndex - indexOfCurrentVisibleTab;
            var tabIsToTheLeft = travelDistance < 0;
            travelDistance = Math.Abs(travelDistance);

            if (tabIsToTheLeft)
            {
                yield return PressKey(Keys.Left, travelDistance);
            }
            else
            {
                yield return PressKey(Keys.Right, travelDistance);
            }

            if (GetIndexOfCurrentVisibleTab() != tabIndex)
            {
                yield return new WaitTime(Delay.GetDelay(Settings.MinimumDelay, Settings.MaximumDelay, Settings.DelayMean, Settings.DelayStandardDeviation));
                yield return SwitchToTabViaArrowKeys(tabIndex, numberOfTries + 1);
            }
        }

        private IEnumerator PressKey(Keys key, int repetitions = 1)
        {
            for (var i = 0; i < repetitions; i++)
            {
                yield return Input.KeyPress(key);
            }
        }

        private bool DropDownMenuIsVisible()
        {
            return GameController.Game.IngameState.IngameUi.StashElement.ViewAllStashPanel.IsVisible;
        }

        private IEnumerator OpenDropDownMenu()
        {
            var button = GameController.Game.IngameState.IngameUi.StashElement.ViewAllStashButton.GetClientRect();
            yield return ClickElement(button.Center);
            while (!DropDownMenuIsVisible())
            {
                yield return new WaitTime(Delay.GetDelay(Settings.MinimumDelay, Settings.MaximumDelay, Settings.DelayMean, Settings.DelayStandardDeviation));
            }
        }

        private static bool StashLabelIsClickable(int index)
        {
            return index + 1 < MaxShownSidebarStashTabs;
        }

        private bool SliderPresent()
        {
            return _stashCount > MaxShownSidebarStashTabs;
        }

        private IEnumerator ClickDropDownMenuStashTabLabel(int tabIndex)
        {
            var dropdownMenu = GameController.Game.IngameState.IngameUi.StashElement.ViewAllStashPanel;
            var stashTabLabels = dropdownMenu.GetChildAtIndex(1);

            var clickable = StashLabelIsClickable(tabIndex);

            var index = clickable ? tabIndex : tabIndex - (_stashCount - 1 - (MaxShownSidebarStashTabs - 1));
            var position = stashTabLabels.GetChildAtIndex(index).GetClientRect().Center;
            MoveMouseToElement(position);
            if (SliderPresent())
            {
                var clicks = _stashCount - MaxShownSidebarStashTabs;
                yield return new WaitTime(Delay.GetDelay(Settings.MinimumDelay, Settings.MaximumDelay, Settings.DelayMean, Settings.DelayStandardDeviation));
                VerticalScroll(scrollUp: clickable, clicks: clicks);
                yield return new WaitTime(Delay.GetDelay(Settings.MinimumDelay, Settings.MaximumDelay, Settings.DelayMean, Settings.DelayStandardDeviation));
            }

            DebugWindow.LogMsg($"{Name}: Moving to Stash Tab '{tabIndex}'.", 3, Color.LightGray);
            yield return Click();
        }

        private IEnumerator ClickElement(Vector2 targetPosition, MouseButtons mouseButton = MouseButtons.Left)
        {
            MoveMouseToElement(targetPosition);
            yield return Click(mouseButton);
        }

        private IEnumerator Click(MouseButtons mouseButton = MouseButtons.Left)
        {
            Input.Click(mouseButton);
            yield return new WaitTime(Delay.GetDelay(Settings.MinimumDelay, Settings.MaximumDelay, Settings.DelayMean, Settings.DelayStandardDeviation));
        }

        private void MoveMouseToElement(Vector2 targetPosition)
        {
            Mouse.MoveMouse(targetPosition + GameController.Window.GetWindowRectangle().TopLeft);
        }

        private IEnumerator SwitchToTabViaDropdownMenu(int tabIndex)
        {
            if (!DropDownMenuIsVisible())
            {
                yield return OpenDropDownMenu();
            }

            yield return ClickDropDownMenuStashTabLabel(tabIndex);
        }

        private int GetIndexOfCurrentVisibleTab()
        {
            return GameController.Game.IngameState.IngameUi.StashElement.IndexVisibleStash;
        }

        private InventoryType GetTypeOfCurrentVisibleStash()
        {
            var stashPanelVisibleStash = GameController.Game.IngameState.IngameUi?.StashElement?.VisibleStash;
            return stashPanelVisibleStash?.InvType ?? InventoryType.InvalidInventory;
        }

        #endregion

        #region Stash Update

        private void OnSettingsStashNameChanged(ListIndexNode node, string newValue)
        {
            node.Index = GetInventIndexByStashName(newValue);
        }

        public override void OnClose()
        {
        }

        private void SetupOrClose()
        {
            _settingsListNodes = new List<ListIndexNode>(100);
            
            LoadCustomFilters();

            try
            {
                Settings.TabToVisitWhenDone.Max =
                    (int)GameController.Game.IngameState.IngameUi.StashElement.TotalStashes - 1;
                var names = GameController.Game.IngameState.IngameUi.StashElement.AllStashNames;
                UpdateStashNames(names);
            }
            catch (Exception e)
            {
                LogError($"{Name}: Failed to get Stash Names: {e}");
            }
        }

        private int GetInventIndexByStashName(string name)
        {
            var index = _renamedAllStashNames.IndexOf(name);
            if (index != -1) index--;

            return index;
        }

        private List<string> _renamedAllStashNames;

        private void UpdateStashNames(ICollection<string> newNames)
        {
            Settings.AllStashNames = newNames.ToList();

            if (newNames.Count < 4)
            {
                LogError($"{Name}: Cannot parse Stash Tab names.");
                return;
            }

            _renamedAllStashNames = new List<string> { "Ignore" };
            var settingsAllStashNames = Settings.AllStashNames;

            for (var i = 0; i < settingsAllStashNames.Count; i++)
            {
                var realStashName = settingsAllStashNames[i];

                if (_renamedAllStashNames.Contains(realStashName))
                {
                    realStashName += " (" + i + ")";
                }

                _renamedAllStashNames.Add(realStashName ?? "%NULL%");
            }

            Settings.AllStashNames.Insert(0, "Ignore");

            foreach (var lOption in _settingsListNodes)
                try
                {
                    lOption.SetListValues(_renamedAllStashNames);
                    var inventoryIndex = GetInventIndexByStashName(lOption.Value);

                    if (inventoryIndex == -1)
                    {
                        if (lOption.Index != -1)
                        {
                            if (lOption.Index + 1 >= _renamedAllStashNames.Count)
                            {
                                lOption.Index = -1;
                                lOption.Value = _renamedAllStashNames[0];
                            }
                            else
                            {
                                lOption.Value = _renamedAllStashNames[lOption.Index + 1];
                            }
                        }
                        else
                        {
                            lOption.Value =
                                _renamedAllStashNames[0];
                        }
                    }
                    else
                    {
                        lOption.Index = inventoryIndex;
                        lOption.Value = _renamedAllStashNames[inventoryIndex + 1];
                    }
                }
                catch (Exception e)
                {
                    DebugWindow.LogError($"{Name}: UpdateStashNames _settingsListNodes: {e}");
                }

            GenerateMenu();
        }

        private static readonly WaitTime Wait2Sec = new WaitTime(2000);
        private static readonly WaitTime Wait1Sec = new WaitTime(1000);
        private uint _counterStashTabNamesCoroutine;

        private IEnumerator StashTabNamesUpdater_Thread()
        {
            while (true)
            {
                while (!GameController.Game.IngameState.InGame) yield return Wait2Sec;

                var stashPanel = GameController.Game.IngameState?.IngameUi?.StashElement;

                while (stashPanel == null || !stashPanel.IsVisibleLocal) yield return Wait1Sec;

                _counterStashTabNamesCoroutine++;
                _stashTabNamesCoroutine?.UpdateTicks(_counterStashTabNamesCoroutine);
                var cachedNames = Settings.AllStashNames;
                var realNames = stashPanel.AllStashNames;

                if (realNames.Count + 1 != cachedNames.Count)
                {
                    UpdateStashNames(realNames);
                    continue;
                }

                for (var index = 0; index < realNames.Count; ++index)
                {
                    var cachedName = cachedNames[index + 1];
                    if (cachedName.Equals(realNames[index])) continue;

                    UpdateStashNames(realNames);
                    break;
                }

                yield return Wait1Sec;
            }
        }

        private static void VerticalScroll(bool scrollUp, int clicks)
        {
            const int wheelDelta = 120;
            if (scrollUp)
                WinApi.mouse_event(Input.MOUSE_EVENT_WHEEL, 0, 0, clicks * wheelDelta, 0);
            else
                WinApi.mouse_event(Input.MOUSE_EVENT_WHEEL, 0, 0, -(clicks * wheelDelta), 0);
        }

        #endregion
    }
}