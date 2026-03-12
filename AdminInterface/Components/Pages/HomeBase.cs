using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;

namespace AdminInterface.Components.Pages;

/// <summary>
/// Code-behind for Home.razor
/// Implements a filter registry system similar to LogTableLogic for the Reset table
/// </summary>
public class HomeBase : EntityManagerBase<Reset, Reset>
{
    // Filter registry to hold all active filters
    protected Dictionary<string, IFilter> Filters { get; set; } = [];
    private bool _filtersInitialized = false; // Whether the filters have been initialized yet

    // Filter properties that expose the filters in the registry with type safety
    protected Filter<int?> FilterAssocNum => GetFilter<int?>("assocNum");
    protected Filter<string?> FilterAssocName => GetFilter<string?>("assocName");
    protected Filter<int?> FilterCmmsNum => GetFilter<int?>("cmmsNum");
    protected Filter<string?> FilterLineName => GetFilter<string?>("lineName");
    protected Filter<bool?> FilterIsAuthorized => GetFilter<bool?>("isAuthorized");
    protected Filter<DateTime?> FilterStartDate => GetFilter<DateTime?>("after");
    protected Filter<DateTime?> FilterEndDate => GetFilter<DateTime?>("before");

    /// <summary>
    /// When the page loads, initialize filters before loading data
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        InitializeFilters();
        SortDir = "descending";
        CurrentSortColumn = "Timestamp";
        await base.OnInitializedAsync();
    }

    /// <summary>
    /// Initializes the filter registry with default values
    /// </summary>
    protected void InitializeFilters()
    {
        if (_filtersInitialized) return;

        Filters["assocNum"] = new Filter<int?>("assocNum", null);
        Filters["assocName"] = new Filter<string?>("assocName", null);
        Filters["cmmsNum"] = new Filter<int?>("cmmsNum", null);
        Filters["lineName"] = new Filter<string?>("lineName", null);
        Filters["isAuthorized"] = new Filter<bool?>("isAuthorized", null);
        Filters["after"] = new Filter<DateTime?>("after", null);
        Filters["before"] = new Filter<DateTime?>("before", null);

        _filtersInitialized = true;
    }

    /// <summary>
    /// Helper method to get a strongly-typed filter from the registry
    /// </summary>
    /// <typeparam name="T">The type of the filter value (int, string, DateTime, or bool)</typeparam>
    /// <param name="key">The key of the filter to retrieve</param>
    /// <returns>The filter with appropriate type</returns>
    protected Filter<T> GetFilter<T>(string key)
    {
        if (Filters.TryGetValue(key, out var filter) && filter is Filter<T> typedFilter)
        {
            return typedFilter;
        }

        // If filter doesn't exist or has wrong type, create a new one
        var newFilter = new Filter<T>(key, default);
        Filters[key] = newFilter;
        return newFilter;
    }

    /// <summary>
    /// Overrides EntityManagerBase's ApplyFilter to apply all active filters from the registry
    /// </summary>
    protected override IQueryable<Reset> ApplyFilter(IQueryable<Reset> query)
    {
        foreach (var filter in Filters.Values){
            Console.WriteLine(filter);
        }
        var filterAssocNum = GetFilter<int?>("assocNum");
        var filterAssocName = GetFilter<string?>("assocName");
        var filterCmmsNum = GetFilter<int?>("cmmsNum");
        var filterLineName = GetFilter<string?>("lineName");
        var filterIsAuthorized = GetFilter<bool?>("isAuthorized");
        var filterStartDate = GetFilter<DateTime?>("after");
        var filterEndDate = GetFilter<DateTime?>("before");

        // Apply Associate Number filter
        if (filterAssocNum.IsActive)
        {
            query = filterAssocNum.IsNegated
                ? query.Where(r => r.AssocNum != filterAssocNum.Value)
                : query.Where(r => r.AssocNum == filterAssocNum.Value);
        }

        // Apply Associate Name filter (contains - case insensitive)
        if (filterAssocName.IsActive && !string.IsNullOrWhiteSpace(filterAssocName.Value))
        {
            var searchName = filterAssocName.Value.ToLower();
            query = filterAssocName.IsNegated
                ? query.Where(r => !r.AssocName.Contains(searchName))
                : query.Where(r => r.AssocName.Contains(searchName));
        }

        // Apply CMMS Number filter
        if (filterCmmsNum.IsActive)
        {
            query = filterCmmsNum.IsNegated
                ? query.Where(r => r.CmmsNum != filterCmmsNum.Value)
                : query.Where(r => r.CmmsNum == filterCmmsNum.Value);
        }

        // Apply Line Name filter (contains - case insensitive)
        if (filterLineName.IsActive && !string.IsNullOrWhiteSpace(filterLineName.Value))
        {
            var searchLine = filterLineName.Value.ToLower();
            query = filterLineName.IsNegated
                ? query.Where(r => !r.LineName.Contains(searchLine))
                : query.Where(r => r.LineName.Contains(searchLine));
        }

        // Apply Authorization Status filter
        if (filterIsAuthorized.IsActive)
        {
            query = filterIsAuthorized.IsNegated
                ? query.Where(r => r.IsAuthorized != filterIsAuthorized.Value)
                : query.Where(r => r.IsAuthorized == filterIsAuthorized.Value);
        }

        // Apply Start Date filter (inclusive)
        if (filterStartDate.IsActive)
        {
            query = filterStartDate.IsNegated
                ? query.Where(r => r.Timestamp < filterStartDate.Value)
                : query.Where(r => r.Timestamp >= filterStartDate.Value);
        }

        // Apply End Date filter (inclusive - include the entire day)
        if (filterEndDate.IsActive)
        {
            var endOfDay = filterEndDate.Value?.AddDays(1).AddTicks(-1);
            query = filterEndDate.IsNegated
                ? query.Where(r => r.Timestamp > endOfDay)
                : query.Where(r => r.Timestamp <= endOfDay);
        }

        return query;
    }

    /// <summary>
    /// Clears all filters and reloads the data
    /// </summary>
    protected async Task ClearAllFilters()
    {
        foreach (var filter in Filters.Values)
        {
            filter.Reset();
        }

        await LoadData();
        StateHasChanged();
    }
}
