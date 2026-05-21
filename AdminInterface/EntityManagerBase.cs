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
/// Defines the shared behavior for an admin interface page.
/// </summary>
/// <typeparam name="TWrite">The datatype to insert (row from SQL table).</typeparam>
/// <typeparam name="TRead">The datatype to show (row from SQL view, or table again if no view).</typeparam>
public class EntityManagerBase<TWrite, TRead> : ComponentBase // Technically could be abstract to denote it's never used standalone, but there's no point
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
    /// Gets or sets the name of the column that results are currently being sorted by.
    /// </summary>
    public string CurrentSortColumn { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort direction of the currently sorted column.
    /// </summary>
    public string SortDir { get; set; } = "none";

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
    /// Cycles through sort directions when column is toggled
    /// Cycle order: None -> Asc -> Desc.
    /// </summary>
    /// <param name="columnName">The column to be toggled.</param>
    /// <returns>A Task representing that the sort option has been applied.</returns>
    public async Task ToggleSort(string columnName)
    {
        if (this.CurrentSortColumn != columnName)
        { // If coming from none, save the column name (it's changed) and switch to asc
            this.CurrentSortColumn = columnName;
            this.SortDir = "ascending";
        }
        else if (this.SortDir == "ascending")
        { // If coming from asc, only need to switch to desc
            this.SortDir = "descending";
        }
        else
        { // If coming from desc, switch to none and inform model no column is specified to sort
            this.SortDir = "none";
            this.CurrentSortColumn = string.Empty;
        }

        await this.LoadData(); // because the sort parameters change we want a guaranteed refresh
    }

    /// <summary>
    /// Helper to render the arrow.
    /// </summary>
    /// <param name="columnName">The column for which to update the sort icon.</param>
    /// <returns>The Unicode arrow representing the sort direction.</returns>
    public string GetSortIcon(string columnName)
    {
        if (this.CurrentSortColumn != columnName || this.SortDir == "none")
        {
            return "↕";
        }

        return this.SortDir == "ascending" ? "▲" : "▼";
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
        return;
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
            hash *= 31 + this.CurrentSortColumn.GetHashCode();
            hash *= 31 + this.SortDir.GetHashCode();

            return hash;
        }
    }

    /// <summary>
    /// Uses dynamic LINQ to draft a SQL ORDER BY based on the current sort.
    /// </summary>
    /// <param name="query">The query to which the sorts should be appended.</param>
    /// <returns>An IQueryable object with sorts applied.</returns>
    private IQueryable<TRead> ApplySorting(IQueryable<TRead> query)
    {
        if (this.SortDir == "none" || string.IsNullOrWhiteSpace(this.CurrentSortColumn))
        {
            return query;
        }

        // Null is the smallest value for any column, so it clutters ascending sorts
        return query.Where($"{this.CurrentSortColumn} != null").OrderBy($"{this.CurrentSortColumn} {this.SortDir}");
    }
}
