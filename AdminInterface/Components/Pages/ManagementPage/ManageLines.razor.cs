// <copyright file="ManageLines.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace AdminInterface.Components.Pages.ManagementPage;

using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Code-behind for the line management page.
/// </summary>
public partial class ManageLines
{
    private AssocNameLine? targetPermission;
    private List<string> allAvailableLines = [];
    private bool isEditing = false;
    private string searchText = string.Empty;

    /// <summary>
    /// Gets or sets the associate focus for this sub-page.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Associate TargetAssociate { get; set; }

    /// <summary>
    /// Gets or sets the action on the signal to close this page.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    [Inject]
    private IMemoryCache Cache { get; set; } = default!;

    /// <summary>
    /// When the page is initialized, create a cache of all available lines for the autofill.
    /// </summary>
    /// <remarks>
    /// Strictly speaking, the database guarantees line name to be non-null, so the null-handling here is overkill.
    /// But it quiets the compiler and shouldn't affect performance, so I see this as a win.
    /// </remarks>
    /// <returns>A Task repreesnting that the page has been initialized.</returns>
    protected override async Task OnInitializedAsync()
    {
        this.allAvailableLines = await this.Cache.GetOrCreateAsync("AllLinesList", async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            using AuthResetDbContext context = this.DbFactory.CreateDbContext();
            return await context.CmmsToLineName
                .Select(x => x.LineName ?? string.Empty) // Coalesce nulls to empty
                .Where(x => x != string.Empty) // then filter out empty entirely (effectively handling null and empty simultaneously)
                .Distinct()
                .ToListAsync();
        }) ?? [];
    }

    /// <summary>
    /// When the page is initialized, set the initial sort and pagination information.
    /// </summary>
    protected override void OnInitialized()
    {
        this.CurrentSortColumn = "Line";
        this.SortDir = "ascending";
        this.PageSize = 10;
    }

    /// <summary>
    /// Whenever the target associate changes, prime the add form object and force a refresh.
    /// </summary>
    /// <returns>A Task representing that the form is ready for adding lines to the new associate.</returns>
    protected override async Task OnParametersSetAsync()
    {
        if (this.TargetAssociate == null)
        {
            this.DataView.Clear();
            return;
        }

        this.ResetNewItem();
        await base.OnParametersSetAsync();
    }

    /// <summary>
    /// Filters the DataView to only show results for the targeted associate.
    /// </summary>
    /// <param name="query">The query to which the filter should be applied.</param>
    /// <returns>An IQueryable with only table entries matching the target associate.</returns>
    protected override IQueryable<AssocNameLine> ApplyFilters(IQueryable<AssocNameLine> query)
    {
        if (this.TargetAssociate == null)
        {
            return query.Where(x => false); // Return empty if no associate selected
        }

        // Filter by line name
        query = query.Where(a => a.Line != null && a.Line.Contains(this.searchText));

        // Filter the view by the selected associate number
        return query.Where(x => x.AssocNum == this.TargetAssociate.AssociateNum);
    }

    /// <summary>
    /// Closes the add/edit form.
    /// </summary>
    protected override void CloseForm()
    {
        this.isEditing = false;
        base.CloseForm();
        this.ResetNewItem();
    }

    /// <summary>
    /// Gets the AssociateLine object referred to by the input AssocNameLine and removes it from the DB.
    /// </summary>
    /// <param name="context">The DB context to use for the check, removal, and save.</param>
    /// <param name="item">The AssocNameLine instance for which to delete the associated data.</param>
    /// <returns>A Task representing that the target associate's permissions for the target line have been removed.</returns>
    protected override async Task ExecuteDelete(AuthResetDbContext context, AssocNameLine item)
    {
        AssociateLine? toDelete = await context.AssociateToLine
            .FirstOrDefaultAsync(x => x.AssocNum == item.AssocNum && x.Line == item.Line);

        if (toDelete != null)
        {
            context.AssociateToLine.Remove(toDelete);
            await context.SaveChangesAsync();
        }

        this.toastService.Notify(new (ToastType.Danger, $"Removed {item.Line} from {item.AssocName}'s privileges."));
    }

    /// <summary>
    /// Because the user doesn't provide the associate number in the EditForm component,
    /// we need to override the submitter to re-instantiate the object and force update the view so Blazor doesn't take shortcuts
    /// as well as provide new logic for submitting an edit.
    /// </summary>
    /// <returns>A Task representing that the submission has been handled.</returns>
    protected override async Task HandleValidSubmit()
    {
        this.ErrorMessage = null;
        try
        {
            using AuthResetDbContext context = this.DbFactory.CreateDbContext();

            // If coming from an edit, use UPDATE
            if (this.isEditing)
            {
                context.AssociateToLine.Update(this.NewItem);
            }

            // If coming from an add, use INSERT
            else
            {
                context.AssociateToLine.Add(this.NewItem);
            }

            await context.SaveChangesAsync();

            AssocNameLine? info = await context.Set<AssocNameLine>().FindAsync(this.NewItem.AssocNum, this.NewItem.Line);
            string displayName = info?.AssocName ?? $"Associate #{this.NewItem.AssocNum}";

            string message;
            if (this.isEditing)
            {
                message = $"Updated {displayName}'s auth level on {this.NewItem.Line} to {this.NewItem.AuthLevel}";
            }
            else
            {
                message = $"Added {this.NewItem.Line} to the authorized reset list for {displayName}";
            }

            this.toastService.Notify(new (ToastType.Success, message));

            // Cleanup state
            this.isEditing = false;
            this.IsFormVisible = false;
            this.ResetNewItem(); // Clears inputs for the next action
            await this.LoadData();
        }
        catch (Exception)
        {
            this.ErrorMessage = "Could not save changes. Ensure the line isn't already authorized.";
        }
    }

    /// <summary>
    /// Clear the search bar, then reload the table for the empty query.
    /// </summary>
    /// <returns>A Task representing that the search bar and table are reset.</returns>
    protected async Task ClearSearchBar()
    {
        this.searchText = string.Empty;
        await this.LoadData();
    }

    private void HandleExpand(AssocNameLine anl)
    {
        if (this.IsFormVisible && this.isEditing)
        {
            this.CloseForm();
            return;
        }

        // Map the read-only view item to the writeable model
        this.NewItem = new AssociateLine
        {
            AssocNum = anl.AssocNum,
            Line = anl.Line,
            AuthLevel = anl.AuthLevel,
            IsNewRecord = false, // bypass PK validation
        };
        this.targetPermission = anl;
        this.isEditing = true;
        this.IsFormVisible = true;
        this.StateHasChanged();
    }

    /// <summary>
    /// If the user leaves the search bar and it is empty,
    /// reload the data to show the full list.
    /// </summary>
    private async Task HandleBlur()
    {
        if (string.IsNullOrWhiteSpace(this.searchText))
        {
            // In this state, the user intent is the same as ClearSearchBar.
            await this.LoadData();
        }
    }

    /// <summary>
    /// Creates a new object for the add form with the target associate number.
    /// </summary>
    private void ResetNewItem()
    {
        this.targetPermission = null;
        this.NewItem = new AssociateLine { IsNewRecord = true };
        if (this.TargetAssociate != null)
        {
            this.NewItem.AssocNum = this.TargetAssociate.AssociateNum;
        }
    }
}
