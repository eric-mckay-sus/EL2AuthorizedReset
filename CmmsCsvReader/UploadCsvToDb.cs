// <copyright file="UploadCsvToDb.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace CmmsCsvReader;

using Microsoft.Data.SqlClient;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Data;

using InterProcessIO;

/// <summary>
/// Represents a line's essential information as it appears in the CSV.
/// </summary>
public class Line
{
    /// <summary>
    /// Gets or sets the machine's CMMS number.
    /// </summary>
    public int CmmsNum { get; set; }

    /// <summary>
    /// Gets or sets the machine's line name.
    /// </summary>
    public required string Name { get; set; }
}

/// <summary>
/// A <see cref="ClassMap"/> for the <see cref="Line"/> class.
/// </summary>
public sealed class LineMap : ClassMap<Line>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LineMap"/> class.
    /// Maps column names as they appear in the CSV to field names in the <see cref="Line"/> object.
    /// </summary>
    public LineMap()
    {
        this.Map(m => m.CmmsNum).Name("CMMS #");
        this.Map(m => m.Name).Name("Location");
    }
}

/// <summary>
/// A CSV parser to get the current mappings of CMMS numbers to line names.
/// Upon successful parsing, replaces the current dataset in the DB.
/// </summary>
public class UploadCsvToDb
{
    /// <summary>
    /// Determines where user input comes from.
    /// </summary>
    private readonly IInputProvider input;

    /// <summary>
    /// Determines where/how program output is displayed.
    /// </summary>
    private readonly IOutputProvider output;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadCsvToDb"/> class.
    /// By default, uses the console for input and output.
    /// </summary>
    public UploadCsvToDb()
    {
        this.input = new ConsoleInputProvider();
        this.output = new ConsoleReporter();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadCsvToDb"/> class, using the specified input and output providers.
    /// </summary>
    /// <param name="inputProvider">The instance of IInputProvider to be used to get input regarding model mapping details.</param>
    /// <param name="outputProvider">The instance of IReportOutputProvider to be used for displaying program results.</param>
    public UploadCsvToDb(IInputProvider inputProvider, IOutputProvider outputProvider)
    {
        this.input = inputProvider;
        this.output = outputProvider;
    }

    /// <summary>
    /// Entry point for the program. Parses the entire file for mappings and adds them all to the database.
    /// </summary>
    /// <param name="args">The file to parse (must be a CSV of the correct format).</param>
    /// <returns>A Task representing the completion of this program.</returns>
    public static async Task Main(string[] args)
    {
        // If there was an input location argument, pass it along (no validation here)
        string? potentialFile = null;
        if (args.Length > 0)
        {
            potentialFile = args[0];
        }

        // Exit static by creating an uploader
        UploadCsvToDb uploader = new ();

        // Then give it the green light
        await uploader.ExecuteAsync(potentialFile);
    }

    /// <summary>
    /// Gets the date this table was last updated (from the extended metadata).
    /// </summary>
    /// <returns>A Task holding the date that the CMMS-line mappings were last updated.</returns>
    public static async Task<string> GetLastUpdatedDate()
    {
        const string sql = @"
            SELECT CAST(value AS NVARCHAR(MAX)) AS Value
            FROM sys.fn_listextendedproperty(N'dateLastUpdated', N'SCHEMA', N'dbo', N'TABLE', N'cmmsToLineName', default, default)";

        try
        {
            using SqlConnection connection = new (Config.GetConnectionString());
            using SqlCommand command = new (sql, connection);

            await connection.OpenAsync();

            // ExecuteScalar is most efficient here since we only expect one row and one column
            object? result = await command.ExecuteScalarAsync();

            return result?.ToString() ?? "No upload history found.";
        }
        catch (Exception)
        {
            return "Error retrieving last upload date.";
        }
    }

    /// <summary>
    /// Designated entry point for outside projects. Parses the entire file for mappings and adds them all to the database.
    /// </summary>
    /// <param name="filename">The file to parse (must be a CSV of the correct format).</param>
    /// <returns>A Task representing that the model mappings have been updated.</returns>
    public async Task<UploadResult> ExecuteAsync(string? filename = null)
    {
        this.output.ClearLogs();
        string? potentialFilePath = null;
        string filePath = string.Empty;
        string? validationError = null;

        while (string.IsNullOrEmpty(filePath))
        {
            potentialFilePath = await this.input.GetFileAsync(new ("Please select the file(s) you wish to upload."), validationError);
            if (potentialFilePath == null)
            {
                validationError = $"No file specified. Please try again.";
            }
            else if (!Path.Exists(potentialFilePath))
            {
                validationError = $"Path '{filename}' is not a valid directory or CSV file. Please try again.";
            }
            else
            {
                filePath = potentialFilePath;
            }
        }

        // Path validation
        try
        {
            if (Directory.Exists(filePath))
            {
                await this.Report($"Path '{filename}' is a directory, which is not supported by this uploader. Using Config default ({filePath}).\n", ReportLevel.WARNING);
            }
            else if (!File.Exists(filePath))
            {
                await this.Report($"Path '{filename}' could not be found. Using Config default ({filePath}).\n", ReportLevel.WARNING);
            }
            else if (!Path.GetExtension(filePath).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                await this.Report($"The file you specified ({filePath}) is not a CSV. Please select a CSV file and try again.\n", ReportLevel.ERROR);
                return UploadResult.ErroredOut;
            }

            await this.Report($"Date of last upload: {await GetLastUpdatedDate()}\n", ReportLevel.IMPORTANT);
            bool confirmOverwrite = await this.input.GetConfirmAsync(new ($"WARNING: If successful, this action will overwrite the current CMMS lookup database with the contents of {filePath}. Proceed?", ReportLevel.WARNING));

            if (!confirmOverwrite)
            {
                await this.output.ReportProgress(ProgressEvent.FileSkipped);
                return UploadResult.Canceled; // default to cancel if user does not confirm explicitly
            }

            await this.Upload(filePath);
            await this.output.ReportProgress(ProgressEvent.UploadComplete);
            return UploadResult.Complete;
        }
        catch (Exception ex)
        {
            await this.Report($"Fatal error: {ex.Message}\n", ReportLevel.ERROR);
            await this.output.ReportProgress(ProgressEvent.UploadComplete);
            return UploadResult.ErroredOut;
        }
    }

    /// <summary>
    /// Uploads the CSV file at filepath to the database.
    /// </summary>
    /// <param name="filepath">The path of the CSV to upload.</param>
    /// <returns>A Task representing that the upload is complete.</returns>
    public async Task Upload(string filepath)
    {
        await this.output.SetCurrentFile(Path.GetFileName(filepath));
        await this.output.ReportProgress(ProgressEvent.FileStarted);

        // The layers of wrapping are kind of disgusting, but we need an open StreamReader to create a CsvReader
        // The CsvReader gives us access to CsvDataReader to stream from the table (to the SqlBulkCopy)
        // Finally, we can use our custom TruncatingDataReader to enforce the 8-character limit while streaming from the CsvDataReader
        using StreamReader reader = new (filepath);
        using CsvReader csv = new (reader, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<LineMap>();
        using CsvDataReader dr = new (csv);

        Dictionary<string, int> maxLengths = new (StringComparer.OrdinalIgnoreCase)
        {
            ["Location"] = 8,
        };
        using IDataReader trunc = new TruncatingDataReader(dr, maxLengths);

        // The above section is very fast because it doesn't actually do any parsing, so it doesn't make sense to report.
        await this.Report("Connecting...");
        using SqlConnection connection = new (Config.GetConnectionString());
        await connection.OpenAsync();

        using SqlTransaction transaction = connection.BeginTransaction();
        using SqlBulkCopy bulkCopy = new (connection, SqlBulkCopyOptions.CheckConstraints, transaction);
        bulkCopy.DestinationTableName = "EL2AuthorizedReset.dbo.CmmsToLineName";
        bulkCopy.ColumnMappings.Add("Cmms #", "cmmsNum");
        bulkCopy.ColumnMappings.Add("Location", "lineName");
        await this.Report("Connected!\n");

        // If any DB interaction fails, rollback the entire transaction
        try
        {
            // Now parsing is complete, prepare to completely overwrite old DB state with new
            using (SqlCommand deleteCommand = new ("TRUNCATE TABLE EL2AuthorizedReset.dbo.CmmsToLineName", connection, transaction))
            {
                deleteCommand.ExecuteNonQuery();
            }

            await this.Report("Uploading...");
            await bulkCopy.WriteToServerAsync(trunc);

            // Log the date of successful update in the extended properties
            string sql = @"
                IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'dateLastUpdated', N'SCHEMA', N'dbo', N'TABLE', N'cmmsToLineName', NULL, NULL))
                    EXEC sys.sp_updateextendedproperty @name=N'dateLastUpdated', @value=@now, @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'cmmsToLineName';
                ELSE
                    EXEC sys.sp_addextendedproperty @name=N'dateLastUpdated', @value=@now, @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'cmmsToLineName';";

            using SqlCommand command = new (sql, connection, transaction);
            command.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            await command.ExecuteNonQueryAsync();

            transaction.Commit();
            await this.Report("Complete!\n", ReportLevel.SUCCESS);
            await this.output.ReportProgress(ProgressEvent.FileCompleted);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            await this.Report($"Bulk Copy Error: {ex.Message}\n", ReportLevel.ERROR);
            await this.output.ReportProgress(ProgressEvent.FileCompleted);
        }
    }

    /// <summary>
    /// Creates a report and passes it to the output provider.
    /// </summary>
    /// <param name="msg">The message to report.</param>
    /// <param name="level">The message's report level.</param>
    /// <returns>A Task representing that the report has been displayed to the user.</returns>
    private async Task Report(string msg, ReportLevel level = ReportLevel.INFO) => await this.output.ReportAsync(new (msg, level));
}
