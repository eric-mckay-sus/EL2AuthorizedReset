// <copyright file="ManageAssociates.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace AdminInterface.Components.Pages.ManagementPage;

using Microsoft.AspNetCore.Components;
using BlazorBootstrap;

/// <summary>
/// Code-behind for the associate management page.
/// </summary>
public partial class ManageAssociates
{
    private string _searchText = string.Empty;
    private bool _searchingInt => int.TryParse(_searchText, out _);
    private bool _blazorStop = true;
    [Parameter] public EventCallback<Associate> HandleExpand { get; set; }
    [Parameter] public Associate? target { get; set; }

    /// <summary>
    /// Skip LoadData if there's no change that requires a DB hit
    /// </summary>
    /// <returns></returns>
    protected override async Task OnParametersSetAsync()
    {
        if (TotalCount == 0)
        {
            await base.OnParametersSetAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_blazorStop)
        {
            _blazorStop = true;
            // The UI has rendered the new page results. Now it's safe to close.
            await OnItemChanged.InvokeAsync(target);
        }
    }

    protected override void OnInitialized()
    {
        CurrentSortColumn="AssociateNum";
        SortDir="ascending";
    }

    /// <summary>
    /// If the user leaves the search bar and it is empty,
    /// reload the data to show the full list.
    /// </summary>
    private async Task HandleBlur()
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            // In this state, the user intent is the same as ClearSearchBar.
            await LoadData();
        }
    }

    /// <summary>
    /// Override for table refresh that verifies the targeted associate is still in the dataview.
    /// If it isn't, tell the parent to hide the associated lines
    /// </summary>
    /// <returns></returns>
    public override async Task LoadData(bool keepPage=false, bool updateCounts=false)
    {
        await base.LoadData(keepPage, updateCounts);

        if (target != null && !DataView.Contains(target))
        {
            _blazorStop = false;
        }
    }

    /// <summary>
    /// Applies the search bar contents as a query filter
    /// Numbers are inferred to be associate/badge number, anything else is substring containment for name
    /// </summary>
    /// <param name="query">The query to which the filter should be applied</param>
    /// <returns>The query where results match the search criterion</returns>
    protected override IQueryable<Associate> ApplyFilters(IQueryable<Associate> query)
    {
        if(int.TryParse(_searchText, out int numeric)){
            query = query.Where(a => a.AssociateNum == numeric || a.BadgeNum == numeric);
        } else {
            query = query.Where(a => a.Name != null && a.Name.Contains(_searchText));
        }
        return query;
    }

    /// <summary>
    /// Clear the search bar, then reload the table for the empty query
    /// </summary>
    /// <returns></returns>
    private async Task ClearSearchBar(){
        _searchText = "";
        await LoadData();
    }

    protected override void ShowSuccessToast()
    {
        ToastService.Notify(new(ToastType.Success, $"{NewItem.Name} was added to the database"));
    }

    protected override async Task ExecuteDelete(AuthResetDbContext context, Associate item)
    {
        await base.ExecuteDelete(context, item);
        ToastService.Notify(new(ToastType.Danger, $"Removed {item.Name} and all their privileges"));
    }
}
