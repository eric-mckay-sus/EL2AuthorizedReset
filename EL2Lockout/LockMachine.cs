using Microsoft.Data.SqlClient;
using ENV = System.Environment;

namespace EL2AuthorizedReset;

/// <summary>
/// Log a lockout using CMMS number and reason
/// </summary>
class LockMachine
{
    /// <summary>
    /// Entry point for the lockout class
    /// </summary>
    /// <param name="args">The command line arguments (harvest badge and CMMS number)</param>
    public static void Main(string[] args)
    {
        if(args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run [CMMS number] [reason (in quotes)]");
            return;
        }
        if(!int.TryParse(args[0], out int cmmsNum))
        {
            PrintInRed("Please ensure CMMS number is a whole number");
            return;
        }
        // Can check length w/o any work on quotes bc that's handled by terminal
        if(args[1].Length > 50)
        {
            PrintInRed("Please ensure reason is no longer than 50 characters");
            return;
        }
        string reason = args[1];
        
        string? server=ENV.GetEnvironmentVariable("DB_SERVER"), user=ENV.GetEnvironmentVariable("DB_USER"), password=ENV.GetEnvironmentVariable("DB_PASS"), name=ENV.GetEnvironmentVariable("DB_NAME");

        if(string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(name))
        {
            PrintInRed("One or more environment variables for database connection are missing. Please reload your terminal (or its context) and try again.");
            return;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            UserID = user,
            Password = password,
            InitialCatalog = name,
            TrustServerCertificate = true //TODO insecure, eventually require certificate verification
        };
        using SqlConnection conn = new(builder.ConnectionString);
        conn.Open();
        LogLockout(cmmsNum, reason, conn);
        Console.WriteLine("Lockout documented.");
    }

    /// <summary>
    /// Prints the specified string to standard output in red
    /// </summary>
    /// <param name="toPrint">The string to print</param>
    private static void PrintInRed(string toPrint)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(toPrint);
        Console.ResetColor();
    }

    /// <summary>
    /// Logs a machine lockout in the historical database
    /// </summary>
    /// <param name="cmmsNum">The CMMS number of the machine being locked out</param>
    /// <param name="reason">The reason the lockout is occurring</param>
    /// <param name="conn">The open SQL connection</param>
    private static void LogLockout(int cmmsNum, string reason, SqlConnection conn)
    {
        string sql = @"
            INSERT INTO HistoricalLockouts (requestTime, cmmsNum, reason)
            VALUES (GETDATE(), @cmms, @reason)";

        using SqlCommand cmd = new(sql, conn);

        // Get the parameters for the SQL statement from the DTO, coalescing nulls as required
        cmd.Parameters.AddWithValue("@cmms", cmmsNum);
        cmd.Parameters.AddWithValue("@reason", reason);

        cmd.ExecuteNonQuery();
    }
}