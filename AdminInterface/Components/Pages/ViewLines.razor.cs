// <copyright file="ViewLines.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace AdminInterface.Components.Pages;

public partial class ViewLines
{
    private Filter<int?> FilterCmmsNum => GetFilter<int?>("cmmsNum");
    private Filter<string?> FilterLineName => GetFilter<string?>("lineName");
    private Filter<bool?> FilterStatus => GetFilter<bool?>("status");

    private bool _isCollapsed = false;
    private void ToggleCollapse() => _isCollapsed = !_isCollapsed;

    /// <summary>
    /// When the page loads, set the default sort and populate the filters
    /// </summary>
    protected override void OnInitialized()
    {
        CurrentSortColumn="IsActive";
        SortDir = "ascending";
        base.OnInitialized();
    }

    private async Task ExecuteAndCollapse(){
        await LoadData(updateCounts:true);
        ToggleCollapse();
    }

    /// <summary>
    /// Populate the search-by-machine filters
    /// </summary>
    protected override void InitializeFilters()
    {
        Filters["cmmsNum"] = new Filter<int?>("cmmsNum", null);
        Filters["lineName"] = new Filter<string?>("lineName", null);
        Filters["status"] = new Filter<bool?>("status", null);
    }

    /// <summary>
    /// Apply all filters with input in the filter panel
    /// </summary>
    /// <param name="query">The query to which the filters should be applied</param>
    /// <returns>The filtered query</returns>
    protected override IQueryable<CmmsLine> ApplyFilters(IQueryable<CmmsLine> query)
    {
        // CMMS Number (Exact)
        if (FilterCmmsNum.IsActive)
            query = query.Where(x => x.CmmsNum == FilterCmmsNum.Value);

        // Status (Exact match from dropdown)
        if (FilterStatus.IsActive)
            query = query.Where(x => x.IsActive == FilterStatus.Value);

        // Line Name (Partial match)
        if (FilterLineName.IsActive)
            query = query.Where(x => x.LineName != null && x.LineName.Contains(FilterLineName.Value!));

        return query;
    }

    private bool? ParseBool(string? value)
    {
        if (bool.TryParse(value, out var result)) return result;
        return null;
    }
}
