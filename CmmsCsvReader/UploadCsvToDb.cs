using Microsoft.Data.SqlClient;
using DotNetEnv;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Data;

namespace CmmsCsvReader;
public class Line
{
    public int CmmsNum { get; set; }
    public string Name { get; set; }
}

public sealed class LineMap : ClassMap<Line>
{
    public LineMap()
    {
        Map(m => m.CmmsNum).Name("CMMS #");
        Map(m => m.Name).Name("Location");
    }
}

/// <summary>
/// A CSV parser to get the current mappings of CMMS numbers to line names.
/// Upon successful parsing, replaces the current dataset in the DB
/// </summary>
public class UploadCsvToDb
{
    /// <summary>
    /// Entry point for the program. Parses the entire file for mappings and adds them all to the database
    /// </summary>
    /// <param name="args">The file to parse (must be a CSV of the correct format)</param>
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("No file argument detected. Please retry and supply path for file from which to harvest line mappings.");
            return;
        }
        string file = args[0];
        if (!File.Exists(file)) {
            Console.WriteLine($"The file you specified ({file}) could not be found. Please check your spelling and try again. The path may be relative to this program or absolute.");
            return;
        }

        // Env.Load();
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Environment.GetEnvironmentVariable("DB_SERVER"),
            UserID = Environment.GetEnvironmentVariable("DB_USER"),
            Password = Environment.GetEnvironmentVariable("DB_PASS"),
            InitialCatalog = Environment.GetEnvironmentVariable("DB_NAME"),
            TrustServerCertificate = true //TODO insecure, eventually require certificate verification
        };
        
        Console.WriteLine($"Date of last upload: {await GetLastUpdatedDate(builder.ConnectionString)}");
        Console.WriteLine($"WARNING: If successful, this action will overwrite the current CMMS lookup database with the contents of {file}. Proceed? (y/n)");

        if (!Console.ReadLine().Equals("y", StringComparison.OrdinalIgnoreCase)) return; // default to cancel if user does not input y or Y

        var consoleProgress = new Progress<string>(msg => Console.Write(msg));
        await Upload(file, builder.ConnectionString, consoleProgress);
    }

    public static async Task<string> GetLastUpdatedDate(string connectionString)
    {
        const string sql = @"
            SELECT CAST(value AS NVARCHAR(MAX)) AS Value 
            FROM sys.fn_listextendedproperty(N'dateLastUpdated', N'SCHEMA', N'dbo', N'TABLE', N'cmmsToLineName', default, default)";

        try
        {
            using SqlConnection connection = new(connectionString);
            using SqlCommand command = new(sql, connection);
            
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
    /// Uploads the CSV file at filepath to the database
    /// </summary>
    /// <param name="filepath">The path of the CSV to upload</param>
    /// <param name="progress">The IProgress implementation to which the progress should be reported</param>
    public static async Task Upload(string filepath, string connectionString, IProgress<string>? progress = null)
    {
        // Called as a conditional Console.Write
        void report(string msg) => progress?.Report(msg);

        if(!Path.GetExtension(filepath).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            report("The file you provided is not a CSV. Please ensure the input file is of the correct filetype and format, then try again");
            return;
        }

        // The layers of wrapping are kind of disgusting, but we need an open StreamReader to create a CsvReader
        // The CsvReader gives us access to CsvDataReader to stream from the table (to the SqlBulkCopy)
        // Finally, we can use our custom TruncatingDataReader to enforce the 8-character limit while streaming from the CsvDataReader
        using var reader = new StreamReader(filepath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<LineMap>();
        using var dr = new CsvDataReader(csv);

        var maxLengths = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Location"] = 8
        };
        using IDataReader trunc = new TruncatingDataReader(dr, maxLengths);
        // The above section is very fast because it doesn't actually do any parsing, so it doesn't make sense to report.

        report("Connecting...");
        using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        using SqlBulkCopy bulkCopy = new(connection, SqlBulkCopyOptions.CheckConstraints, transaction);
        bulkCopy.DestinationTableName = "EL2AuthorizedReset.dbo.CmmsToLineName";
        bulkCopy.ColumnMappings.Add("Cmms #", "cmmsNum");
        bulkCopy.ColumnMappings.Add("Location", "lineName");
        report("Connected!\n");

        // If any DB interaction fails, rollback the entire transaction
        try
        {
            // Now parsing is complete, prepare to completely overwrite old DB state with new
            using (var deleteCommand = new SqlCommand("TRUNCATE TABLE EL2AuthorizedReset.dbo.CmmsToLineName", connection, transaction))
            {
                deleteCommand.ExecuteNonQuery();
            }

            report("Uploading...");
            await bulkCopy.WriteToServerAsync(trunc);

            // Log the date of successful update in the extended properties
            string sql = @"
                IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'dateLastUpdated', N'SCHEMA', N'dbo', N'TABLE', N'cmmsToLineName', NULL, NULL))
                    EXEC sys.sp_updateextendedproperty @name=N'dateLastUpdated', @value=@now, @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'cmmsToLineName';
                ELSE
                    EXEC sys.sp_addextendedproperty @name=N'dateLastUpdated', @value=@now, @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'cmmsToLineName';";

            using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            await command.ExecuteNonQueryAsync();

            transaction.Commit();
            report("Complete!\n");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            report($"Bulk Copy Error: {ex.Message}\n");
            throw; // so the caller knows it failed
        }
    }
}