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
    private string searchText = string.Empty;

    private bool blazorStop = true;

    /// <summary>
    /// Gets or sets the action to take when an associate is expanded.
    /// </summary>
    [Parameter]
    public EventCallback<Associate> HandleExpand { get; set; }

    /// <summary>
    /// Gets or sets the targeted associate.
    /// </summary>
    [Parameter]
    public Associate? Target { get; set; }

    /// <summary>
    /// Gets a value indicating whether the user is searching an integer (thereby is searching for an associate number).
    /// </summary>
    private bool SearchingInt => int.TryParse(this.searchText, out _);

    /// <summary>
    /// Override for table refresh that verifies the targeted associate is still in the dataview.
    /// If it isn't, tell the parent to hide the associated lines.
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
    /// Skip LoadData if there's no change that requires a DB hit.
    /// </summary>
    /// <returns>A Task representing that the conditional DB hit is complete.</returns>
    protected override async Task OnParametersSetAsync()
    {
        if (this.TotalCount == 0)
        {
            await base.OnParametersSetAsync();
        }
    }

    /// <summary>
    /// When rendering the page, detect if <see cref="Target"/> may now be absent.
    /// </summary>
    /// <param name="firstRender"><inheritdoc path="/param[@name='firstRender']"/></param>
    /// <returns>A Task representing that post-render operations have completed.</returns>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!this.blazorStop)
        {
            this.blazorStop = true;

            // The UI has rendered the new page results, so it's now safe to close.
            await this.OnItemChanged.InvokeAsync(this.Target);
        }
    }

    /// <summary>
    /// When this component is initialized, set the default sort.
    /// </summary>
    protected override void OnInitialized() => this.SortList.Add(new ("AssociateNum", SortDir.Asc));

    /// <summary>
    /// Applies the search bar contents as a query filter
    /// Numbers are inferred to be associate/badge number, anything else is substring containment for name.
    /// </summary>
    /// <param name="query">The query to which the filter should be applied.</param>
    /// <returns>The query where results match the search criterion.</returns>
    protected override IQueryable<Associate> ApplyFilters(IQueryable<Associate> query)
    {
        if (int.TryParse(this.searchText, out int numeric))
        {
            query = query.Where(a => a.AssociateNum == numeric || a.BadgeNum == numeric);
        }
        else
        {
            query = query.Where(a => a.Name != null && a.Name.Contains(this.searchText));
        }

        return query;
    }

    /// <summary>
    /// Shows the success toast for when <see cref="EntityManagerBase{TWrite, TRead}.NewItem"/> is added.
    /// </summary>
    protected override void ShowSuccessToast()
    {
        this.ToastService.Notify(new (ToastType.Success, $"{this.NewItem.Name} was added to the database"));
    }

    /// <summary>
    /// Associate deletion logic: removes <paramref name="item"/> from the <paramref name="context"/> and shows the deletion toast.
    /// </summary>
    /// <param name="context"><inheritdoc path="/param[@name='context']"/></param>
    /// <param name="item">The <see cref="Associate"/> to be deleted.</param>
    /// <returns><inheritdoc/></returns>
    protected override async Task ExecuteDelete(AuthResetDbContext context, Associate item)
    {
        await base.ExecuteDelete(context, item);
        this.ToastService.Notify(new (ToastType.Danger, $"Removed {item.Name} and all their privileges"));
    }

    /// <summary>
    /// Clear the search bar, then reload the table for the empty query.
    /// </summary>
    /// <returns>A Task representing that the search bar has been cleared and the table has been reset.</returns>
    private async Task ClearSearchBar()
    {
        this.searchText = string.Empty;
        await this.LoadData();
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
}
