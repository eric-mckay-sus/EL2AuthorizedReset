using Microsoft.Data.SqlClient;
using DotNetEnv;

namespace EL2AuthorizedReset;
class AuthorizeReset
{
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
        bool isAuthorized = Authorize(badgeNum, cmmsNum, conn);
        LogResetAttempt(badgeNum, cmmsNum, isAuthorized, conn);
    }

    /// <summary>
    /// Authorize a badge swipe to release a certain machine.
    /// Effectively, this just counts the entries in the associate-line relationship
    /// where the associate number and badge name match
    /// </summary>
    /// <param name="badgeNum">The badge number read from the badge reader</param>
    /// <param name="cmmsNum">The machine's CMMS number</param>
    /// <param name="conn">The open SQL connection</param>
    /// <returns>Whether the swipe was authorized</returns>
    private static bool Authorize(int badgeNum, int cmmsNum, SqlConnection conn)
    {
        // The first join looks up associate number from badge number
        // The second join looks up line name from CMMS number
        // The WHERE clause is where this particular badge and CMMS are applied
        string sql = @"
        SELECT COUNT(*)
        FROM AssociateInfo a
        JOIN AssociateToLine atl ON a.associateNum = atl.associateNum
        JOIN CmmsToLineName ctl ON atl.lineName = ctl.lineName
        WHERE a.badgeNum = @badge AND ctl.cmmsNum = @cmms";

        using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@badge", badgeNum);
        cmd.Parameters.AddWithValue("@cmms", cmmsNum);

        if (conn.State != System.Data.ConnectionState.Open) conn.Open();

        int count = (int)cmd.ExecuteScalar();
        return count > 0;
    }

    /// <summary>
    /// Logs an attempted reset in the historical database
    /// </summary>
    /// <param name="badgeNum">The badge number from which to get the associate name and number</param>
    /// <param name="cmmsNum">The CMMS number to record and from which to get line name</param>
    /// <param name="isAuthorized">Whether the access attempt was authorized</param>
    /// <param name="conn">The open SQL connection</param>
    private static void LogResetAttempt(int badgeNum, int cmmsNum, bool isAuthorized, SqlConnection conn)
    {
        // We pull the descriptive info (Name and Line) while inserting to keep the history table complete
        string sql = @"
            INSERT INTO Historical (requestTime, associateNum, associateName, cmmsNum, lineName, isAuthorized)
            SELECT 
                GETDATE(), 
                a.associateNum, 
                a.associateName, 
                @cmms, 
                c.lineName, 
                @isAuth
            FROM AssociateInfo a
            CROSS JOIN CmmsToLineName c
            WHERE a.badgeNum = @badge AND c.cmmsNum = @cmms";

        using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@badge", badgeNum);
        cmd.Parameters.AddWithValue("@cmms", cmmsNum);
        cmd.Parameters.AddWithValue("@isAuth", isAuthorized);

        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        int rows = cmd.ExecuteNonQuery();

        if (rows == 0)
        {
            // Handle case where badge or CMMS doesn't exist in the system at all
            Console.WriteLine("Log failed: Invalid Badge or CMMS number.");
        }

        // Remove this later
        Console.WriteLine($"Attempted access at {DateTime.Now}: Badge {badgeNum} for CMMS {cmmsNum} ({(isAuthorized ? "authorized" : "not authorized")})");
    }
}