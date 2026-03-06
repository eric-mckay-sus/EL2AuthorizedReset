using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using AdminInterface.Components.Pages;

namespace AdminInterface;

/// <summary>
/// Defines the shared behavior for an admin interface page
/// </summary>
/// <typeparam name="TWrite">The datatype to insert (row from table)</typeparam>
/// <typeparam name="TRead">The datatype to show (row from view, or table again if no view)</typeparam>
public class EntityManagerBase<TWrite, TRead> : ComponentBase
    where TWrite : class, new()
    where TRead : class, new()
{
    [Inject] protected IDbContextFactory<AuthResetDbContext> DbFactory { get; set; } = default!; // The DB context

    protected TWrite NewItem = new(); // The item to be added (from the add form)
    protected List<TRead> DataView = []; // The view to READ from (type may be different from the one being written)
    protected string? ErrorMessage; // The error message for uniqueness constraint, if applicable
    protected DeleteDialog deleteDialog = default!; // The dialog to show upon pressing the delete button for a row
    protected bool IsFormVisible = false; // Whether to show or hide the add form

    /// <summary>
    /// When the page loads, prepare the table
    /// </summary>
    /// <returns></returns>
    protected override async Task OnInitializedAsync() => await LoadData();

    protected async Task LoadData()
    {
        using var context = DbFactory.CreateDbContext();
        DataView = await context.Set<TRead>().ToListAsync();
    }

    /// <summary>
    /// Throw flag to display add form, view handles the actual displaying
    /// </summary>
    protected void ShowForm() => IsFormVisible = true;
    
    /// <summary>
    /// Remove add form flag, clear input and error message
    /// </summary>
    protected void CancelForm() 
    {
        IsFormVisible = false;
        NewItem = new TWrite();
        ErrorMessage = null;
    }

    /// <summary>
    /// On submit, attempt to insert into table, and catch potential constraint violations
    /// </summary>
    /// <returns></returns>
    protected async Task HandleValidSubmit()
    {
        ErrorMessage = null;
        try
        {
            using var context = DbFactory.CreateDbContext();
            context.Set<TWrite>().Add(NewItem);
            await context.SaveChangesAsync();

            NewItem = new();
            await LoadData();
            IsFormVisible = false;
        }
        catch (DbUpdateException)
        {
            // Fallback for race conditions (form validation handled elsewhere)
            ErrorMessage = "A database error occurred. The data may have changed since you opened the form.";
        }
        catch (Exception)
        {
            ErrorMessage = "An unexpected error occurred. Please try again.";
        }
    }

    /// <summary>
    /// Assigns default behavior for removing a row from the database
    /// MUST be overridden in child if TRead is not the same type as TWrite (recommend interface between the two)
    /// </summary>
    /// <param name="context">The current DB context</param>
    /// <param name="item">The item to delete</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">When TRead is not the same type as TWrite</exception>
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
    /// Shows the delete dialog, and if confirmed, remove from underlying table in the DB (then update view)
    /// </summary>
    /// <param name="item">The item to delete from the view</param>
    /// <returns></returns>
    protected async Task HandleDelete(TRead item)
    {
        if (await deleteDialog.ConfirmAsync(item))
        {
            using var context = DbFactory.CreateDbContext();
            await ExecuteDelete(context, item);
            await LoadData();
        }
    }
}