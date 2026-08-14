// <copyright file="AuthorizeReset.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace EL2AuthorizedReset;

using Microsoft.Data.SqlClient;

using InterProcessIO;

/// <summary>
/// A DTO containing the information to log for a reset attempt
/// </summary>
record ResetAttempt(
    int? associateNum,
    string? associateName,
    int cmmsNum,
    string? lineName,
    bool isAuthorized
);

/// <summary>
/// Authorize and log a reset based on the permissions stored in the DB.
/// </summary>
public static class AuthorizeReset
{
    /// <summary>
    /// Entry point for the authorization class. Validates arguments, connects to database, then delegates to <see cref="Authorize"/> and <see cref="LogResetAttempt"/> for documenting lockout.
    /// </summary>
    /// <param name="args">The command line arguments (harvest badge and CMMS number).</param>
    public static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run [badge number] [CMMS number]");
            return;
        }

        if (!(int.TryParse(args[0], out int badgeNum) && int.TryParse(args[1], out int cmmsNum)))
        {
            PrintInRed("Please ensure both badge number and CMMS number are whole numbers");
            return;
        }

        using SqlConnection conn = new (Config.GetConnectionString());
        conn.Open();
        ResetAttempt? attempt = Authorize(badgeNum, cmmsNum, conn);
        if (attempt.associateNum == null) // Indicates that the badge/CMMS was not found. Indicate this, but log it anyway
        {
            PrintInRed("ERROR: Invalid Badge or CMMS number.");
        }

        LogResetAttempt(attempt, conn);
        Console.WriteLine($"Access {(attempt.isAuthorized ? "granted" : "denied")}.");
    }

    /// <summary>
    /// Prints the specified string to standard output in red.
    /// </summary>
    /// <param name="toPrint">The string to print.</param>
    private static void PrintInRed(string toPrint)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(toPrint);
        Console.ResetColor();
    }

    /// <summary>
    /// Authorize a badge swipe to release a certain machine and collects the request data.
    /// </summary>
    /// <param name="badgeNum">The badge number read from the badge reader.</param>
    /// <param name="cmmsNum">The machine's CMMS number.</param>
    /// <param name="conn">The open SQL connection.</param>
    /// <returns>A ResetAttempt record containing associate name, number, CMMS, line name, and whether the request was authorized.</returns>
    private static ResetAttempt Authorize(int badgeNum, int cmmsNum, SqlConnection conn)
    {
        // 1. Lookup minimum auth level
        // 2. Lookup associate by badge (PK on badgeNum - fast)
        // 3. Check if CMMS maps to one of those lines (indexed on lineName)
        // 4. Get lines for that associate (indexed on associateNum)
        // 5. Verify that associate has line and sufficient auth level
        string sql = @"
        DECLARE @lockoutLevel TINYINT = 0
        SELECT TOP 1 @lockoutLevel = lockoutLevel FROM HistoricalLockouts
		WHERE cmmsNum = @cmmsNum
			AND requestTime <= GETDATE()
			AND resolvedResetId IS NULL
		ORDER BY requestTime ASC

        SELECT TOP 1 a.associateNum, a.associateName, ctl.lineName,
        CAST(CASE WHEN (atl.associateNum IS NOT NULL AND atl.authLevel >= @lockoutLevel) THEN 1 ELSE 0 END AS BIT) as IsAuthorized
        FROM AssociateInfo a
        INNER JOIN CmmsToLineName ctl ON ctl.cmmsNum = @cmmsNum
        LEFT JOIN AssociateToLine atl ON a.associateNum = atl.associateNum AND ctl.lineName = atl.lineName
        WHERE a.badgeNum = @badgeNum";

        using SqlCommand cmd = new (sql, conn);
        cmd.Parameters.AddWithValue("@badgeNum", badgeNum);
        cmd.Parameters.AddWithValue("@cmmsNum", cmmsNum);

        // Set up a reader to build a record from the returned data
        using SqlDataReader reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new ResetAttempt(
                reader.GetInt32(0),
                reader.GetString(1),
                cmmsNum,
                reader.GetString(2),
                reader.GetBoolean(3));
        }
        else
        {
            return new ResetAttempt( // the badge or CMMS doesn't exist: return an empty denied request with the CMMS number
                null,
                null,
                cmmsNum,
                null,
                false);
        }
    }

    /// <summary>
    /// Logs an attempted reset in the historical database.
    /// </summary>
    /// <param name="attempt">The ResetAttempt record to log.</param>
    /// <param name="conn">The open SQL connection.</param>
    private static void LogResetAttempt(ResetAttempt attempt, SqlConnection conn)
    {
        // Authorize already did the heavy lifting of getting the data to insert
        string sql = @"
            INSERT INTO HistoricalResets (requestTime, associateNum, associateName, cmmsNum, lineName, isAuthorized)
            VALUES (GETDATE(), @aNum, @aName, @cmms, @line, @isAuth)";

        using SqlCommand cmd = new (sql, conn);

        // Get the parameters for the SQL statement from the DTO, coalescing nulls as required
        cmd.Parameters.AddWithValue("@aNum", (object?)attempt.associateNum ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@aName", (object?)attempt.associateName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cmms", attempt.cmmsNum);
        cmd.Parameters.AddWithValue("@line", (object?)attempt.lineName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isAuth", attempt.isAuthorized);

        cmd.ExecuteNonQuery();
    }
}
