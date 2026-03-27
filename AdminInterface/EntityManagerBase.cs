using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using AdminInterface.Components.Pages.CommonComponents;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;

namespace AdminInterface;

/// <summary>
/// Defines the shared behavior for an admin interface page
/// </summary>
/// <typeparam name="TWrite">The datatype to insert (row from SQL table)</typeparam>
/// <typeparam name="TRead">The datatype to show (row from SQL view, or table again if no view)</typeparam>
public class EntityManagerBase<TWrite, TRead> : ComponentBase // Technically could be abstract to denote it's never used standalone, but there's no point
    where TWrite : class, new()
    where TRead : class, new()
{
    [Inject] private protected IDbContextFactory<AuthResetDbContext> DbFactory { get; set; } = default!; // The thread-safe DB context generator
    [Parameter] public EventCallback<TRead> OnItemChanged { get; set; } // An event to detect when an item might not appear in the DataView

    // Filter registry to hold all active filters
    public Dictionary<string, IFilter> Filters { get; set; } = [];
    private int _lastQueryHash;
    private Dictionary<string, IFilter> _filterSnapshot { get; set; } = [];
    public bool IsStale => _lastQueryHash != GetFilterStateHash(Filters);

    // Pagination variables
    public int CurrentPage { get; set; } = 1; // Tracks the current page number (always between 1 and TotalPages, inclusive)
    public int PageSize { get; set; } = 50; // The number of results per page
    public int TotalCount { get; set; } // The total number of results
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize); // Dynamically compute page count whenever totalCount or pageSize update

    private protected TWrite NewItem = new(); // The item to be added (from the add form)
    public List<TRead> DataView = []; // The view to READ from (type may be different from the one being written)
    public string? ErrorMessage; // The error message for uniqueness constraint, if applicable
    private protected DeleteDialog deleteDialog = default!; // The dialog to show upon pressing the delete button for a row
    private protected bool _isFormVisible = false; // Whether to show or hide the add form
    public bool IsLoading = true; // Whether the DataView is loading

    // For sorting
    public string CurrentSortColumn { get; set; } = ""; // The name of the column that results are currently being sorted by
    public string SortDir { get; set; } = "none"; // The sort direction of the currently sorted column

    /// <summary>
    /// Generate a state ID, for checking equality between two filter states
    /// 17 and 31 are primes one off of powers of two, so we get few collisions and the compiler can take shortcuts
    /// </summary>
    /// <param name="filterDict">A dictionary of keys mapped to filters</param>
    /// <returns>A value representing the state of the filters for the input dictionary</returns>
    private protected virtual int GetFilterStateHash(Dictionary<string, IFilter> filterDict) {
        unchecked // Tells the compiler to simply truncate the calculation instead of throwing an exception for integer overflow
        {
            int hash = 17;
            // Order by key to ensure dictionary order doesn't change the hash
            foreach (string key in filterDict.Keys.OrderBy(k => k))
            {
                // Ignore 'in' key, it does not affect the search contents within a table
                if (key.Equals("in", StringComparison.OrdinalIgnoreCase)) continue;

                // Factor in each aspect of the filter
                IFilter filter = filterDict[key];

                // Key and activity status are part of hash regardless of activity status
                hash *= 31 + key.ToLower().GetHashCode();
                hash *= 31 + filter.IsActive.GetHashCode();

                // Only hash filter details if active
                if (filter.IsActive)
                {
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
    /// When an instance of EntityManagerBase is initialized, load the filter registry
    /// </summary>
    protected override void OnInitialized() => InitializeFilters();

    /// <summary>
    /// Hook for children to initialize filters by adding them to the registry.
    /// The generic EntityBaseManager has no filters, so return immediately
    /// </summary>
    protected virtual void InitializeFilters()
    {
        return;
    }

    /// <summary>
    /// Helper method to get a strongly-typed filter from the registry
    /// </summary>
    /// <typeparam name="T">The type of the filter value (int, string, DateTime, or bool)</typeparam>
    /// <param name="key">The key of the filter to retrieve</param>
    /// <returns>The filter with appropriate type</returns>
    protected Filter<T> GetFilter<T>(string key)
    {
        if (Filters.TryGetValue(key, out IFilter? filter) && filter is Filter<T> typedFilter)
        {
            return typedFilter;
        }

        // If filter doesn't exist or has wrong type, create a new one
        var newFilter = new Filter<T>(key, default);
        Filters[key] = newFilter;
        return newFilter;
    }

    /// <summary>
    /// Counts filters used in the last call to LoadData
    /// </summary>
    /// <returns></returns>
    public int CountActiveFilters() => _filterSnapshot.Values.Count(x => x.IsActive);

    /// <summary>
    /// Counts filters where the UI value differs from the value in the snapshot
    /// </summary>
    /// <returns></returns>
    public int CountPendingFilters()
    {
        return Filters.Count(kvp =>
        {
            // If it doesn't exist in the snapshot, it's pending if it's currently active
            if (!_filterSnapshot.TryGetValue(kvp.Key, out IFilter? snapshot))
                return kvp.Value.IsActive;

            object? currentValue = kvp.Value.GetValue();
            object? snapshotValue = snapshot.GetValue();

            // Check for value equality. Using !Equals handles null transitions correctly.
            return !Equals(currentValue, snapshotValue);
        });
    }

    /// <summary>
    /// When the page loads, prepare the table
    /// </summary>
    /// <returns></returns>
    protected override async Task OnParametersSetAsync() => await LoadData();

    /// <summary>
    /// Load the table, applying any filters the child assigns
    /// </summary>
    /// <returns></returns>
    public virtual async Task LoadData(bool keepPage=false, bool updateCounts=false)
    {
        if (!keepPage) CurrentPage = 1;
        // Update and show loading state
        IsLoading = true;
        StateHasChanged();

        using AuthResetDbContext context = await DbFactory.CreateDbContextAsync();

        // Gets all results (delayed execution)
        IQueryable<TRead> query = context.Set<TRead>().AsNoTracking();

        // Apply filter(s) set by the child, sort, then count
        query = ApplyFilters(query);
        query = ApplySorting(query);
        TotalCount = await query.CountAsync(); // have to count after sort bc it applies an assumed 'sort col not null' filter

        // Execute here (DataView requires a list for display)
        DataView = await query
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

        _lastQueryHash = GetFilterStateHash(Filters);
        if(updateCounts) _filterSnapshot = Filters.ToDictionary(entry => entry.Key, entry => entry.Value.Clone());
        IsLoading = false;
        StateHasChanged();
    }

    /// <summary>
    /// Override this in child components to provide specific filtering logic.
    /// </summary>
    /// <param name="query">The IQueryable implementation to which the filters should be applied</param>
    /// <returns>The query, filtered by whatever filter(s) applied by the child</returns>
    protected virtual IQueryable<TRead> ApplyFilters(IQueryable<TRead> query) => query;

    /// <summary>
    /// Throw flag to display add form, view handles the actual displaying
    /// </summary>
    protected void ShowForm() => _isFormVisible = true;

    /// <summary>
    /// Remove add form flag, clear input and error message
    /// </summary>
    protected virtual void CloseForm()
    {
        _isFormVisible = false;
        NewItem = new TWrite();
        ErrorMessage = null;
    }

    /// <summary>
    /// On submit, attempt to insert into table, and catch potential constraint violations
    /// </summary>
    /// <returns></returns>
    protected virtual async Task HandleValidSubmit()
    {
        ErrorMessage = null;
        try
        {
            using AuthResetDbContext context = DbFactory.CreateDbContext();
            context.Set<TWrite>().Add(NewItem);
            await context.SaveChangesAsync();

            NewItem = new();
            await LoadData();
            CloseForm();
        }
        catch (DbUpdateException)
        {
            // Fallback for race conditions (form validation handled elsewhere)
            ErrorMessage = "A database error occurred. The data may have changed since you opened the form.";
        }
        catch (Exception)
        {
            ErrorMessage = "An unexpected error occurred. Please try again.";
        }
    }

    /// <summary>
    /// Assigns default behavior for removing a row from the database
    /// MUST be overridden in child if TRead is not the same type as TWrite (recommend interface between the two)
    /// </summary>
    /// <param name="context">The current DB context</param>
    /// <param name="item">The item to delete</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">When TRead is not the same type as TWrite</exception>
    protected virtual async Task ExecuteDelete(AuthResetDbContext context, TRead item)
    {
        // Default behavior: Assume TRead is TWrite
        if (item is TWrite writeable)
        {
            context.Set<TWrite>().Remove(writeable);
            await context.SaveChangesAsync();
        }
        else
        {
            throw new InvalidOperationException("Override ExecuteDelete for complex Read/Write mappings.");
        }
    }

    /// <summary>
    /// Shows the delete dialog, and if confirmed, remove from underlying table in the DB (then update view)
    /// </summary>
    /// <param name="item">The item to delete from the view</param>
    /// <returns></returns>
    protected async Task HandleDelete(TRead item)
    {
        if (await deleteDialog.ConfirmAsync(item))
        {
            using AuthResetDbContext context = DbFactory.CreateDbContext();
            await ExecuteDelete(context, item);

            if (OnItemChanged.HasDelegate)
            {
                await OnItemChanged.InvokeAsync(item);
            }

            await LoadData();
        }
    }

    /// <summary>
    /// Cycles through sort directions when column is toggled
    /// Cycle order: None -> Asc -> Desc
    /// </summary>
    /// <param name="columnName">The column to be toggled</param>
    /// <returns></returns>
    public async Task ToggleSort(string columnName)
    {
        if (CurrentSortColumn != columnName) { // If coming from none, save the column name (it's changed) and switch to asc
            CurrentSortColumn = columnName;
            SortDir = "ascending";
        } else if(SortDir == "ascending") { // If coming from asc, only need to switch to desc
            SortDir = "descending";
        } else { // If coming from desc, switch to none and inform model no column is specified to sort
            SortDir = "none";
            CurrentSortColumn = "";
        }
        await LoadData(); // because the sort parameters change we want a guaranteed refresh
    }

    /// <summary>
    /// Uses dynamic LINQ to draft a SQL ORDER BY based on the current sort
    /// </summary>
    /// <param name="query">The query to which the sorts should be appended</param>
    /// <returns>An IQueryable object with sorts applied</returns>
    private IQueryable<TRead> ApplySorting(IQueryable<TRead> query)
    {
        if (SortDir == "none" || string.IsNullOrWhiteSpace(CurrentSortColumn))
        {
            return query;
        }
        // Null is the smallest value for any column, so it clutters ascending sorts
        return query.Where($"{CurrentSortColumn} != null").OrderBy($"{CurrentSortColumn} {SortDir}");
    }

    /// <summary>
    /// Helper to render the arrow
    /// </summary>
    /// <param name="columnName">The column for which to update the sort icon</param>
    /// <returns>The Unicode arrow representing the sort direction</returns>
    public string GetSortIcon(string columnName)
    {
        if (CurrentSortColumn != columnName || SortDir == "none") return "↕";
        return SortDir == "ascending" ? "▲" : "▼";
    }

    /// <summary>
    /// Jumps to the specified new page (if within bounds)
    /// </summary>
    /// <param name="newPage">The page number to jump to</param>
    /// <returns></returns>
    public async Task ChangePage(int newPage)
    {
        if (newPage != CurrentPage && newPage >= 1 && newPage <= TotalPages)
        {
            CurrentPage = newPage;
            await LoadData(keepPage: true);
        }
    }

    /// <summary>
    /// Modifies the page size from PageSize to newSize
    /// </summary>
    /// <param name="newSize">The desired number of entries per page</param>
    /// <returns></returns>
    public async Task AlterPageSize(int newSize)
    {
        if (newSize != PageSize)
        {
            PageSize = newSize;
            // Reset to page 1 because the number of pages has changed
            CurrentPage = 1;
            await LoadData();
        }
    }

    /// <summary>
    /// Clears all filters and reloads the data
    /// </summary>
    protected internal async Task ClearAllFilters()
    {
        foreach (IFilter filter in Filters.Values)
        {
            filter.Reset();
        }

        await LoadData(updateCounts:true);
        StateHasChanged();
    }
}
