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
    public DbSet<Reset> HistoricalResets { get; set; }
    public DbSet<Lockout> HistoricalLockouts { get; set; }
    public DbSet<LockoutReset> FullHistorical { get; set; } // for reading (view)
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
    [Range(1, 99999, ErrorMessage = "Associate number must be five digits")]
    [UniqueAssociateNumber]
    [Column("associateNum")]
    public int? AssocNum { get; set; }

    [Required(ErrorMessage = "Associate name is required")]
    [MaxLength(32, ErrorMessage = "Associate name must be no longer than 32 characters")]
    [Column("associateName")]
    public string? Name { get; set; }

    [Column("isAdmin")]
    [NotDisplayed]
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Associates are equal if they share the same badge number (by PK definition)
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>Whether this associate equals <paramref="obj /></returns>
    public override bool Equals(object? obj)
    {
        if (obj is Associate other)
        {
            return BadgeNum == other.BadgeNum;
        }
        return false;
    }

    /// <summary>
    /// The hash code of an associate is the hash of its badge number
    /// </summary>
    /// <returns>The associate's hash code</returns>
    public override int GetHashCode() => BadgeNum.GetHashCode();

    public override string ToString()
    {
        return $"Name: {Name}, Assoc #: {AssocNum}, Badge #: {BadgeNum}";
    }
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
    public int CmmsNum { get; set; }

    [Column("lineName")]
    public string LineName { get; set; }

    [Column("isActive")]
    public bool IsActive { get; set; }
}

/// <summary>
/// Represents the pairing of a lockout and reset
/// </summary>
public class LockoutReset
{
    [Key] // EF Core needs a key; LockoutId is unique per row here
    [Column("LockoutId")]
    [NotDisplayed]
    public int LockoutId { get; set; }

    [Column("CmmsNum")]
    public int CmmsNum { get; set; }

    [Column("LockoutTime")]
    public DateTime LockoutTime { get; set; }

    [Column("Reason")]
    public string Reason { get; set; }

    [Column("Status")]
    public string Status { get; set; }

    [Column("ResetBy")]
    public string? ResetBy { get; set; }

    [Column("ResetTime")]
    public DateTime? ResetTime { get; set; }

    [Column("LineName")]
    public string? LineName { get; set; }

    [NotDisplayed]
    [Column("ResetId")]
    public int? ResetId { get; set; }
}

/// <summary>
/// Represents one row of HistoricalResets in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES
/// </summary>
[PrimaryKey(nameof(Id))]
public class Reset
{
    [Column("Id")]
    [NotDisplayed]
    public int Id { get; set; }

    [Column("requestTime")]
    public DateTime Timestamp { get; set; }

    [Column("associateNum")]
    public int? AssocNum { get; set; }

    [Column("associateName")]
    public string? AssocName { get; set; }

    [Column("cmmsNum")]
    public int CmmsNum { get; set; }

    [Column("lineName")]
    public string? LineName { get; set; }

    [Column("isAuthorized")]
    public bool? IsAuthorized { get; set; }

    public override string ToString()
    {
        return $"Associate #{AssocNum} reset {CmmsNum} at {Timestamp}";
    }
}

/// <summary>
/// Represents one row of HistoricalLockouts in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES
/// </summary>
[PrimaryKey(nameof(Id))]
public class Lockout
{
    [Column("Id")]
    [NotDisplayed]
    public int Id { get; set; }

    [Column("requestTime")]
    public DateTime Timestamp { get; set; }

    [Column("cmmsNum")]
    public int CmmsNum { get; set; }

    [Column("reason")]
    public string Reason { get; set; }
}