
namespace AdminInterface.Components.Pages.HomePage;
public class HomeBase : EntityManagerBase<Lockout, LockoutReset>
{
    // Filter accessors for cleaner HTML
    protected Filter<int?> FilterCmmsNum => GetFilter<int?>("cmmsNum");
    protected Filter<string?> FilterReason => GetFilter<string?>("reason");
    protected Filter<int?> FilterLockoutLevel => GetFilter<int?>("lockoutLevel");
    protected Filter<string?> FilterStatus => GetFilter<string?>("status");
    protected Filter<string?> FilterLineName => GetFilter<string?>("lineName");
    protected Filter<string?> FilterResetter => GetFilter<string?>("resetter");
    
    // Time range filters
    protected Filter<DateTime?> FilterLockAfter => GetFilter<DateTime?>("lockAfter");
    protected Filter<DateTime?> FilterLockBefore => GetFilter<DateTime?>("lockBefore");
    protected Filter<DateTime?> FilterResetAfter => GetFilter<DateTime?>("resetAfter");
    protected Filter<DateTime?> FilterResetBefore => GetFilter<DateTime?>("resetBefore");

    protected LockoutReset? target;
    protected bool isTargeting = false;
    protected bool lockoutLevelGreaterThan = true;

    /// <summary>
    /// When the app loads, fill the filter registry and default sort
    /// </summary>
    /// <returns></returns>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        CurrentSortColumn = "LockoutTime";
        SortDir = "descending";
    }

    /// <summary>
    /// Load the filter registry for searching the lockout/reset table
    /// </summary>
    protected override void InitializeFilters() {
        Filters["cmmsNum"] = new Filter<int?>("cmmsNum", null);
        Filters["reason"] = new Filter<string?>("reason", null);
        Filters["lockoutLevel"] = new Filter<int?>("lockoutLevel", null);
        Filters["status"] = new Filter<string?>("status", null);
        Filters["lineName"] = new Filter<string?>("lineName", null);
        Filters["resetter"] = new Filter<string?>("resetter", null);
        
        Filters["lockAfter"] = new Filter<DateTime?>("lockAfter", null);
        Filters["lockBefore"] = new Filter<DateTime?>("lockBefore", null);
        Filters["resetAfter"] = new Filter<DateTime?>("resetAfter", null);
        Filters["resetBefore"] = new Filter<DateTime?>("resetBefore", null);
    }

    public override int GetFilterStateHash(Dictionary<string, IFilter> filterDict)
    {
        int hash = base.GetFilterStateHash(filterDict);

        if (FilterLockoutLevel.IsActive)
            hash *= 31 + lockoutLevelGreaterThan.GetHashCode();
        return hash;
    }

    /// <summary>
    /// Apply all filters with input in the filter panel
    /// </summary>
    /// <param name="query">The query to which the filters should be applied</param>
    /// <returns>The filtered query</returns>
    protected override IQueryable<LockoutReset> ApplyFilters(IQueryable<LockoutReset> query)
    {
        // CMMS Number (Exact)
        if (FilterCmmsNum.IsActive)
            query = query.Where(x => x.CmmsNum == FilterCmmsNum.Value);

        // Status (Exact match from dropdown)
        if (FilterStatus.IsActive && !string.IsNullOrEmpty(FilterStatus.Value))
            query = query.Where(x => x.Status == FilterStatus.Value);

        // Reason (Partial match)
        if (FilterReason.IsActive)
            query = query.Where(x => x.Reason.Contains(FilterReason.Value!));

        // Lockout Level (< or >)
        if (FilterLockoutLevel.IsActive)
        {
            if(lockoutLevelGreaterThan)
                query = query.Where(x => x.LockoutLevel >= FilterLockoutLevel.Value);
            else
                query = query.Where(x => x.LockoutLevel <= FilterLockoutLevel.Value);
        }

        // Line Name (Partial match)
        if (FilterLineName.IsActive)
            query = query.Where(x => x.LineName != null && x.LineName.Contains(FilterLineName.Value!));

        // Resetter Name (Partial match)
        if (FilterResetter.IsActive)
            query = query.Where(x => x.ResetBy != null && x.ResetBy.Contains(FilterResetter.Value!));

        // Lockout Time Range
        if (FilterLockAfter.IsActive)
            query = query.Where(x => x.LockoutTime >= FilterLockAfter.Value);
        if (FilterLockBefore.IsActive)
            query = query.Where(x => x.LockoutTime <= FilterLockBefore.Value);

        // Reset Time Range
        if (FilterResetAfter.IsActive)
            query = query.Where(x => x.ResetTime != null && x.ResetTime >= FilterResetAfter.Value);
        if (FilterResetBefore.IsActive)
            query = query.Where(x => x.ResetTime != null && x.ResetTime <= FilterResetBefore.Value);

        return query;
    }

    public void HandleExpand(LockoutReset row) {
        if (row.Equals(target)){
            HandleClose();
        } else {
            target = row;
            isTargeting = true;
        }
    }

    public void HandleClose(){
        target = null;
        isTargeting = false;
    }
}