using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
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
    public DbSet<AssociateLine> AssociateToLine { get; set; } // for writing (table)
    public DbSet<AssocNameLine> AssocNameToLine { get; set; } // for reading (view)
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
    [Required(ErrorMessage = "Badge number is required")]
    [Range(1, 99999, ErrorMessage = "Badge number must be five digits")]
    [UniqueBadgeNumber]
    [Column("badgeNum")]
    public int? BadgeNum { get; set; }

    [Required(ErrorMessage = "Associate number is required")]
    [Range(1, 9999, ErrorMessage = "Associate number must be four digits")]
    [UniqueAssociateNumber]
    [Column("associateNum")]
    public int? AssocNum { get; set; }

    [Required(ErrorMessage = "Associate name is required")]
    [MaxLength(32, ErrorMessage = "Associate name must be no longer than 32 characters")]
    [Column("associateName")]
    public string? Name { get; set; }

    [Column("isAdmin")]
    public bool? IsAdmin { get; set; } = false; // default to no admin privileges
}

/// <summary>
/// To be implemented by AssociateLine and its view.
/// Contains the shared information between the two classes
/// </summary>
public interface IAssociateLink
{
    int? AssocNum { get; set; }
    string? Line { get; set; }
}

/// <summary>
/// Represents one row of AssociateToLine in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES
/// </summary>
[PrimaryKey(nameof(AssocNum), nameof(Line))]
[ValidateLineAssignedToAssociate]
public class AssociateLine : IAssociateLink
{
    [Column("associateNum")]
    public int? AssocNum { get; set; }

    [Required(ErrorMessage = "Line name is required")]
    [MaxLength(32, ErrorMessage = "Line name must be no longer than 8 characters (try truncating)")]
    [ValidateLineExists]
    [Column("lineName")]
    public string? Line { get; set; }
}

/// <summary>
/// Represents one row of AssocNameToLine in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES
/// </summary>
[PrimaryKey(nameof(AssocNum), nameof(Line))]
public class AssocNameLine : IAssociateLink
{
    [Column("Associate Name")]
    public string? AssocName { get; set; }

    [Column("Associate Number")]
    public int? AssocNum { get; set; }

    [Column("Authorized Line")]
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
    public int? CmmsNum { get; set; }

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
    public DateTime? Timestamp { get; }

    [Column("associateNum")]
    public int? AssocNum { get; }

    [Column("associateName")]
    public string? AssocName { get; }

    [Column("cmmsNum")]
    public int? CmmsNum { get; }

    [Column("lineName")]
    public string? LineName { get; }

    [Column("isAuthorized")]
    public bool? IsAuthorized { get; }
}