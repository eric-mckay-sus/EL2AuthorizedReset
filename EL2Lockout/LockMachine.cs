// <copyright file="LockMachine.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace EL2Lockout;

using Microsoft.Data.SqlClient;
using static Environment;

/// <summary>
/// Log a lockout with required auth level using CMMS number and reason.
/// </summary>
public class LockMachine
{
    /// <summary>
    /// Entry point for the lockout class. Validates arguments, connects to database, then delegates to <see cref="LogLockout"/> for documenting lockout.
    /// </summary>
    /// <param name="args">The command line arguments (harvest badge, lockout level, and CMMS number).</param>
    public static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run [CMMS number] [reason (in quotes)] <lockout level>");
            return;
        }

        if (!int.TryParse(args[0], out int cmmsNum))
        {
            ErrorOut("Please ensure CMMS number is a whole number");
        }

        byte lockoutLevel = 0;
        if (args.Length > 2 && !byte.TryParse(args[2], out lockoutLevel))
        {
            ErrorOut("Please ensure lockout level is a whole number between 0 and 255");
        }

        // Can check length w/o any work on quotes bc that's handled by terminal
        if (args[1].Length > 50)
        {
            ErrorOut("Please ensure reason is no longer than 50 characters");
        }

        string reason = args[1];

        string? server = GetEnvironmentVariable("DB_SERVER"), user = GetEnvironmentVariable("DB_USER"), password = GetEnvironmentVariable("DB_PASS"), name = GetEnvironmentVariable("DB_NAME");

        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(name))
        {
            ErrorOut("One or more environment variables for database connection are missing. Please reload your terminal (or its context) and try again.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            UserID = user,
            Password = password,
            InitialCatalog = name,
            TrustServerCertificate = true, // TODO insecure, eventually require certificate verification
        };
        using SqlConnection conn = new (builder.ConnectionString);
        conn.Open();
        LogLockout(cmmsNum, reason, lockoutLevel, conn);
        Console.WriteLine("Lockout documented.");
    }

    /// <summary>
    /// Prints the specified string to standard output in red.
    /// </summary>
    /// <param name="toPrint">The string to print.</param>
    private static void ErrorOut(string toPrint)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(toPrint);
        Console.ResetColor();
        Exit(-1);
    }

    /// <summary>
    /// Logs a machine lockout in the historical database.
    /// </summary>
    /// <param name="cmmsNum">The CMMS number of the machine being locked out.</param>
    /// <param name="reason">The reason the lockout is occurring.</param>
    /// <param name="level">The auth level required to reset this lockout.</param>
    /// <param name="conn">The open SQL connection.</param>
    private static void LogLockout(int cmmsNum, string reason, byte level, SqlConnection conn)
    {
        string sql = @"
            INSERT INTO HistoricalLockouts (requestTime, cmmsNum, lockoutLevel, reason)
            VALUES (GETDATE(), @cmms, @level, @reason)";

        using SqlCommand cmd = new (sql, conn);

        // Get the parameters for the SQL statement from the DTO, coalescing nulls as required
        cmd.Parameters.AddWithValue("@cmms", cmmsNum);
        cmd.Parameters.AddWithValue("@level", level);
        cmd.Parameters.AddWithValue("@reason", reason);

        cmd.ExecuteNonQuery();
    }
}
