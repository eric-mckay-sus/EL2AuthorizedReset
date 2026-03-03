using Microsoft.Data.SqlClient;
using DotNetEnv;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Data;

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
class Program
{
    /// <summary>
    /// Entry point for the program. Parses the entire file for mappings and adds them all to the database
    /// </summary>
    /// <param name="args">The file to parse (must be a CSV of the correct format)</param>
    static void Main(string[] args)
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

        Console.WriteLine($"WARNING: If successful, this action will overwrite the current CMMS lookup database with the contents of {file}. Proceed? (y/n)");

        if (!Console.ReadLine().Equals("y", StringComparison.OrdinalIgnoreCase)) return; // default to cancel if user does not input y or Y

        Console.Write("Parsing...");
        // The layers of wrapping are kind of disgusting, but we need an open StreamReader to create a CsvReader
        // The CsvReader gives us access to CsvDataReader to stream from the table (to the SqlBulkCopy)
        // Finally, we can use our custom TruncatingDataReader to enforce the 8-character limit while streaming from the CsvDataReader
        using var reader = new StreamReader(file);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<LineMap>();
        using var dr = new CsvDataReader(csv);

        var maxLengths = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Location"] = 8
        };
        using IDataReader trunc = new TruncatingDataReader(dr, maxLengths);
        Console.WriteLine("Complete!");

        Console.Write("Connecting...");
        Env.Load();
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Environment.GetEnvironmentVariable("DB_SERVER"),
            UserID = Environment.GetEnvironmentVariable("DB_USER"),
            Password = Environment.GetEnvironmentVariable("DB_PASS"),
            InitialCatalog = Environment.GetEnvironmentVariable("DB_NAME"),
            TrustServerCertificate = true //TODO insecure, eventually require certificate verification
        };
        using SqlConnection connection = new(builder.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using SqlBulkCopy bulkCopy = new(connection, SqlBulkCopyOptions.CheckConstraints, transaction);
        bulkCopy.DestinationTableName = "EL2AuthorizedReset.dbo.CmmsToLineName";
        bulkCopy.ColumnMappings.Add("Cmms #", "cmmsNum");
        bulkCopy.ColumnMappings.Add("Location", "lineName");
        Console.WriteLine("Connected!");

        // If any DB interaction fails, rollback the entire transaction
        try
        {
            // Now parsing is complete, prepare to completely overwrite old DB state with new
            using (var deleteCommand = new SqlCommand("TRUNCATE TABLE EL2AuthorizedReset.dbo.CmmsToLineName", connection, transaction))
            {
                deleteCommand.ExecuteNonQuery();
            }

            Console.Write("Uploading...");
            bulkCopy.WriteToServer(trunc);

            transaction.Commit();
            Console.WriteLine("Complete!");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"Bulk Copy Error: {ex.Message}");
        }
    }
}