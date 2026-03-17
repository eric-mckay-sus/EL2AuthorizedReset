using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using AdminInterface.Components.Pages;
using System.Linq.Dynamic.Core;

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
    [Inject] protected IDbContextFactory<AuthResetDbContext> DbFactory { get; set; } = default!; // The thread-safe DB context generator
    [Parameter] public EventCallback<TRead> OnItemChanged { get; set; } // An event to detect when an item might not appear in the DataView

    // Filter registry to hold all active filters
    protected Dictionary<string, IFilter> Filters { get; set; } = [];
    protected bool _filtersInitialized = false; // Whether the filters have been initialized yet
    int LastQueryHash;
    protected bool IsStale => LastQueryHash != GetFilterStateHash(Filters);

    protected TWrite NewItem = new(); // The item to be added (from the add form)
    protected List<TRead> DataView = []; // The view to READ from (type may be different from the one being written)
    protected string? ErrorMessage; // The error message for uniqueness constraint, if applicable
    protected DeleteDialog deleteDialog = default!; // The dialog to show upon pressing the delete button for a row
    protected bool IsFormVisible = false; // Whether to show or hide the add form
    protected bool IsLoading = true; // Whether the DataView is loading

    // For sorting
    public string CurrentSortColumn { get; set; } = ""; // The name of the column that results are currently being sorted by
    public string SortDir { get; set; } = "none"; // The sort direction of the currently sorted column

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
    /// When the page loads, prepare the table
    /// </summary>
    /// <returns></returns>
    protected override async Task OnParametersSetAsync() => await LoadData();

    /// <summary>
    /// Load the table, applying any filters the child assigns
    /// </summary>
    /// <returns></returns>
    protected virtual async Task LoadData()
    {
        // Update and show loading state
        IsLoading = true;
        StateHasChanged();

        using var context = DbFactory.CreateDbContext();

        // Gets all results (delayed execution)
        IQueryable<TRead> query = context.Set<TRead>();

        // Apply filter(s) set by the child
        query = ApplyFilter(query);

        query = ApplySorting(query);

        // Execute here (DataView requires a list for display)
        DataView = await query.ToListAsync();
        LastQueryHash = GetFilterStateHash(Filters);
        IsLoading = false;
        StateHasChanged();
    }

    /// <summary>
    /// Override this in child components to provide specific filtering logic.
    /// </summary>
    /// <param name="query">The IQueryable implementation to which the filters should be applied</param>
    /// <returns>The query, filtered by whatever filter(s) applied by the child</returns>
    protected virtual IQueryable<TRead> ApplyFilter(IQueryable<TRead> query) => query;

    /// <summary>
    /// Throw flag to display add form, view handles the actual displaying
    /// </summary>
    protected void ShowForm() => IsFormVisible = true;
    
    /// <summary>
    /// Remove add form flag, clear input and error message
    /// </summary>
    protected void CancelForm() 
    {
        IsFormVisible = false;
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
            using var context = DbFactory.CreateDbContext();
            context.Set<TWrite>().Add(NewItem);
            await context.SaveChangesAsync();

            NewItem = new();
            await LoadData();
            IsFormVisible = false;
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
            using var context = DbFactory.CreateDbContext();
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
    public IQueryable<TRead> ApplySorting(IQueryable<TRead> query)
    {
        if (SortDir == "none" || string.IsNullOrWhiteSpace(CurrentSortColumn))
        {
            return query;
        }
        return query.OrderBy($"{CurrentSortColumn} {SortDir}");
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