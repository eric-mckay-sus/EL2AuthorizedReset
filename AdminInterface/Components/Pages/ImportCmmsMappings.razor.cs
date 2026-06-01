// <copyright file="ImportCmmsMappings.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace AdminInterface.Components.Pages;

using BlazorBootstrap;
using Microsoft.AspNetCore.Components.Forms;

using CmmsCsvReader;
using InterProcessIO;

/// <summary>
/// Code-behind for the CMMS mapping import page.
/// </summary>
public partial class ImportCmmsMappings : IDisposable
{
    private string lastUpload = "Loading...";
    private IBrowserFile? selectedFile;
    private string? filePath;
    private bool isDragging = false;
    private bool isUploading = false;
    private string? validationError;

    private bool ConfirmationOpen => this.selectedFile != null && this.validationError == null && !this.isUploading;

    /// <summary>
    /// Gets the path of the uploads folder for this session.
    /// </summary>
    private string UploadsFolderPath { get; } = Path.Combine(Path.GetTempPath(), "uploads", Guid.NewGuid().ToString());

    /// <summary>
    /// Signature and pattern in order to implement IDisposable.
    /// Note: GC stands for garbage collector, which internally calls Dispose(false). By calling Dispose(true) here, we effectively circumvent that with the manual disposal.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// When this component unloads, unsubscribe from the I/O provider events.
    /// </summary>
    /// <param name="disposing">Whether to actually dispose. This is a help for the garbage collector.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.InputProvider.OnInputRequested -= this.HandleInputRequested;
            this.InputProvider.OnFileRequested -= this.HandleFileRequested;
            this.InputProvider.OnConfirmationRequested -= this.HandleConfirmationRequested;
        }
    }

    /// <summary>
    /// When this page loads, set the sort information, bind to the input provider, and get the last upload date.
    /// </summary>
    /// <returns>A Task representing that the page has initialized.</returns>
    protected override async Task OnInitializedAsync()
    {
        // No risk of using null-forgiving operator because the app would have already crashed without a connection
        this.PageSize = 100;
        this.SortList.Add(new ("CmmsNum", SortDir.Asc));
        this.lastUpload = await UploadCsvToDb.GetLastUpdatedDate();
        this.InputProvider.OnInputRequested += this.HandleInputRequested;
        this.InputProvider.OnFileRequested += this.HandleFileRequested;
        this.InputProvider.OnConfirmationRequested += this.HandleConfirmationRequested;
    }

    /// <summary>
    /// Validates the selected file before showing the confirmation panel.
    /// Sets _validationError and clears _selectedFile on failure so only valid files reach confirmation.
    /// </summary>
    private void HandleFileChanged(InputFileChangeEventArgs e)
    {
        this.validationError = null;
        this.selectedFile = null;

        IBrowserFile file = e.File;
        string extension = Path.GetExtension(file.Name);

        if (string.IsNullOrWhiteSpace(extension) || !extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            this.validationError = $"\"{file.Name}\" is not a CSV file. Please select a file with a .csv extension.";
            return;
        }

        // Accept the file — confirmation panel will open
        this.selectedFile = file;
    }

    /// <summary>
    /// Cancels selection by setting the file and error message to null.
    /// </summary>
    private void CancelSelection()
    {
        this.selectedFile = null;
        this.validationError = null;
    }

    /// <summary>
    /// When <see cref="EntityManagerBase{TWrite, TRead}.InputProvider"/> requests string input, show the prompt (input area shown page-side).
    /// </summary>
    /// <param name="prompt">The prompt requiring string input.</param>
    /// <param name="previousError">The error from the previous prompt, if applicable.</param>
    private void HandleInputRequested(Report prompt, string? previousError)
    {
        this.validationError = string.IsNullOrWhiteSpace(previousError) ? prompt.message : $"{previousError}\n{prompt.message}";
        this.InvokeAsync(this.StateHasChanged);
    }

    /// <summary>
    /// When <see cref="EntityManagerBase{TWrite, TRead}.InputProvider"/> requests file input, show the prompt and error message (file passing handled separately).
    /// </summary>
    /// <param name="prompt">The prompt requiring file input.</param>
    /// <param name="previousError">The error message that caused this file prompt, if applicable.</param>
    private void HandleFileRequested(Report prompt, string? previousError)
    {
        if (string.IsNullOrWhiteSpace(previousError))
        {
            this.validationError = $"{previousError}\n{prompt.message}";
        }

        this.InvokeAsync(this.StateHasChanged);
    }

    /// <summary>
    /// When <see cref="EntityManagerBase{TWrite, TRead}.InputProvider"/>, approve automatically when already uploading (page already confirmed).
    /// </summary>
    /// <param name="prompt">The prompt to be confirmed.</param>
    private void HandleConfirmationRequested(Report prompt)
    {
        _ = prompt;

        if (this.isUploading)
        {
            this.InputProvider.SetConfirmResult(true);
            return;
        }

        this.InvokeAsync(this.StateHasChanged);
    }

    /// <summary>
    /// Downloads <see cref="selectedFile"/> at <see cref="UploadsFolderPath"/> and sets it as the file task completion source using <see cref="BlazorInputProvider.SetFileResult"/>.
    /// </summary>
    /// <returns>A Task representing that the file was downloaded and passed off successfully.</returns>
    private async Task SaveSelectedFileAndSignalAsync()
    {
        if (this.selectedFile == null)
        {
            return;
        }

        Directory.CreateDirectory(this.UploadsFolderPath);
        string trustedFileName = $"cmms_mappings_{DateTime.Now:yyyy-MM-dd}";
        this.filePath = Path.Combine(this.UploadsFolderPath, trustedFileName + Path.GetExtension(this.selectedFile.Name));

        using FileStream stream = new (this.filePath, FileMode.Create);
        await this.selectedFile.OpenReadStream().CopyToAsync(stream);

        this.InputProvider.SetFileResult(this.filePath);
        this.validationError = null;
    }

    /// <summary>
    /// When a file is confirmed, download it to the server (from the browser), then upload to the DB.
    /// </summary>
    /// <returns>A Task representing that the file was confirmed and an upload was attempted.</returns>
    private async Task HandleFileConfirmation()
    {
        this.isUploading = true;
        this.validationError = null;

        try
        {
            this.Reporter.ClearLogs();
            this.Reporter.InitializeProgress(1);

            await this.SaveSelectedFileAndSignalAsync(); // actually get the file (and pass to uploader via input provider)
            UploadCsvToDb uploader = new (this.InputProvider, this.Reporter);
            UploadResult result = await uploader.ExecuteAsync(this.filePath);

            if (result == UploadResult.Complete && !this.Reporter.Logs.Any(l => l.level == ReportLevel.ERROR))
            {
                this.ToastService.Notify(new (ToastType.Success, $"\n'{this.selectedFile?.Name ?? "CSV"}' successfully uploaded."));
                this.lastUpload = await UploadCsvToDb.GetLastUpdatedDate();
            }
            else if (result == UploadResult.Canceled)
            {
                this.ToastService.Notify(new (ToastType.Secondary, "Upload canceled."));
            }
            else
            {
                this.ToastService.Notify(new (ToastType.Danger, $"Upload failed."));
            }

            await this.LoadData();
        }
        catch (Exception)
        {
            this.ToastService.Notify(new (ToastType.Danger, $"Upload failed."));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(this.filePath) && File.Exists(this.filePath))
            {
                File.Delete(this.filePath);
            }

            this.selectedFile = null;
            this.filePath = null;
            this.isUploading = false;
        }
    }

    private void HandleDragEnter() => this.isDragging = true;

    private void HandleDragOver() => this.isDragging = true;

    private void HandleDragLeave() => this.isDragging = false;

    private void HandleDrop() => this.isDragging = false;
}
