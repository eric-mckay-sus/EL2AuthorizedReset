// <copyright file="SearchLockouts.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace AdminInterface.Components.Pages.HomePage;

using Microsoft.AspNetCore.Components;

/// <summary>
/// The code-behind for the lockout search sub-page.
/// </summary>
public partial class SearchLockouts
{
    private bool blazorStop = true;
    private bool isCollapsed = false;

    /// <summary>
    /// Gets or sets the targeted lockout-reset.
    /// </summary>
    [Parameter]
    public LockoutReset? Target { get; set; }

    /// <summary>
    /// Gets or sets the action to perform upon pressing the expand button.
    /// </summary>
    [Parameter]
    public EventCallback<LockoutReset> HandleExpand { get; set; }

    /// <summary>
    /// Gets a value indicating whether the lockout level filter is to get levels above (or levels below).
    /// </summary>
    public bool LockoutLevelGreaterThan { get; private set; } = true;

    // Filter accessors for cleaner HTML

    /// <summary>
    /// Gets the CMMS number filter from the filter registry.
    /// </summary>
    private Filter<int?> FilterCmmsNum => this.GetFilter<int?>("cmmsNum");

    /// <summary>
    /// Gets the lockout reason filter from the filter registry.
    /// </summary>
    private Filter<string?> FilterReason => this.GetFilter<string?>("reason");

    /// <summary>
    /// Gets the lockout level filter from the filter registry.
    /// </summary>
    private Filter<int?> FilterLockoutLevel => this.GetFilter<int?>("lockoutLevel");

    /// <summary>
    /// Gets the line status filter from the filter registry.
    /// </summary>
    private Filter<string?> FilterStatus => this.GetFilter<string?>("status");

    /// <summary>
    /// Gets the line name filter from the filter registry.
    /// </summary>
    private Filter<string?> FilterLineName => this.GetFilter<string?>("lineName");

    /// <summary>
    /// Gets the resetter name filter from the filter registry.
    /// </summary>
    private Filter<string?> FilterResetter => this.GetFilter<string?>("resetter");

    // Time range filters

    /// <summary>
    /// Gets the filter for lockouts after target date from the filter registry.
    /// </summary>
    private Filter<DateTime?> FilterLockAfter => this.GetFilter<DateTime?>("lockAfter");

    /// <summary>
    /// Gets the filter for lockouts before target date from the filter registry.
    /// </summary>
    private Filter<DateTime?> FilterLockBefore => this.GetFilter<DateTime?>("lockBefore");

    /// <summary>
    /// Gets the filter for resets after target date from the filter registry.
    /// </summary>
    private Filter<DateTime?> FilterResetAfter => this.GetFilter<DateTime?>("resetAfter");

    /// <summary>
    /// Gets the filter for resets before target date from the filter registry.
    /// </summary>
    private Filter<DateTime?> FilterResetBefore => this.GetFilter<DateTime?>("resetBefore");

    /// <summary>
    /// Override for table refresh that verifies the targeted lockout is still in the dataview.
    /// If it isn't, tell the parent to hide the associated resets.
    /// </summary>
    /// <param name="keepPage"><inheritdoc path="/param[@name='keepPage']"/></param>
    /// <param name="updateCounts"><inheritdoc path="/param[@name='updateCounts']"/></param>
    /// <returns><inheritdoc/></returns>
    public override async Task LoadData(bool keepPage = false, bool updateCounts = false)
    {
        await base.LoadData(keepPage, updateCounts);

        if (this.Target != null && !this.DataView.Contains(this.Target))
        {
            this.blazorStop = false;
        }
    }

    /// <summary>
    /// Load the filter registry for searching the lockout/reset table.
    /// </summary>
    protected override void InitializeFilters()
    {
        this.Filters["cmmsNum"] = new Filter<int?>("cmmsNum", null);
        this.Filters["reason"] = new Filter<string?>("reason", null);
        this.Filters["lockoutLevel"] = new Filter<int?>("lockoutLevel", null);
        this.Filters["status"] = new Filter<string?>("status", null);
        this.Filters["lineName"] = new Filter<string?>("lineName", null);
        this.Filters["resetter"] = new Filter<string?>("resetter", null);

        this.Filters["lockAfter"] = new Filter<DateTime?>("lockAfter", null);
        this.Filters["lockBefore"] = new Filter<DateTime?>("lockBefore", null);
        this.Filters["resetAfter"] = new Filter<DateTime?>("resetAfter", null);
        this.Filters["resetBefore"] = new Filter<DateTime?>("resetBefore", null);
    }

    /// <summary>
    /// Skip LoadData if there's no change that requires a DB hit.
    /// </summary>
    /// <returns>A Task representing that the conditional load has completed.</returns>
    protected override async Task OnParametersSetAsync()
    {
        if (this.TotalCount == 0)
        {
            await base.OnParametersSetAsync();
        }
    }

    /// <summary>
    /// When the app loads, fill the filter registry and default sort.
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        this.CurrentSortColumn = "LockoutTime";
        this.SortDir = "descending";
    }

    /// <summary>
    /// When rendering the page, detect if <see cref="Target"/> may now be absent.
    /// </summary>
    /// <param name="firstRender"><inheritdoc path="/param[@name='firstRender']"/></param>
    /// <returns>A Task representing that post-render operations have completed.</returns>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!this.blazorStop)
        {
            this.blazorStop = true;

            // The UI has rendered the new page results, so now it's safe to close.
            await this.OnItemChanged.InvokeAsync(this.Target);
        }
    }

    /// <summary>
    /// Apply all filters with input in the filter panel.
    /// </summary>
    /// <param name="query">The query to which the filters should be applied.</param>
    /// <returns>The filtered query.</returns>
    protected override IQueryable<LockoutReset> ApplyFilters(IQueryable<LockoutReset> query)
    {
        // CMMS Number (Exact)
        if (this.FilterCmmsNum.IsActive)
        {
            query = query.Where(x => x.CmmsNum == this.FilterCmmsNum.Value);
        }

        // Status (Exact match from dropdown)
        if (this.FilterStatus.IsActive && !string.IsNullOrEmpty(this.FilterStatus.Value))
        {
            query = query.Where(x => x.Status == this.FilterStatus.Value);
        }

        // Reason (Partial match)
        if (this.FilterReason.IsActive)
        {
            query = query.Where(x => x.Reason != null && x.Reason.Contains(this.FilterReason.Value!));
        }

        // Lockout Level (< or >)
        if (this.FilterLockoutLevel.IsActive)
        {
            if (this.LockoutLevelGreaterThan)
            {
                query = query.Where(x => x.LockoutLevel >= this.FilterLockoutLevel.Value);
            }
            else
            {
                query = query.Where(x => x.LockoutLevel <= this.FilterLockoutLevel.Value);
            }
        }

        // Line Name (Partial match)
        if (this.FilterLineName.IsActive)
        {
            query = query.Where(x => x.LineName != null && x.LineName.Contains(this.FilterLineName.Value!));
        }

        // Resetter Name (Partial match)
        if (this.FilterResetter.IsActive)
        {
            query = query.Where(x => x.ResetBy != null && x.ResetBy.Contains(this.FilterResetter.Value!));
        }

        // Lockout Time Range
        if (this.FilterLockAfter.IsActive)
        {
            query = query.Where(x => x.LockoutTime >= this.FilterLockAfter.Value);
        }

        if (this.FilterLockBefore.IsActive)
        {
            query = query.Where(x => x.LockoutTime <= this.FilterLockBefore.Value);
        }

        // Reset Time Range
        if (this.FilterResetAfter.IsActive)
        {
            query = query.Where(x => x.ResetTime != null && x.ResetTime >= this.FilterResetAfter.Value);
        }

        if (this.FilterResetBefore.IsActive)
        {
            query = query.Where(x => x.ResetTime != null && x.ResetTime <= this.FilterResetBefore.Value);
        }

        return query;
    }

    /// <summary>
    /// Calculate the filter state hash for the hydration check, factoring in the lockout level filter direction.
    /// </summary>
    /// <param name="filterDict">The filter dictionary for which to compute the filter state hash.</param>
    /// <returns><inheritdoc/></returns>
    private protected override int GetFilterStateHash(Dictionary<string, IFilter> filterDict)
    {
        int hash = base.GetFilterStateHash(filterDict);

        if (this.FilterLockoutLevel.IsActive)
        {
            hash *= 31 + this.LockoutLevelGreaterThan.GetHashCode();
        }

        return hash;
    }

    private void ToggleCollapse() => this.isCollapsed = !this.isCollapsed;

    private async Task ExecuteAndCollapse()
    {
        await this.LoadData(updateCounts: true);
        this.ToggleCollapse();
    }

    private async Task HandleClear() => await this.ClearAllFilters();
}
