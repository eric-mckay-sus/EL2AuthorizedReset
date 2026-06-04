// <copyright file="AuthResetDbContext.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace AdminInterface;

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Represents the state of the database in a way friendly to EFCore.
/// </summary>
/// <param name="options">The server details and login credentials.</param>
public class AuthResetDbContext(DbContextOptions<AuthResetDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets or sets the object mapped to the associate info table.
    /// </summary>
    public DbSet<Associate> AssociateInfo { get; set; }

    /// <summary>
    /// Gets or sets the object mapped to the associate-line table.
    /// </summary>
    public DbSet<AssociateLine> AssociateToLine { get; set; } // for writing (table)

    /// <summary>
    /// Gets or sets the object mapped to the view for the group result table.
    /// </summary>
    public DbSet<AssocNameLine> AssocNameToLine { get; set; } // for reading (view)

    /// <summary>
    /// Gets or sets the object mapped to the CMMS number-line table.
    /// </summary>
    public DbSet<CmmsLine> CmmsToLineName { get; set; }

    /// <summary>
    /// Gets or sets the object mapped to the reset table.
    /// </summary>
    public DbSet<Reset> HistoricalResets { get; set; }

    /// <summary>
    /// Gets or sets the object mapped to the lockout table.
    /// </summary>
    public DbSet<Lockout> HistoricalLockouts { get; set; }

    /// <summary>
    /// Gets or sets the object mapped to the view for the full historical (lockouts connected to resets) table.
    /// </summary>
    public DbSet<LockoutReset> FullHistorical { get; set; } // for reading (view)
}

/// <summary>
/// Represents one row of <see cref="AuthResetDbContext.AssociateInfo"/> in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES.
/// </summary>
[PrimaryKey(nameof(BadgeNum))]
public class Associate
{
    /// <summary>
    /// Gets or sets the badge number for an associate.
    /// </summary>
    [Required(ErrorMessage = "Badge number is required")]
    [Range(1, 99999, ErrorMessage = "Badge number must be five digits")]
    [UniqueBadgeNumber]
    [Column("badgeNum")]
    public int BadgeNum { get; set; }

    /// <summary>
    /// Gets or sets the associate number for an associate.
    /// </summary>
    [Required(ErrorMessage = "Associate number is required")]
    [Range(1, 99999, ErrorMessage = "Associate number must be five digits")]
    [UniqueAssociateNumber]
    [Column("associateNum")]
    public int AssociateNum { get; set; }

    /// <summary>
    /// Gets or sets an associate's name.
    /// </summary>
    [Required(ErrorMessage = "Associate name is required")]
    [MaxLength(32, ErrorMessage = "Associate name must be no longer than 32 characters")]
    [Column("associateName")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this associate has privileges to create, edit, and delete associates and their reset permissions.
    /// </summary>
    [Column("isManager")]
    [NotDisplayed]
    public bool IsManager { get; set; }

    /// <summary>
    /// <see cref="Associate"/> objects are equal if they share the same badge number (by PK definition).
    /// </summary>
    /// <param name="obj">The object to check equality with.</param>
    /// <returns>Whether this <see cref="Associate"/> is equal to <paramref name="obj"/>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is Associate other)
        {
            return this.BadgeNum == other.BadgeNum;
        }

        return false;
    }

    /// <summary>
    /// The hash code of an <see cref="Associate"/> is its <see cref="BadgeNum"/> (by PK definition).
    /// </summary>
    /// <returns>A value representing the uniqueness of this <see cref="Associate"/>.</returns>
    public override int GetHashCode() => this.BadgeNum;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <returns>A descriptive string of this <see cref="Associate"/> containing the associate name, number, and badge number.</returns>
    public override string ToString()
    {
        return $"Name: {this.Name}, Assoc #: {this.AssociateNum}, Badge #: {this.BadgeNum}";
    }
}

/// <summary>
/// Implemented by <see cref="AuthResetDbContext.AssociateToLine"/> and <see cref="AuthResetDbContext.AssocNameToLine"/> for linking information.
/// Contains the shared information between the two classes.
/// </summary>
public interface IAssociateLink
{
    /// <summary>
    /// Gets or sets the associate's number.
    /// </summary>
    int AssocNum { get; set; }

    /// <summary>
    /// Gets or sets the line's name.
    /// </summary>
    string? Line { get; set; }

    /// <summary>
    /// Gets or sets the associate' authorization level for this <see cref="Line"/>.
    /// </summary>
    byte AuthLevel { get; set; }
}

/// <summary>
/// Represents one row of <see cref="AuthResetDbContext.AssociateToLine"/> in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES.
/// </summary>
[PrimaryKey(nameof(AssocNum), nameof(Line))]
[ValidateLineAssignedToAssociate]
public class AssociateLine : IAssociateLink
{
    /// <summary>
    /// Gets or sets this associate's number.
    /// </summary>
    [Column("associateNum")]
    public int AssocNum { get; set; }

    /// <summary>
    /// Gets or sets this line's name.
    /// </summary>
    [Required(ErrorMessage = "Line name is required")]
    [MaxLength(8, ErrorMessage = "Line name must be no longer than 8 characters (try truncating)")]
    [ValidateLineExists]
    [Column("lineName")]
    public string? Line { get; set; }

    /// <summary>
    /// Gets or sets this associate's authorization level for this <see cref="Line"/>.
    /// </summary>
    [Range(0, 255, ErrorMessage = "Authorization level must be between 0 and 255 (inclusive)")]
    [Column("authLevel")]
    public byte AuthLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="AssociateLine"/> is new to the database (i.e. part of an insert rather than an update).
    /// </summary>
    [NotMapped] // Tell EF Core to ignore this
    public bool IsNewRecord { get; set; } = true;

    /// <summary>
    /// <see cref="AssociateLine"/> objects are equal if they share the same associate number and line name (by PK definition).
    /// </summary>
    /// <param name="obj">The object to check equality with.</param>
    /// <returns>Whether this <see cref="AssociateLine"/> is equal to <paramref name="obj"/>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is AssociateLine other)
        {
            Console.WriteLine($"this:{this}, other:{other}");
            return this.AssocNum == other.AssocNum && this.Line == other.Line;
        }

        return false;
    }

    /// <summary>
    /// The hash code of an <see cref="AssociateLine"/> is the combination of the hashes of its <see cref="AssocNum"/> and <see cref="Line"/> (by PK definition).
    /// </summary>
    /// <returns>A value representing the uniqueness of this <see cref="AssociateLine"/>.</returns>
    public override int GetHashCode() => HashCode.Combine(this.AssocNum, this.Line);

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <returns>A descriptive string of this <see cref="AssociateLine"/> containing the associate number, line name, and auth level.</returns>
    public override string ToString()
    {
        return $"AssocNum: {this.AssocNum}, LineName: {this.Line}, AuthLevel: {this.AuthLevel}";
    }
}

/// <summary>
/// Represents one row of <see cref="AuthResetDbContext.AssocNameToLine"/>  in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES.
/// </summary>
[PrimaryKey(nameof(AssocNum), nameof(Line))]
public class AssocNameLine : IAssociateLink
{
    /// <summary>
    /// Gets or sets this associate's name.
    /// </summary>
    [Column("AssociateName")]
    [NotDisplayed]
    public string? AssocName { get; set; }

    /// <summary>
    /// Gets or sets this associate's number.
    /// </summary>
    [Column("AssociateNumber")]
    public int AssocNum { get; set; }

    /// <summary>
    /// Gets or sets this line's name.
    /// </summary>
    [Column("AuthorizedLine")]
    public string? Line { get; set; }

    /// <summary>
    /// Gets or sets this associate's auth level on this <see cref="Line"/>.
    /// </summary>
    [Column("AuthLevel")]
    public byte AuthLevel { get; set; }
}

/// <summary>
/// Represents one row of <see cref="AuthResetDbContext.CmmsToLineName"/>  in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES.
/// </summary>
[PrimaryKey(nameof(CmmsNum))]
public class CmmsLine
{
    /// <summary>
    /// Gets or sets the CMMS number for this line.
    /// </summary>
    [Column("cmmsNum")]
    public int CmmsNum { get; set; }

    /// <summary>
    /// Gets or sets this line's name.
    /// </summary>
    [Column("lineName")]
    public string? LineName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this line is active (i.e. if this line has a <see cref="Lockout"/>  in <see cref="AuthResetDbContext.HistoricalLockouts"/>, it has a matching <see cref="Reset"/> ).
    /// </summary>
    [Column("isActive")]
    public bool IsActive { get; set; }

    /// <summary>
    /// The hash code of an <see cref="CmmsLine"/> is its <see cref="CmmsNum"/> (by PK definition).
    /// </summary>
    /// <returns>A value representing the uniqueness of this <see cref="CmmsLine"/>.</returns>
    public override int GetHashCode() => this.CmmsNum;

    /// <summary>
    /// <see cref="CmmsLine"/> objects are equal if they share the same <see cref="CmmsNum"/> (by PK definition).
    /// </summary>
    /// <param name="obj">The object to check equality with.</param>
    /// <returns>Whether this <see cref="CmmsLine"/> is equal to <paramref name="obj"/>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is CmmsLine other)
        {
            return this.CmmsNum == other.CmmsNum;
        }

        return false;
    }
}

/// <summary>
/// Represents one row of <see cref="AuthResetDbContext.FullHistorical"/> in the DB.
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES.
/// </summary>
[PrimaryKey(nameof(LockoutId))]
public class LockoutReset
{
    /// <summary>
    /// Gets or sets the ID linking to the corresponding row in <see cref="Lockout"/>.
    /// </summary>
    [Column("LockoutId")]
    [NotDisplayed]
    public int LockoutId { get; set; }

    /// <summary>
    /// Gets or sets the name of the line (only populated when linked to a <see cref="Reset"/>).
    /// </summary>
    [Column("LineName")]
    public string? LineName { get; set; }

    /// <summary>
    /// Gets or sets the target line's CMMS number.
    /// </summary>
    [Column("CmmsNum")]
    public int CmmsNum { get; set; }

    /// <summary>
    /// Gets or sets the target line's lockout level.
    /// </summary>
    [Column("LockoutLevel")]
    public byte LockoutLevel { get; set; }

    /// <summary>
    /// Gets or sets the time of the lockout.
    /// </summary>
    [Column("LockoutTime")]
    public DateTime LockoutTime { get; set; }

    /// <summary>
    /// Gets or sets the lockout reason.
    /// </summary>
    [Column("Reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the line's status (i.e. still locked out or reset).
    /// </summary>
    [Column("Status")]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the name of the resetting associate (if linked to a <see cref="Reset"/>).
    /// </summary>
    [Column("ResetBy")]
    public string? ResetBy { get; set; }

    /// <summary>
    /// Gets or sets the time of the reset (if linked to a <see cref="Reset"/>).
    /// </summary>
    [Column("ResetTime")]
    public DateTime? ResetTime { get; set; }

    /// <summary>
    /// Gets or sets the ID of the <see cref="Reset"/> which resolved this <see cref="Lockout"/> (if linked).
    /// </summary>
    [NotDisplayed]
    [Column("ResetId")]
    public int? ResetId { get; set; }

    /// <summary>
    /// The hash code of a <see cref="LockoutReset"/> is its <see cref="LockoutId"/> (by PK definition).
    /// </summary>
    /// <returns>A value representing the uniqueness of this <see cref="LockoutReset"/>.</returns>
    public override int GetHashCode() => this.LockoutId;

    /// <summary>
    /// <see cref="LockoutReset"/> objects are equal if they share the same lockout ID.
    /// Reset ID is implied to also be equal, as there is exactly one entry in <see cref="AuthResetDbContext.FullHistorical"/>
    /// for each entry in <see cref="AuthResetDbContext.HistoricalLockouts"/> and one reset resolves exactly one lockout.
    /// </summary>
    /// <param name="obj">The object to check equality with.</param>
    /// <returns>Whether this <see cref="LockoutReset"/> is equal to <paramref name="obj"/>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is LockoutReset other)
        {
            return this.LockoutId == other.LockoutId;
        }

        return false;
    }
}

/// <summary>
/// Represents one row of HistoricalResets in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES.
/// </summary>
[PrimaryKey(nameof(Id))]
public class Reset
{
    /// <summary>
    /// Gets or sets this reset request's ID.
    /// </summary>
    [Column("Id")]
    [NotDisplayed]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets this reset request's time.
    /// </summary>
    [Column("requestTime")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets this reset request's authorization level.
    /// </summary>
    [Column("authLevel")]
    public byte? AuthLevel { get; set; }

    /// <summary>
    /// Gets or sets this reset request's associate number.
    /// </summary>
    [NotDisplayed]
    [Column("associateNum")]
    public int? AssocNum { get; set; }

    /// <summary>
    /// Gets or sets this reset request's associate name.
    /// </summary>
    [Column("associateName")]
    public string? AssocName { get; set; }

    /// <summary>
    /// Gets or sets this reset request's CMMS number.
    /// </summary>
    [NotDisplayed]
    [Column("cmmsNum")]
    public int CmmsNum { get; set; }

    /// <summary>
    /// Gets or sets this reset request's line name.
    /// </summary>
    [Column("lineName")]
    public string? LineName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether reset request was authorized.
    /// </summary>
    [Column("isAuthorized")]
    public bool IsAuthorized { get; set; }

    /// <summary>
    /// Gets or sets the lockout ID foreign keyed to <see cref="AuthResetDbContext.HistoricalLockouts"/>.
    /// </summary>
    [NotDisplayed]
    [Column("LockoutId")]
    public int LockoutId { get; set; }

    /// <summary>
    /// The hash code of an <see cref="Reset"/> is its <see cref="Id"/> (by PK definition).
    /// </summary>
    /// <returns>A value representing the uniqueness of this <see cref="Reset"/>.</returns>
    public override int GetHashCode() => this.Id;

    /// <summary>
    /// <see cref="Reset"/> objects are equal if they share the same <see cref="Id"/>  (by PK definition).
    /// </summary>
    /// <param name="obj">The object to check equality with.</param>
    /// <returns>Whether this <see cref="Reset"/> is equal to <paramref name="obj"/>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is Reset other)
        {
            return this.Id == other.Id;
        }

        return false;
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <returns>A descriptive string of this <see cref="Reset"/> containing the associate number, CMMS number, and timestamp.</returns>
    public override string ToString()
    {
        return $"Associate #{this.AssocNum} reset {this.CmmsNum} at {this.Timestamp}";
    }
}

/// <summary>
/// Represents one row of HistoricalLockouts in the DB
/// NOTE: VERY SENSITIVE TO COL NAME CHANGES.
/// </summary>
[PrimaryKey(nameof(Id))]
public class Lockout
{
    /// <summary>
    /// Gets or sets this lockout's ID.
    /// </summary>
    [Column("Id")]
    [NotDisplayed]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets this lockout's request time.
    /// </summary>
    [Column("requestTime")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets this CMMS number of the machine that this lockout affects.
    /// </summary>
    [Column("cmmsNum")]
    public int CmmsNum { get; set; }

    /// <summary>
    /// Gets or sets the minimum authorization level required to reset this lockout.
    /// </summary>
    [Column("lockoutLevel")]
    public byte LockoutLevel { get; set; }

    /// <summary>
    /// Gets or sets the reason this machine was locked out.
    /// </summary>
    [Column("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// The hash code of an <see cref="Lockout"/> is its <see cref="Id"/> (by PK definition).
    /// </summary>
    /// <returns>A value representing the uniqueness of this <see cref="Lockout"/>.</returns>
    public override int GetHashCode() => this.Id;

    /// <summary>
    /// <see cref="Lockout"/> objects are equal if they share the same <see cref="Id"/>  (by PK definition).
    /// </summary>
    /// <param name="obj">The object to check equality with.</param>
    /// <returns>Whether this <see cref="Lockout"/> is equal to <paramref name="obj"/>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is Lockout other)
        {
            return this.Id == other.Id;
        }

        return false;
    }
}
