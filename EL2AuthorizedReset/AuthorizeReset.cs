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
        LogResetAttempt(badgeNum, cmmsNum, isAuthorized);
    }

    /// <summary>
    /// Authorize a badge swipe to release a certain machine
    /// </summary>
    /// <param name="badgeNum">The badge number read from the badge reader</param>
    /// <param name="cmmsNum">The machine's CMMS number</param>
    /// <param name="conn">The open SQL connection</param>
    /// <returns>Whether the swipe was authorized</returns>
    private static bool Authorize(int badgeNum, int cmmsNum, SqlConnection conn)
    {
        return false;
    }

    /// <summary>
    /// Logs an attempted reset in the historical database
    /// </summary>
    /// <param name="badgeNum">The badge number from which to get the associate name and number</param>
    /// <param name="cmmsNum">The CMMS number to record and from which to get line name</param>
    /// <param name="isAuthorized">Whether the access attempt was authorized</param>
    private static void LogResetAttempt(int badgeNum, int cmmsNum, bool isAuthorized)
    {
        Console.WriteLine($"Attempted access at {DateTime.Now}: Badge {badgeNum} for CMMS {cmmsNum} ({(isAuthorized ? "authorized" : "not authorized")})");
    }
}