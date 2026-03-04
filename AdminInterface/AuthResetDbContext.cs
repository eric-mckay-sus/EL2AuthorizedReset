using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdminInterface;
/// <summary>
/// Represents the state of the database in a way friendly to EFCore
/// </summary>
/// <param name="options">The server details and login credentials</param>
public class AuthResetDbContext(DbContextOptions<AuthResetDbContext> options) : DbContext(options)
{
    // One set per table, MUST match table names
    public DbSet<Associate> AssociateInfo { get; set; }
    public DbSet<AssociateLine> AssociateToLine { get; set; }
    public DbSet<CmmsLine> CmmsToLineName { get; set; }
    public DbSet<Reset> Historical { get; set; }
}

/// <summary>
/// Represents one row of AssociateInfo in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES
/// </summary>
[PrimaryKey(nameof(BadgeNum))]
public class Associate
{
    [Column("badgeNum")]
    public int? BadgeNum { get; set; }

    [Column("associateNum")]
    public int? AssocNum { get; set; }

    [Column("associateName")]
    public string? Name { get; set; }

    [Column("isAdmin")]
    public bool? IsAdmin { get; set; } = false; // default to no admin privileges
}

/// <summary>
/// Represents one row of AssociateToLine in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES
/// </summary>
[PrimaryKey(nameof(AssocNum))]
public class AssociateLine
{
    [Column("associateNum")]
    public int? AssocNum { get; set; }

    [Column("lineName")]
    public string? Line { get; set; }
}

/// <summary>
/// Represents one row of CmmsToLineName in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES
/// </summary>
[PrimaryKey(nameof(CmmsNum))]
public class CmmsLine
{
    [Column("cmmsNum")]
    public string? CmmsNum { get; set; }

    [Column("lineName")]
    public string? LineName { get; set; }
}

/// <summary>
/// Represents one row of Historical in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES
/// </summary>
[PrimaryKey(nameof(Timestamp))]
public class Reset
{
    [Column("requestTime")]
    public DateTime? Timestamp;

    [Column("associateNum")]
    public DateTime? AssocNum;

    [Column("associateName")]
    public DateTime? AssocName;

    [Column("cmmsNum")]
    public DateTime? CmmsNum;

    [Column("lineName")]
    public DateTime? LineName;

    [Column("isAuthorized")]
    public DateTime? IsAuthorized;
}