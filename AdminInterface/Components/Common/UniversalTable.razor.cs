// <copyright file="UniversalTable.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace AdminInterface.Components.Common;

using Microsoft.AspNetCore.Components;
using System.Reflection;

/// <summary>
/// Defines the methods and state necessary to display the contents of <see cref="EntityManagerBase{TRead, TWrite}"/>.
/// </summary>
/// <typeparam name="T">The class defining one record in the table.</typeparam>
public partial class UniversalTable<T>
    where T : class
{
    /// <summary>
    /// A cache of the properties of <typeparamref name="T"/> so it's not invoked for each header, excluding those marked NotDisplayed.
    /// </summary>
    private readonly PropertyInfo[] cachedProps = typeof(T).GetProperties().Where(p => p.GetCustomAttribute<NotDisplayedAttribute>() == default).ToArray();

    /// <summary>
    /// Allows the user to highlight a row for focus (no information shown).
    /// </summary>
    private T? attentionItem;

    /// <summary>
    /// Binds to the text field "jump to".
    /// </summary>
    private string jumpPage = string.Empty;

    /// <summary>
    /// Gets or sets the data to display.
    /// </summary>
    [Parameter]
    public IEnumerable<T>? Items { get; set; }

    /// <summary>
    /// Gets or sets the action to bind to the delete button being pressed.
    /// </summary>
    [Parameter]
    public EventCallback<T> OnDelete { get; set; }

    /// <summary>
    /// Gets or sets the action to bind to the expand button being pressed.
    /// </summary>
    [Parameter]
    public EventCallback<T> OnExpand { get; set; }

    /// <summary>
    /// Gets or sets the row to highlight (because its information is shown).
    /// </summary>
    [Parameter]
    public T? Target { get; set; }

    /// <summary>
    /// Gets or sets the style to apply to <see cref="Target"/>.
    /// </summary>
    [Parameter]
    public string? TargetStyle { get; set; }

    // Sorting & staleness

    /// <summary>
    /// Gets or sets the action to perform when a sort column header is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnSort { get; set; }

    /// <summary>
    /// Gets or sets the method used to fetch the sort icon for a target column.
    /// </summary>
    [Parameter]
    public Func<string, string> GetSortIcon { get; set; } = (col) => string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to use the 'stale' style.
    /// </summary>
    [Parameter]
    public bool IsStale { get; set; } = false;

    // Pagination Params

    /// <summary>
    /// Gets or sets the current page number (must be between 1 and <see cref="TotalPages"/>, inclusive).
    /// </summary>
    [Parameter]
    public int CurrentPage { get; set; }

    /// <summary>
    /// Gets or sets the page count.
    /// </summary>
    [Parameter]
    public int TotalPages { get; set; }

    /// <summary>
    /// Gets or sets the total number of records retrieved.
    /// </summary>
    [Parameter]
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    [Parameter]
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the action to bind to page change.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnPageChange { get; set; }

    /// <summary>
    /// Gets or sets the action to bind to page size change.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnPageSizeChange { get; set; }

    // Denotes range of records shown

    /// <summary>
    /// Gets the (ordinal) number of the first record currently in <see cref="Items"/>.
    /// </summary>
    private int StartRecord => ((this.CurrentPage - 1) * this.PageSize) + 1;

    /// <summary>
    /// Gets the (ordinal) number of the last record currently in <see cref="Items"/>.
    /// </summary>
    private int EndRecord => Math.Min(this.CurrentPage * this.PageSize, this.TotalCount);

    private string GetRowClass(T item)
    {
        if (item.Equals(default))
        {
            return string.Empty;
        }

        // Priority 1: the row being targeted
        if (item.Equals(this.Target))
        {
            return this.TargetStyle ?? "table-primary";
        }

        // Priority 2: the row the user clicked to "watch"
        if (item.Equals(this.attentionItem))
        {
            return "table-active cursor-pointer";
        }

        return "cursor-pointer";
    }

    private async Task HandleJumpPage()
    {
        if (int.TryParse(this.jumpPage, out int targetPage))
        {
            // Clamp the value between 1 and TotalPages
            targetPage = Math.Max(1, Math.Min(targetPage, this.TotalPages));
            this.jumpPage = targetPage.ToString(); // Update UI to show clamped value
            await this.OnPageChange.InvokeAsync(targetPage);
        }
    }

    private string SortIcon(string col) => this.GetSortIcon?.Invoke(col) ?? string.Empty;
}
