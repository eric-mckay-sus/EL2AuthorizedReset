// <copyright file="TargetedResets.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace AdminInterface.Components.Pages.HomePage;

using Microsoft.AspNetCore.Components;

public partial class TargetedResets
{
    [Parameter] public LockoutReset? target { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    private Filter<bool?> FilterDenialType => GetFilter<bool?>("denialType");
    /// <summary>
    /// When this component loads, create a filter
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        CurrentSortColumn = "IsAuthorized";
        SortDir = "descending";
    }

    protected override void InitializeFilters()
    {
        Filters["denialType"] =new Filter<bool?>("denialType", null);
    }

    /// <summary>
    /// Ensures that only resets targeting the selected lockout are shown
    /// </summary>
    protected override IQueryable<Reset> ApplyFilters(IQueryable<Reset> query)
    {
        if (target == null) return query.Where(x => false); // Return empty if no associate selected

        // Filter the view by the selected lockout first to limit results
        query = query.Where(x => x.LockoutId == target.LockoutId);

        // Apply the denial type filter if applicable
        if(FilterDenialType.IsActive && FilterDenialType.Value.HasValue){
            query = query.Where(x => x.IsAuthorized == false); // when filtering by denial type, ignore authorized row(s)

            if (FilterDenialType.Value == true){ // true represents insufficent auth level
                query = query.Where(x => x.AuthLevel < target.LockoutLevel);
            }
            else{ // false represents not authorized for the line at all
                query = query.Where(x => x.AuthLevel == null);
            }
        }

        return query;
    }

    /// <summary>
    /// Helper method to update filter and view on dropdown change
    /// </summary>
    /// <param name="e">The change event thrown by the dropdown</param>
    /// <returns></returns>
    private async Task ChangeDenialType(ChangeEventArgs e){
        string val = e.Value?.ToString() ?? string.Empty;
        FilterDenialType.Value = string.IsNullOrWhiteSpace(val) ? null : bool.Parse(val);
        await LoadData();
    }
}
