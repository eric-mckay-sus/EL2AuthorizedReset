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
    int LastQueryHash;
    protected bool IsStale => LastQueryHash != GetFilterStateHash(Filters);

    // Filter properties that expose the filters in the registry with type safety
    protected Filter<int?> FilterAssocNum => GetFilter<int?>("assocNum");
    protected Filter<string?> FilterAssocName => GetFilter<string?>("assocName");
    protected Filter<int?> FilterCmmsNum => GetFilter<int?>("cmmsNum");
    protected Filter<string?> FilterLineName => GetFilter<string?>("lineName");
    protected Filter<bool?> FilterIsAuthorized => GetFilter<bool?>("isAuthorized");
    protected Filter<DateTime?> FilterStartDate => GetFilter<DateTime?>("after");
    protected Filter<DateTime?> FilterEndDate => GetFilter<DateTime?>("before");

    /// <summary>
    /// Generate a state ID, for checking equality between two filter states
    /// 17 and 31 are primes one off of powers of two, so we get few collisions and the compiler can take shortcuts
    /// </summary>
    /// <param name="filterDict">A dictionary of keys mapped to filters</param>
    /// <returns>A value representing the state of the filters for the input dictionary</returns>
    public int GetFilterStateHash(Dictionary<string, IFilter> filterDict) {
        unchecked // Tells the compiler to simply truncate the calculation instead of throwing an exception for integer overflow
        {
            int hash = 17;
            // Order by key to ensure dictionary order doesn't change the hash
            foreach (var key in filterDict.Keys.OrderBy(k => k))
            {
                // Ignore 'in' key, it does not affect the search contents within a table
                if (key.Equals("in", StringComparison.OrdinalIgnoreCase)) continue;

                // Factor in each aspect of the filter
                var filter = filterDict[key];

                // Key and activity status are part of hash regardless of activity status
                hash *= 31 + key.ToLower().GetHashCode();
                hash *= 31 + filter.IsActive.GetHashCode();

                // Only hash filter details if active
                if (filter.IsActive)
                {
                    hash *= 31 + filter.IsNegated.GetHashCode();
                    string value = filter.GetValue()?.ToString()?.Trim().ToLower() ?? "";
                    hash *= 31 + value.GetHashCode();
                }
            }
            // Sorts affect view, so a hash of the view should include them
            hash *= 31 + CurrentSortColumn.GetHashCode();
            hash *= 31 + SortDir.GetHashCode();
            
            return hash;
        }
    }

    /// <summary>
    /// When the page loads, initialize filters and sorts before loading data
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        InitializeFilters();
        SortDir = "descending";
        CurrentSortColumn = "Timestamp";
        await base.OnInitializedAsync();
    }

    /// <summary>
    /// Update the query hash after loading data
    /// </summary>
    /// <returns></returns>
    protected override async Task LoadData()
    {
        await base.LoadData();
        LastQueryHash = GetFilterStateHash(Filters);
        StateHasChanged();
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
