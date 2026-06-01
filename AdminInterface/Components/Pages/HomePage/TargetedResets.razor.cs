// <copyright file="TargetedResets.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace AdminInterface.Components.Pages.HomePage;

using Microsoft.AspNetCore.Components;

/// <summary>
/// The code-behind for the reset lookup sub-page.
/// </summary>
public partial class TargetedResets
{
    /// <summary>
    /// Gets or sets the targeted lockout-reset.
    /// </summary>
    [Parameter]
    public LockoutReset? Target { get; set; }

    /// <summary>
    /// Gets or sets the action to take when the signal is sent for this component to be closed.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    private Filter<bool?> FilterDenialType => this.GetFilter<bool?>("denialType");

    /// <summary>
    /// When this component loads, create a filter for denial type and set the default sort.
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        this.CurrentSortColumn = "IsAuthorized";
        this.SortDir = "descending";
    }

    /// <summary>
    /// The only filter this component is responsible for is denial type.
    /// </summary>
    protected override void InitializeFilters()
    {
        this.Filters["denialType"] = new Filter<bool?>("denialType", null);
    }

    /// <summary>
    /// Ensures that only resets targeting the selected lockout are shown.
    /// </summary>
    /// <param name="query"><inheritdoc path="/param[@name='query']"/></param>
    /// <returns><inheritdoc/></returns>
    protected override IQueryable<Reset> ApplyFilters(IQueryable<Reset> query)
    {
        if (this.Target == null)
        {
            return query.Where(x => false); // Return empty if no associate selected
        }

        // Filter the view by the selected lockout first to limit results
        query = query.Where(x => x.LockoutId == this.Target.LockoutId);

        // Apply the denial type filter if applicable
        if (this.FilterDenialType.IsActive && this.FilterDenialType.Value.HasValue)
        {
            query = query.Where(x => !x.IsAuthorized); // when filtering by denial type, ignore authorized row(s)

            // true represents insufficent auth level
            if (this.FilterDenialType.Value == true)
            {
                query = query.Where(x => x.AuthLevel < this.Target.LockoutLevel);
            }

            // false represents not authorized for the line at all
            else
            {
                query = query.Where(x => x.AuthLevel == null);
            }
        }

        return query;
    }

    /// <summary>
    /// Helper method to update filter and view on dropdown change.
    /// </summary>
    /// <param name="e">The change event thrown by the dropdown.</param>
    /// <returns>A Task representing that the denial type has been changed successfully.</returns>
    private async Task ChangeDenialType(ChangeEventArgs e)
    {
        string val = e.Value?.ToString() ?? string.Empty;
        this.FilterDenialType.Value = string.IsNullOrWhiteSpace(val) ? null : bool.Parse(val);
        await this.LoadData();
    }
}
