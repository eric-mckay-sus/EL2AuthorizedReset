// <copyright file="ViewLines.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace AdminInterface.Components.Pages;

/// <summary>
/// Code-behind for the simple line status page.
/// </summary>
public partial class ViewLines
{
    private bool isCollapsed = false;

    private Filter<int?> FilterCmmsNum => this.GetFilter<int?>("cmmsNum");

    private Filter<string?> FilterLineName => this.GetFilter<string?>("lineName");

    private Filter<bool?> FilterStatus => this.GetFilter<bool?>("status");

    /// <summary>
    /// When the page loads, set the default sort and populate the filters.
    /// </summary>
    protected override void OnInitialized()
    {
        this.CurrentSortColumn = "IsActive";
        this.SortDir = "ascending";
        base.OnInitialized();
    }

    /// <summary>
    /// Populate the search-by-machine filters.
    /// </summary>
    protected override void InitializeFilters()
    {
        this.Filters["cmmsNum"] = new Filter<int?>("cmmsNum", null);
        this.Filters["lineName"] = new Filter<string?>("lineName", null);
        this.Filters["status"] = new Filter<bool?>("status", null);
    }

    /// <summary>
    /// Apply all filters with input in the filter panel.
    /// </summary>
    /// <param name="query">The query to which the filters should be applied.</param>
    /// <returns>The filtered query.</returns>
    protected override IQueryable<CmmsLine> ApplyFilters(IQueryable<CmmsLine> query)
    {
        // CMMS Number (Exact)
        if (this.FilterCmmsNum.IsActive)
        {
            query = query.Where(x => x.CmmsNum == this.FilterCmmsNum.Value);
        }

        // Status (Exact match from dropdown)
        if (this.FilterStatus.IsActive)
        {
            query = query.Where(x => x.IsActive == this.FilterStatus.Value);
        }

        // Line Name (Partial match)
        if (this.FilterLineName.IsActive)
        {
            query = query.Where(x => x.LineName != null && x.LineName.Contains(this.FilterLineName.Value!));
        }

        return query;
    }

    private static bool? ParseBool(string? value)
    {
        if (bool.TryParse(value, out var result))
        {
            return result;
        }

        return null;
    }

    private async Task ExecuteAndCollapse()
    {
        await this.LoadData(updateCounts: true);
        this.ToggleCollapse();
    }

    private void ToggleCollapse() => this.isCollapsed = !this.isCollapsed;
}
