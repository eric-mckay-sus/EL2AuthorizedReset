// <copyright file="ValidationAttributes.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace AdminInterface;

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Verifies that an associate's badge number is unique.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class UniqueBadgeNumberAttribute : ValidationAttribute
{
    /// <summary>
    /// Checks the new associate's badge number against the database.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The metadata for the validation.</param>
    /// <returns>ValidationResult.Success if the badge number is unique, otherwise a validation result reporting the failure.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        IDbContextFactory<AuthResetDbContext>? dbFactory = validationContext.GetService<IDbContextFactory<AuthResetDbContext>>();
        var entity = (Associate)validationContext.ObjectInstance;

        using AuthResetDbContext context = dbFactory!.CreateDbContext();

        // Check BadgeNum collision
        if (context.AssociateInfo.Any(a => a.BadgeNum == entity.BadgeNum))
        {
            return new ValidationResult($"Badge #{entity.BadgeNum} is already assigned.", [nameof(Associate.BadgeNum)]);
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Verify that an associate's associate number is unique.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class UniqueAssociateNumberAttribute : ValidationAttribute
{
    /// <summary>
    /// Checks the new associate's associate number against the database.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The metadata for the validation.</param>
    /// <returns>ValidationResult.Success if the associate number is unique, otherwise a validation result reporting the failure.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        IDbContextFactory<AuthResetDbContext>? dbFactory = validationContext.GetService<IDbContextFactory<AuthResetDbContext>>();
        var entity = (Associate)validationContext.ObjectInstance;

        using AuthResetDbContext context = dbFactory!.CreateDbContext();

        // Check AssocNum collision
        if (context.AssociateInfo.Any(a => a.AssociateNum == entity.AssociateNum))
        {
            return new ValidationResult($"Associate #{entity.AssociateNum} is already in use.", [nameof(Associate.AssociateNum)]);
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Verify that an associate exists in the associate database.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ValidateAssociateExistsAttribute : ValidationAttribute
{
    /// <summary>
    /// Checks that the associate-line link's associate is in the associate database.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The metadata for the validation.</param>
    /// <returns>ValidationResult.Success if the associate is in the associate database, otherwise a validation result reporting the failure.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        IDbContextFactory<AuthResetDbContext>? dbFactory = validationContext.GetService<IDbContextFactory<AuthResetDbContext>>();
        var al = (AssociateLine)validationContext.ObjectInstance;

        using AuthResetDbContext context = dbFactory!.CreateDbContext();

        // FK Check: Does Associate exist?
        if (!context.AssociateInfo.Any(a => a.AssociateNum == al.AssocNum))
        {
            return new ValidationResult($"Associate #{al.AssocNum} does not exist.", [nameof(AssociateLine.AssocNum)]);
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Verify that a line exists in the CMMS to line name database.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ValidateLineExistsAttribute : ValidationAttribute
{
    /// <summary>
    /// Checks that the associate-line link's line is in the CMMS to line database.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The metadata for the validation.</param>
    /// <returns>ValidationResult.Success if the line is in the CMMS to line database, otherwise a validation result reporting the failure.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        IDbContextFactory<AuthResetDbContext>? dbFactory = validationContext.GetService<IDbContextFactory<AuthResetDbContext>>();
        var al = (AssociateLine)validationContext.ObjectInstance;

        using AuthResetDbContext context = dbFactory!.CreateDbContext();

        // FK Check: Does Line exist?
        if (!context.CmmsToLineName.Any(l => l.LineName == al.Line))
        {
            return new ValidationResult($"Line '{al.Line}' is not valid. If it should be, please add it in the 'Update CMMS' section", [nameof(AssociateLine.Line)]);
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Verify that a target associate and line are not already linked.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ValidateLineAssignedToAssociateAttribute : ValidationAttribute
{
    /// <summary>
    /// Checks that an associate-line pair is not already in the associate-line database.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The metadata for the validation.</param>
    /// <returns>ValidationResult.Success if there is not a matching entry in the associate-line database, otherwise a validation result reporting the failure.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var al = (AssociateLine)validationContext.ObjectInstance;
        if (!al.IsNewRecord)
        {
            return ValidationResult.Success; // immediately return if updating
        }

        IDbContextFactory<AuthResetDbContext>? dbFactory = validationContext.GetService<IDbContextFactory<AuthResetDbContext>>();
        using AuthResetDbContext context = dbFactory!.CreateDbContext();

        // PK Check: Is this pair already linked with this auth level?
        if (context.AssociateToLine.Any(x => x.AssocNum == al.AssocNum && x.Line == al.Line))
        {
            return new ValidationResult("This associate is already assigned to this line. If you meant to update their auth level, please expand its row.", [nameof(AssociateLine.Line)]);
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Marks a property that should not be displayed in UniversalTable.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class NotDisplayedAttribute : Attribute
{
}
