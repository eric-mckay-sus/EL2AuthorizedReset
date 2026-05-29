// <copyright file="ImportCmmsMappings.razor.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace AdminInterface.Components.Pages;

using BlazorBootstrap;
using Microsoft.AspNetCore.Components.Forms;

using CmmsCsvReader;
using InterProcessIO;

public partial class ImportCmmsMappings : IDisposable
{
    private string? _uploadStatus;
    private string _lastUpload = "Loading...";
    private IBrowserFile? _selectedFile;
    private string? _filePath;
    private bool _isDragging = false;
    private bool _isUploading = false;
    private string? _validationError;
    private bool _confirmationOpen => _selectedFile != null && _validationError == null && !_isUploading;

    private void HandleDragEnter() => _isDragging = true;
    private void HandleDragOver() => _isDragging = true;
    private void HandleDragLeave() => _isDragging = false;
    private void HandleDrop() => _isDragging = false;

    /// <summary>
    /// Gets the path of the uploads folder for this session.
    /// </summary>
    protected string UploadsFolderPath { get; } = Path.Combine(Path.GetTempPath(), "uploads", Guid.NewGuid().ToString());

    /// <summary>
    /// When this page loads, set up the connection string and get the last upload date
    /// </summary>
    /// <returns></returns>
    protected override async Task OnInitializedAsync()
    {
        // No risk of using null-forgiving operator because the app would have already crashed without a connection
        PageSize = 100;
        CurrentSortColumn = "CmmsNum";
        SortDir = "ascending";
        _lastUpload = await UploadCsvToDb.GetLastUpdatedDate();
        InputProvider.OnInputRequested += this.HandleInputRequested;
        InputProvider.OnFileRequested += this.HandleFileRequested;
        InputProvider.OnConfirmationRequested += this.HandleConfirmationRequested;
    }

    /// <summary>
    /// Validates the selected file before showing the confirmation panel.
    /// Sets _validationError and clears _selectedFile on failure so only valid files reach confirmation.
    /// </summary>
    private void HandleFileChanged(InputFileChangeEventArgs e)
    {
        _validationError = null;
        _selectedFile = null;

        IBrowserFile file = e.File;
        string extension = Path.GetExtension(file.Name);

        if (string.IsNullOrWhiteSpace(extension) || !extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            _validationError = $"\"{file.Name}\" is not a CSV file. Please select a file with a .csv extension.";
            return;
        }

        // Accept the file — confirmation panel will open
        _selectedFile = file;
    }

    /// <summary>
    /// Cancels selection by setting the file and error message to null.
    /// </summary>
    private void CancelSelection()
    {
        _selectedFile = null;
        _validationError = null;
    }

    /// <summary>
    /// When <see cref="EntityManagerBase{TWrite, TRead}.InputProvider"/> requests string input, show the prompt (input area shown page-side)
    /// </summary>
    /// <param name="prompt">The prompt requiring string input.</param>
    /// <param name="previousError">The error from the previous prompt, if applicable.</param>
    private void HandleInputRequested(Report prompt, string? previousError)
    {
        _validationError = string.IsNullOrWhiteSpace(previousError) ? prompt.message : $"{previousError}\n{prompt.message}";
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// When <see cref="EntityManagerBase{TWrite, TRead}.InputProvider"/> requests file input, show the prompt and error message (file passing handled separately)
    /// </summary>
    /// <param name="prompt">The prompt requiring file input.</param>
    /// <param name="previousError">The error message that caused this file prompt, if applicable.</param>
    private void HandleFileRequested(Report prompt, string? previousError)
    {
        if (string.IsNullOrWhiteSpace(previousError))
        {
            _validationError = $"{previousError}\n{prompt.message}";
        }
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// When <see cref="EntityManagerBase{TWrite, TRead}.InputProvider"/>, approve automatically when already uploading (page already confirmed).
    /// </summary>
    /// <param name="prompt">The prompt to be confirmed.</param>
    private void HandleConfirmationRequested(Report prompt)
    {
        if (_isUploading)
        {
            InputProvider.SetConfirmResult(true);
            return;
        }
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Downloads <see cref="_selectedFile"/> at <see cref="UploadsFolderPath"/> and sets it as the file task completion source using <see cref="BlazorInputProvider.SetFileResult"/>
    /// </summary>
    /// <returns>A Task representing that the file was downloaded and passed off successfully.</returns>
    private async Task SaveSelectedFileAndSignalAsync()
    {
        if (_selectedFile == null)
        {
            return;
        }

        Directory.CreateDirectory(UploadsFolderPath);
        string trustedFileName = $"cmms_mappings_{DateTime.Now:yyyy-MM-dd}";
        _filePath = Path.Combine(UploadsFolderPath, trustedFileName + Path.GetExtension(_selectedFile.Name));

        using FileStream stream = new(_filePath, FileMode.Create);
        await _selectedFile.OpenReadStream().CopyToAsync(stream);

        InputProvider.SetFileResult(_filePath);
        _validationError = null;
    }

    /// <summary>
    /// When a file is confirmed, download it to the server (from the browser), then upload to the DB
    /// </summary>
    /// <returns>A Task representing that the file was confirmed and an upload was attempted.</returns>
    private async Task HandleFileConfirmation()
    {
        _isUploading = true;
        _uploadStatus = "Uploading...";
        _validationError = null;

        try
        {
            Reporter.ClearLogs();
            Reporter.InitializeProgress(1);

            await this.SaveSelectedFileAndSignalAsync(); // actually get the file (and pass to uploader via input provider)
            UploadCsvToDb uploader = new(InputProvider, Reporter);
            UploadResult result = await uploader.ExecuteAsync(_filePath);

            if (result == UploadResult.Complete && !Reporter.Logs.Any(l => l.level == ReportLevel.ERROR))
            {
                ToastService.Notify(new(ToastType.Success, $"\n'{_selectedFile?.Name ?? "CSV"}' successfully uploaded."));
                _lastUpload = await UploadCsvToDb.GetLastUpdatedDate();
            }
            else if (result == UploadResult.Canceled)
            {
                ToastService.Notify(new(ToastType.Secondary, "Upload canceled."));
            }
            else
            {
                ToastService.Notify(new (ToastType.Danger, $"Upload failed."));
            }
            await LoadData();
        }
        catch (Exception ex)
        {
            _uploadStatus += $"\nUpload failed: {ex.Message}";
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(_filePath) && File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }

            _selectedFile = null;
            _filePath = null;
            _isUploading = false;
        }
    }

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
    public void Dispose(bool disposing){
        if (disposing)
        {
            InputProvider.OnInputRequested -= this.HandleInputRequested;
            InputProvider.OnFileRequested -= this.HandleFileRequested;
            InputProvider.OnConfirmationRequested -= this.HandleConfirmationRequested;
        }
    }
}
