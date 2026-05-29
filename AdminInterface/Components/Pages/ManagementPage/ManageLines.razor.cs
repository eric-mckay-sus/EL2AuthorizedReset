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
    [Parameter, EditorRequired] public Associate targetAssociate { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Inject] private IMemoryCache cache { get; set; } = default!;
    private AssocNameLine? targetPermission { get; set; }
    private List<string> allAvailableLines = [];
    private bool isEditing = false;
    private string searchText = "";

    /// <summary>
    /// When the page is initialized, create a cache of all available lines for the autofill.
    /// </summary>
    /// <remarks>
    /// Strictly speaking, the database guarantees line name to be non-null, so the null-handling here is overkill.
    /// But it quiets the compiler and shouldn't affect performance, so I see this as a win.
    /// </remarks>
    /// <returns></returns>
    protected override async Task OnInitializedAsync()
    {
        allAvailableLines = await cache.GetOrCreateAsync("AllLinesList", async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            using AuthResetDbContext context = DbFactory.CreateDbContext();
            return await context.CmmsToLineName
                .Select(x => x.LineName ?? string.Empty) // Coalesce nulls to empty
                .Where(x => x != string.Empty) // then filter out empty entirely (effectively handling null and empty simultaneously)
                .Distinct()
                .ToListAsync();
        }) ?? [];
    }

    protected override void OnInitialized()
    {
        CurrentSortColumn="Line";
        SortDir="ascending";
        PageSize=10;
    }

    /// <summary>
    /// Whenever the target associate changes, prime the add form object and force a refresh
    /// </summary>
    /// <returns></returns>
    protected override async Task OnParametersSetAsync(){
        if (targetAssociate == null)
        {
            DataView.Clear();
            return;
        }
        ResetNewItem();
        await base.OnParametersSetAsync();
    }

    /// <summary>
    /// If the user leaves the search bar and it is empty,
    /// reload the data to show the full list.
    /// </summary>
    private async Task HandleBlur()
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            // In this state, the user intent is the same as ClearSearchBar.
            await LoadData();
        }
    }

    /// <summary>
    /// Creates a new object for the add form with the target associate number
    /// </summary>
    private void ResetNewItem(){
        targetPermission = null;
        NewItem = new AssociateLine();
        NewItem.IsNewRecord = true;
        if (targetAssociate != null){
            NewItem.AssocNum = targetAssociate.AssociateNum;
        }
    }

    /// <summary>
    /// Filters the DataView to only show results for the targeted associate
    /// </summary>
    /// <param name="query">The query to which the filter should be applied</param>
    /// <returns>An IQueryable with only table entries matching the target associate</returns>
    protected override IQueryable<AssocNameLine> ApplyFilters(IQueryable<AssocNameLine> query)
    {
        if (targetAssociate == null) return query.Where(x => false); // Return empty if no associate selected

        // Filter by line name
        query = query.Where(a => a.Line != null && a.Line.Contains(searchText));

        // Filter the view by the selected associate number
        return query.Where(x => x.AssocNum == targetAssociate.AssociateNum);
    }

    private void HandleExpand(AssocNameLine anl){
        if (IsFormVisible && isEditing){
            CloseForm();
            return;
        }
        // Map the read-only view item to the writeable model
        NewItem = new AssociateLine {
            AssocNum = anl.AssocNum,
            Line = anl.Line,
            AuthLevel = anl.AuthLevel,
            IsNewRecord=false // bypass PK validation
        };
        targetPermission = anl;
        isEditing = true;
        IsFormVisible = true;
        StateHasChanged();
    }

    protected override void CloseForm(){
        isEditing = false;
        base.CloseForm();
        ResetNewItem();
    }

    /// <summary>
    /// Gets the AssociateLine object referred to by the input AssocNameLine and removes it from the DB
    /// </summary>
    /// <param name="context">The DB context to use for the check, removal, and save</param>
    /// <param name="item">The AssocNameLine instance for which to delete the associated data</param>
    /// <returns></returns>
    protected override async Task ExecuteDelete(AuthResetDbContext context, AssocNameLine item)
    {
        var toDelete = await context.AssociateToLine
            .FirstOrDefaultAsync(x => x.AssocNum == item.AssocNum && x.Line == item.Line);

        if (toDelete != null)
        {
            context.AssociateToLine.Remove(toDelete);
            await context.SaveChangesAsync();
        }
        toastService.Notify(new(ToastType.Danger, $"Removed {item.Line} from {item.AssocName}'s privileges."));
    }

    /// <summary>
    /// Because the user doesn't provide the associate number in the EditForm component,
    /// we need to override the submitter to re-instantiate the object and force update the view so Blazor doesn't take shortcuts
    /// as well as provide new logic for submitting an edit
    /// </summary>
    /// <returns></returns>
    protected override async Task HandleValidSubmit()
    {
        ErrorMessage = null;
        try
        {
            using var context = DbFactory.CreateDbContext();

            if (isEditing) // If coming from an edit, use UPDATE
                context.AssociateToLine.Update(NewItem);
            else // If coming from an add, use INSERT
                context.AssociateToLine.Add(NewItem);

            await context.SaveChangesAsync();

            var info = await context.Set<AssocNameLine>().FindAsync(NewItem.AssocNum, NewItem.Line);
            string displayName = info?.AssocName ?? $"Associate {NewItem.AssocNum}";

            string message = $"Saved {displayName}'s privilege on {NewItem.Line}";
            if(isEditing){
                message = $"Updated {displayName}'s auth level on {NewItem.Line} to {NewItem.AuthLevel}";
            }
            else{
                message = $"Added {NewItem.Line} to the authorized reset list for {displayName}";
            }
            toastService.Notify(new(ToastType.Success, message));

            // Cleanup state
            isEditing = false;
            IsFormVisible = false;
            ResetNewItem(); // Clears inputs for the next action
            await LoadData();
        }
        catch (Exception)
        {
            ErrorMessage = "Could not save changes. Ensure the line isn't already authorized.";
        }
    }

    /// <summary>
    /// Clear the search bar, then reload the table for the empty query
    /// </summary>
    /// <returns></returns>
    protected async Task ClearSearchBar(){
        searchText = "";
        await LoadData();
    }
}
