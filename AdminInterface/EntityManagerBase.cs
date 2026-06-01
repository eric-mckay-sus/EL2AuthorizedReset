// <copyright file="EntityManagerBase.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace AdminInterface;

using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;

using AdminInterface.Components.Common;
using InterProcessIO;

/// <summary>
/// Non-generic parent of <see cref="EntityManagerBase{TRead, TWrite}"/> to contain static information.
/// </summary>
public class EntityManagerBase : ComponentBase
{
    /// <summary>
    /// The maximum allowable sorts active at once.
    /// </summary>
    protected static readonly byte MaxSorts = 2;

    /// <summary>
    /// The array of Unicode digits to use for arrow subscript building.
    /// </summary>
    protected static readonly char[] SubscriptDigits = ['₀', '₁', '₂', '₃', '₄', '₅', '₆', '₇', '₈', '₉'];
}

/// <summary>
/// Defines the shared behavior for an admin interface page.
/// </summary>
/// <typeparam name="TWrite">The datatype to insert (row from SQL table).</typeparam>
/// <typeparam name="TRead">The datatype to show (row from SQL view, or table again if no view).</typeparam>
public class EntityManagerBase<TWrite, TRead> : EntityManagerBase // Technically could be abstract to denote it's never used standalone, but there's no point
    where TWrite : class, new()
    where TRead : class, new()
{
    private int lastQueryHash;

    private Dictionary<string, IFilter> filterSnapshot = [];

    /// <summary>
    /// Gets or sets this upload page's input provider.
    /// </summary>
    [Inject]
    public BlazorInputProvider InputProvider { get; set; } = default!;

    /// <summary>
    /// Gets or sets this upload page's output provider.
    /// </summary>
    [Inject]
    public BlazorReporter Reporter { get; set; } = default!;

    /// <summary>
    /// Gets or sets the event to detect when an item might not appear in the DataView.
    /// </summary>
    [Parameter]
    public EventCallback<TRead> OnItemChanged { get; set; }

    /// <summary>
    /// Gets or sets the filter registry to hold all active filters.
    /// </summary>
    public Dictionary<string, IFilter> Filters { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether the data in <see cref="DataView"/> matches the pending filter state.
    /// </summary>
    public bool IsStale => this.lastQueryHash != this.GetFilterStateHash(this.Filters);

    /// <summary>
    /// Gets the current page number (always clamped between 0 and <see cref="TotalPages"/>, inclusive).
    /// </summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>
    /// Gets or sets the number of rows on one page.
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Gets the total number of rows retrieved by the current query.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// Gets the number of pages retrieved by the current query (total rows divided by page size).
    /// </summary>
    public int TotalPages => this.PageSize > 0 ? (int)Math.Ceiling((double)this.TotalCount / this.PageSize) : 1;

    /// <summary>
    /// Gets the view to READ from (type may be different from the one being written).
    /// </summary>
    public List<TRead> DataView { get; private set; } = [];

    /// <summary>
    /// Gets or sets the error message for uniqueness constraint, if applicable.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets a value indicating whether the DataView is loading.
    /// </summary>
    public bool IsLoading { get; private set; } = true;

    /// <summary>
    /// Gets the list of sorts to be applied to the query.
    /// </summary>
    protected List<Sort> SortList { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the insertion/update form is open.
    /// </summary>
    private protected bool IsFormVisible { get; set; } = false; // Whether to show or hide the add form

    /// <summary>
    /// Gets or sets the item to be added (from the add/update form).
    /// </summary>
    private protected TWrite NewItem { get; set; } = new ();

    /// <summary>
    /// Gets or sets the dialog to show upon pressing the delete button for a row.
    /// </summary>
    private protected DeleteDialog DeleteDialog { get; set; } = default!;

    /// <summary>
    /// Gets or sets the thread-safe DB context generator.
    /// </summary>
    [Inject]
    private protected IDbContextFactory<AuthResetDbContext> DbFactory { get; set; } = default!;

    /// <summary>
    /// Gets or sets the toast service for displaying success/failure messages.
    /// </summary>
    [Inject]
    private protected ToastService ToastService { get; set; } = default!;

    /// <summary>
    /// Load the table, applying any filters/sorts the child assigns.
    /// </summary>
    /// <param name="keepPage">Whether to persist the page number (or reset to page 1).</param>
    /// <param name="updateCounts">Whether the filters have changed, thus the filter counters should update.</param>
    /// <returns>A Task representing that the data has been reloaded.</returns>
    public virtual async Task LoadData(bool keepPage = false, bool updateCounts = false)
    {
        if (!keepPage)
        {
            this.CurrentPage = 1;
        }

        // Update and show loading state
        this.IsLoading = true;
        this.StateHasChanged();

        using AuthResetDbContext context = await this.DbFactory.CreateDbContextAsync();

        // Gets all results (delayed execution)
        IQueryable<TRead> query = context.Set<TRead>().AsNoTracking();

        // Apply filter(s) set by the child, sort, then count
        query = this.ApplyFilters(query);
        query = this.ApplySorting(query);
        this.TotalCount = await query.CountAsync(); // have to count after sort bc it applies an assumed 'sort col not null' filter

        // Execute here (DataView requires a list for display)
        this.DataView = await query
                .Skip((this.CurrentPage - 1) * this.PageSize)
                .Take(this.PageSize)
                .ToDynamicListAsync<TRead>();

        this.lastQueryHash = this.GetFilterStateHash(this.Filters);
        if (updateCounts)
        {
            this.filterSnapshot = this.Filters.ToDictionary(entry => entry.Key, entry => entry.Value.Clone());
        }

        this.IsLoading = false;
        this.StateHasChanged();
    }

    /// <summary>
    /// Counts filters used in the last call to LoadData.
    /// </summary>
    /// <returns>The number of filters used in the last call to LoadData.</returns>
    public int CountActiveFilters() => this.filterSnapshot.Values.Count(x => x.IsActive);

    /// <summary>
    /// Counts filters where the UI value differs from the value in the snapshot.
    /// </summary>
    /// <returns>The number of filters where the UI value differs from the value in the snapshot.</returns>
    public int CountPendingFilters()
    {
        return this.Filters.Count(kvp =>
        {
            // If it doesn't exist in the snapshot, it's pending if it's currently active
            if (!this.filterSnapshot.TryGetValue(kvp.Key, out IFilter? snapshot))
            {
                return kvp.Value.IsActive;
            }

            object? currentValue = kvp.Value.GetValue();
            object? snapshotValue = snapshot.GetValue();

            // Check for value equality. Using !Equals handles null transitions correctly.
            return !Equals(currentValue, snapshotValue);
        });
    }

    /// <summary>
    /// Cycle order: None -> Asc -> Desc. All sort columns are "drive to zero". Toggling an already-sorted column simply follows the cycle.
    /// When a column is toggled to <see cref="SortDir.None"/>, the sort is removed from the list, freeing up a sort slot and promoting all lesser sorts.
    /// For example, toggling column B with the sort list [{col A, asc}, {col B, desc}, {col C, asc}] promotes col C and leaves col A unaffected, resulting in [{col A, asc}, {col C, asc}]
    /// The number of available sorts may be modified (to increase customization or to simplify) with <see cref="EntityManagerBase.MaxSorts"/>.
    /// When toggling a new column, it is assigned the highest available sort priority. If there are no open sort slots, it overwrites the lowest priority sort.
    /// Sort priority is visualized with the subscript next to the sort arrow.
    /// </summary>
    /// <param name="columnName">The column to be toggled.</param>
    /// <returns>A Task representing that the sort has been applied.</returns>
    public async Task ToggleSort(string columnName)
    {
        // Determines if the column being toggled is already being sorted
        Sort? existingSort = this.SortList.FirstOrDefault(x => x.ColumnName == columnName);

        // If this column is already being sorted, cycle the state, removing if deactivated
        if (existingSort != null)
        {
            bool isActive = existingSort.Toggle();
            if (!isActive)
            {
                this.SortList.Remove(existingSort);
            }
        }

        // Otherwise, add it.
        else
        {
            Sort newSort = new (columnName, SortDir.Asc);

            // If there's an open slot, use it
            if (this.SortList.Count < MaxSorts)
            {
                this.SortList.Add(newSort);
            }

            // If not, overwrite the last sort
            else
            {
                this.SortList[^1] = newSort;
            }
        }

        await this.LoadData();
    }

    /// <summary>
    /// Helper to render the arrow for <paramref name="columnName"/>.
    /// Denotes sort priority with the subscript attached to the arrow (primary sort gets no subscript).
    /// </summary>
    /// <param name="columnName">The column for which to update the sort icon.</param>
    /// <returns>The Unicode arrow representing the sort direction and sort priority.</returns>
    public string GetSortIcon(string columnName)
    {
        var sortEntry = this.SortList
            .Select((s, i) => new { s.ColumnName, s.Direction, Index = i })
            .FirstOrDefault(x => x.ColumnName == columnName);

        if (sortEntry == null || sortEntry.Direction == SortDir.None)
        {
            return "↕";
        }

        string arrow = sortEntry.Direction == SortDir.Asc ? "▲" : "▼";

        // Concatenate the arrow with its priority subscript. The primary sort gets no subscript.
        return arrow + GetSubscript(sortEntry.Index + 1);
    }

    /// <summary>
    /// Jumps to the specified new page (if within bounds).
    /// </summary>
    /// <param name="newPage">The page number to jump to.</param>
    /// <returns>A Task representing that the page has been changed to <paramref name="newPage"/>.</returns>
    public async Task ChangePage(int newPage)
    {
        if (newPage != this.CurrentPage && newPage >= 1 && newPage <= this.TotalPages)
        {
            this.CurrentPage = newPage;
            await this.LoadData(keepPage: true);
        }
    }

    /// <summary>
    /// Modifies the page size from <see cref="PageSize"/> to <paramref name="newSize"/>.
    /// </summary>
    /// <param name="newSize">The desired number of entries per page.</param>
    /// <returns>A Task representing that the number of records per page are now <paramref name="newSize"/>.</returns>
    public async Task AlterPageSize(int newSize)
    {
        if (newSize != this.PageSize)
        {
            this.PageSize = newSize;

            // Reset to page 1 because the number of pages has changed
            this.CurrentPage = 1;
            await this.LoadData();
        }
    }

    /// <summary>
    /// When an instance of EntityManagerBase is initialized, load the filter registry.
    /// </summary>
    protected override void OnInitialized() => this.InitializeFilters();

    /// <summary>
    /// Hook for children to initialize filters by adding them to the registry.
    /// The generic EntityBaseManager has no filters, so return immediately.
    /// </summary>
    protected virtual void InitializeFilters()
    {
    }

    /// <summary>
    /// Clears all filters and reloads the data.
    /// </summary>
    /// <returns>A Task representing that the filters have been cleared.</returns>
    protected async Task ClearAllFilters()
    {
        foreach (IFilter filter in this.Filters.Values)
        {
            filter.Reset();
        }

        await this.LoadData(updateCounts: true);
        this.StateHasChanged();
    }

    /// <summary>
    /// Helper method to get a strongly-typed filter from the registry.
    /// </summary>
    /// <typeparam name="T">The type of the filter value (int, string, DateTime, or bool).</typeparam>
    /// <param name="key">The key of the filter to retrieve.</param>
    /// <returns>The filter with appropriate type.</returns>
    protected Filter<T> GetFilter<T>(string key)
    {
        if (this.Filters.TryGetValue(key, out IFilter? filter) && filter is Filter<T> typedFilter)
        {
            return typedFilter;
        }

        // If filter doesn't exist or has wrong type, create a new one
        var newFilter = new Filter<T>(key, default);
        this.Filters[key] = newFilter;
        return newFilter;
    }

    /// <summary>
    /// When the page loads, prepare the table.
    /// </summary>
    /// <returns>A Task representing that the data has been reloaded.</returns>
    protected override async Task OnParametersSetAsync() => await this.LoadData();

    /// <summary>
    /// Override this in child components to provide specific filtering logic.
    /// </summary>
    /// <param name="query">The IQueryable implementation to which the filters should be applied.</param>
    /// <returns>The query, filtered by whatever filter(s) applied by the child.</returns>
    protected virtual IQueryable<TRead> ApplyFilters(IQueryable<TRead> query) => query;

    /// <summary>
    /// Throw flag to display add form, view handles the actual displaying.
    /// </summary>
    protected void ShowForm() => this.IsFormVisible = true;

    /// <summary>
    /// Remove add form flag, clear input and error message.
    /// </summary>
    protected virtual void CloseForm()
    {
        this.IsFormVisible = false;
        this.NewItem = new TWrite();
        this.ErrorMessage = null;
    }

    /// <summary>
    /// On submit, attempt to insert into table, and catch potential constraint violations.
    /// </summary>
    /// <returns>A Task representing that <see cref="NewItem"/> has been successfully inserted/updated.</returns>
    protected virtual async Task HandleValidSubmit()
    {
        this.ErrorMessage = null;
        try
        {
            using AuthResetDbContext context = this.DbFactory.CreateDbContext();
            context.Set<TWrite>().Add(this.NewItem);
            await context.SaveChangesAsync();

            this.ShowSuccessToast();
            this.NewItem = new ();
            await this.LoadData();
            this.CloseForm();
        }
        catch (DbUpdateException)
        {
            // Fallback for race conditions (form validation handled elsewhere)
            this.ErrorMessage = "A database error occurred. The data may have changed since you opened the form.";
        }
        catch (Exception)
        {
            this.ErrorMessage = "An unexpected error occurred. Please try again.";
        }
    }

    /// <summary>
    /// Hook for children to display toast before NewItem is cleared.
    /// </summary>
    protected virtual void ShowSuccessToast()
    {
    }

    /// <summary>
    /// Assigns default behavior for removing a row from the database
    /// MUST be overridden in child if TRead is not the same type as TWrite (recommend interface between the two)
    /// Override if toast is desired.
    /// </summary>
    /// <param name="context">The current DB context.</param>
    /// <param name="item">The item to delete.</param>
    /// <returns>A Task representing that <paramref name="item"/> has been logically deleted.</returns>
    /// <exception cref="InvalidOperationException">When TRead is not the same type as TWrite.</exception>
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
    /// Shows the delete dialog, and if confirmed, remove from underlying table in the DB (then update view).
    /// </summary>
    /// <param name="item">The item to delete from the view.</param>
    /// <returns>A Task representing that <paramref name="item"/> has been removed and the view has been updated.</returns>
    protected async Task HandleDelete(TRead item)
    {
        if (await this.DeleteDialog.ConfirmAsync(item))
        {
            using AuthResetDbContext context = this.DbFactory.CreateDbContext();
            await this.ExecuteDelete(context, item);

            if (this.OnItemChanged.HasDelegate)
            {
                await this.OnItemChanged.InvokeAsync(item);
            }

            await this.LoadData();
        }
    }

    /// <summary>
    /// Generate a state ID, for checking equality between two filter states
    /// 17 and 31 are primes one off of powers of two, so we get few collisions and the compiler can take shortcuts.
    /// </summary>
    /// <param name="filterDict">A dictionary of keys mapped to filters.</param>
    /// <returns>A value representing the state of the filters for the input dictionary.</returns>
    private protected virtual int GetFilterStateHash(Dictionary<string, IFilter> filterDict)
    {
        // Tells the compiler to simply truncate the calculation instead of throwing an exception for integer overflow
        unchecked
        {
            int hash = 17;

            // Order by key to ensure dictionary order doesn't change the hash
            foreach (string key in filterDict.Keys.OrderBy(k => k))
            {
                // Ignore 'in' key, it does not affect the search contents within a table
                if (key.Equals("in", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Factor in each aspect of the filter
                IFilter filter = filterDict[key];

                // Key and activity status are part of hash regardless of activity status
                hash *= 31 + key.ToLower().GetHashCode();
                hash *= 31 + filter.IsActive.GetHashCode();

                // Only hash filter details if active
                if (filter.IsActive)
                {
                    string value = filter.GetValue()?.ToString()?.Trim().ToLower() ?? string.Empty;
                    hash *= 31 + value.GetHashCode();
                }
            }

            // Sorts affect view, so a hash of the view should include them
            hash *= 31 + this.SortList.GetHashCode();

            return hash;
        }
    }

    /// <summary>
    /// Uses dynamic LINQ to draft a SQL ORDER BY based on the current sort.
    /// </summary>
    /// <param name="query">The query to which the sorts should be appended.</param>
    /// <returns>An IQueryable object with sorts applied.</returns>
    private protected IQueryable<TRead> ApplySorting(IQueryable<TRead> query)
    {
        // If there is no sort, simply order by itself (PK for DB objects)
        if (this.SortList.Count == 0)
        {
            return query.OrderBy(x => x);
        }

        bool isFirst = true;

        // Iterate through all the sorts and apply them in series
        foreach (Sort sort in this.SortList)
        {
            // Incomplete Sort objects shouldn't be in the list to begin with, but if they are, just ignore them
            if (string.IsNullOrEmpty(sort.ColumnName))
            {
                continue;
            }

            string sortExpression = $"{sort.ColumnName} {Sort.SortDirString[sort.Direction]}";

            // Ensure the first sort uses OrderBy instead of ThenBy, and throw flag to use ThenBy for remaining filters
            if (isFirst)
            {
                query = query.Where($"{sort.ColumnName} != null").OrderBy(sortExpression);
                isFirst = false;
            }
            else
            {
                query = ((IOrderedQueryable<TRead>)query).ThenBy(sortExpression);
            }
        }

        return query;
    }

    /// <summary>
    /// Converts a 1-digit or 2-digit integer into Unicode subscript characters.
    /// Designed for enumerating sort priority
    /// Specifically designed not to be extensible in order to avoid issues with looped string concatenation.
    /// </summary>
    private static string GetSubscript(int number)
    {
        // If it's the primary sort, we return empty (actually draws MORE attention)
        if (number <= 1)
        {
            return string.Empty;
        }

        // For a 2-digit number
        if (number >= 10)
        {
            return string.Create(2, number, (span, num) =>
            {
                span[0] = SubscriptDigits[(num / 10) % 10];
                span[1] = SubscriptDigits[num % 10];
            });
        }

        // For a 1-digit number
        return SubscriptDigits[number].ToString();
    }
}
