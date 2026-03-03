using Microsoft.Data.SqlClient;
using DotNetEnv;
using System.Data;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

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

        Console.Write("Connecting...");
        Env.Load(); // Only use of DotNetEnv
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Environment.GetEnvironmentVariable("DB_SERVER"),
            UserID = Environment.GetEnvironmentVariable("DB_USER"),
            Password = Environment.GetEnvironmentVariable("DB_PASS"),
            InitialCatalog = Environment.GetEnvironmentVariable("DB_NAME"),
            TrustServerCertificate = true //TODO insecure, eventually require certificate verification
        };
        using SqlBulkCopy bulkCopy = new(builder.ConnectionString, SqlBulkCopyOptions.CheckConstraints);
        bulkCopy.DestinationTableName = "EL2AuthorizedReset.dbo.CmmsToLineName";
        Console.WriteLine("Connected!");
        
        Console.Write("Parsing...");
        DataTable table = new();
        table.Columns.Add("cmmsNum", typeof(int));
        table.Columns.Add("lineName", typeof(string));

        using (StreamReader reader = new(file))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            csv.Context.RegisterClassMap<LineMap>();
            var records = csv.GetRecords<Line>();
            foreach (var record in records)
            {
                string name = record.Name; // don't care whether this mutates record.Name or not
                if (name.Length > 8) name = name[..8];
                table.Rows.Add(record.CmmsNum, name);
            }
            Console.WriteLine("Complete!");
        }
        Console.Write("Uploading...");
        try
        {
            bulkCopy.WriteToServer(table);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bulk Copy Error: {ex.Message}");
        }
        Console.WriteLine("Complete!");        
    }
}
