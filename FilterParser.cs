using ExileCore;
using System;
using System.Collections.Generic;
using System.IO;
using ItemFilterLibrary;
using Newtonsoft.Json;

namespace Stashie
{
    public class FilterParser
    {
        public partial class IFL
        {
            public ParentMenu[] ParentMenu { get; set; }
        }

        public partial class ParentMenu
        {
            public string MenuName { get; set; }

            public List<Filter> Filters { get; set; }
        }

        public partial class Filter
        {
            public string FilterName { get; set; }

            public string[] RawQuery { get; set; }

            public bool? Shifting { get; set; }

            public bool? Affinity { get; set; }
        }


        public static List<CustomFilter> Load(string fileName, string filePath)
        {
            List<CustomFilter> allFilters = new List<CustomFilter>();

            try
            {
                string fileContents = File.ReadAllText(filePath);

                var newFilters = JsonConvert.DeserializeObject<IFL>(fileContents);

                var newFilter = 0;
                for (int i = 0; i < newFilters.ParentMenu.Length; i++)
                {
                    var newParent = new CustomFilter
                    {
                        ParentMenuName = newFilters.ParentMenu[i].MenuName,
                    };

                    for (int j = 0; j < newFilters.ParentMenu[i].Filters.Count; j++)
                    {

                        var compiledQuery = ItemQuery.Load(string.Join("", newFilters.ParentMenu[i].Filters[j].RawQuery).Replace("\n", ""));
                        
                        var filterErrorParse = compiledQuery.FailedToCompile;
                        
                        if (filterErrorParse)
                        {
                            DebugWindow.LogError($"Stashie: JSON Error. Parent: {newFilters.ParentMenu[i].MenuName}, Filter: {newFilters.ParentMenu[i].Filters[j].FilterName}", 15);
                        }
                        else
                        {
                            newParent.Filters.Add(new CustomFilter.Filter
                            {
                                FilterName = newFilters.ParentMenu[i].Filters[j].FilterName,
                                RawQuery = string.Join(" ", newFilters.ParentMenu[i].Filters[j].RawQuery),
                                Shifting = newFilters.ParentMenu[i].Filters[j].Shifting ?? false,
                                Affinity = newFilters.ParentMenu[i].Filters[j].Affinity ?? false,
                                CompiledQuery = compiledQuery,
                            });
                        }
                        newFilter ++;
                    }
                    if (newParent.Filters.Count > 0)
                        allFilters.Add(newParent);
                }
            }
            catch (Exception ex)
            {
                DebugWindow.LogError($"Stashie: Failed Loading Filter: {fileName}. {filePath}.\nException: {ex.Message}", 15);
            }
            
            return allFilters;
        }
    }
}