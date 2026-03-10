using Microsoft.Data.SqlClient;
using DotNetEnv;

namespace EL2AuthorizedReset;
/// <summary>
/// A DTO containing the information to log for a reset attempt
/// </summary>
record ResetAttempt(
    int AssociateNum,
    string AssociateName,
    int CmmsNum,
    string LineName,
    bool IsAuthorized
);

/// <summary>
/// Authorize and log a reset based on the permissions stored in the DB
/// </summary>
class AuthorizeReset
{
    /// <summary>
    /// Entry point for the authorization class
    /// </summary>
    /// <param name="args">The command line arguments (harvest badge and CMMS number)</param>
    public static void Main(string[] args)
    {
        if(args.Length < 2)
        {
            Console.WriteLine("Usage: AuthorizeReset [badge number] [CMMS number]");
            return;
        }
        if(!(int.TryParse(args[0], out int badgeNum) && int.TryParse(args[1], out int cmmsNum)))
        {
            Console.WriteLine("Please ensure both badge number and CMMS number are whole numbers");
            return;
        }
        Env.Load();
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Environment.GetEnvironmentVariable("DB_SERVER"),
            UserID = Environment.GetEnvironmentVariable("DB_USER"),
            Password = Environment.GetEnvironmentVariable("DB_PASS"),
            InitialCatalog = Environment.GetEnvironmentVariable("DB_NAME"),
            TrustServerCertificate = true //TODO insecure, eventually require certificate verification
        };
        using SqlConnection conn = new(builder.ConnectionString);
        conn.Open();
        ResetAttempt? attempt = Authorize(badgeNum, cmmsNum, conn);
        if (attempt != null)
        {
            LogResetAttempt(attempt, conn);
            Console.WriteLine($"Authorized: {attempt.IsAuthorized}");
        } else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("ERROR: Invalid Badge or CMMS number.");
        }
    }

    /// <summary>
    /// Authorize a badge swipe to release a certain machine and collects the request data
    /// </summary>
    /// <param name="badgeNum">The badge number read from the badge reader</param>
    /// <param name="cmmsNum">The machine's CMMS number</param>
    /// <param name="conn">The open SQL connection</param>
    /// <returns>Null if badge/CMMS does not exist,
    /// otherwise a ResetAttempt record containing associate name, number, CMMS, line name, and whether the request was authorized
    /// </returns>
    private static ResetAttempt? Authorize(int badgeNum, int cmmsNum, SqlConnection conn)
    {
        // 1. Lookup associate by badge (PK on badgeNum - fast)
        // 2. Check if CMMS maps to one of those lines (indexed on lineName)
        // 3. Get lines for that associate (indexed on associateNum)
        string sql = @"
        SELECT TOP 1 a.associateNum, a.associateName, ctl.lineName,
        CAST(CASE WHEN atl.associateNum IS NOT NULL THEN 1 ELSE 0 END AS BIT) as IsAuthorized
        FROM AssociateInfo a
        INNER JOIN CmmsToLineName ctl ON ctl.cmmsNum = @cmms
        LEFT JOIN AssociateToLine atl ON a.associateNum = atl.associateNum AND ctl.lineName = atl.lineName
        WHERE a.badgeNum = @badge";

        using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@badge", badgeNum);
        cmd.Parameters.AddWithValue("@cmms", cmmsNum);

        // Set up a reader to build a record from the returned data
        using SqlDataReader reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new ResetAttempt(
                reader.GetInt32(0),
                reader.GetString(1),
                cmmsNum,
                reader.GetString(2),
                reader.GetBoolean(3)
            );
        }
        return null; // the badge or CMMS doesn't exist
    }

    /// <summary>
    /// Logs an attempted reset in the historical database
    /// </summary>
    /// <param name="attempt">The ResetAttempt record to log</param>
    /// <param name="conn">The open SQL connection</param>
    private static void LogResetAttempt(ResetAttempt attempt, SqlConnection conn)
    {
        // Authorize already did the heavy lifting of getting the data to insert
        string sql = @"
            INSERT INTO Historical (requestTime, associateNum, associateName, cmmsNum, lineName, isAuthorized)
            VALUES (GETDATE(), @aNum, @aName, @cmms, @line, @isAuth)";

        using SqlCommand cmd = new(sql, conn);

        // Get the parameters for the SQL statement from the DTO
        cmd.Parameters.AddWithValue("@aNum", attempt.AssociateNum);
        cmd.Parameters.AddWithValue("@aName", attempt.AssociateName);
        cmd.Parameters.AddWithValue("@cmms", attempt.CmmsNum);
        cmd.Parameters.AddWithValue("@line", attempt.LineName);
        cmd.Parameters.AddWithValue("@isAuth", attempt.IsAuthorized);

        cmd.ExecuteNonQuery();
    }
}