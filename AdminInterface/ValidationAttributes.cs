using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AdminInterface;

/// <summary>
/// Verifies that an associate's badge number is unique
/// </summary>
public class UniqueBadgeNumberAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var dbFactory = validationContext.GetService<IDbContextFactory<AuthResetDbContext>>();
        var entity = (Associate)validationContext.ObjectInstance;

        using var context = dbFactory!.CreateDbContext();

        // Check BadgeNum collision
        if (context.AssociateInfo.Any(a => a.BadgeNum == entity.BadgeNum))
            return new ValidationResult($"Badge #{entity.BadgeNum} is already assigned.", [nameof(Associate.BadgeNum)]);

        return ValidationResult.Success;
    }
}

/// <summary>
/// Verify that an associate's associate number is unique
/// </summary>
public class UniqueAssociateNumberAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var dbFactory = validationContext.GetService<IDbContextFactory<AuthResetDbContext>>();
        var entity = (Associate)validationContext.ObjectInstance;

        using var context = dbFactory!.CreateDbContext();

        // Check AssocNum collision
        if (context.AssociateInfo.Any(a => a.AssocNum == entity.AssocNum))
            return new ValidationResult($"Associate #{entity.AssocNum} is already in use.", [nameof(Associate.AssocNum)]);

        return ValidationResult.Success;
    }
}

/// <summary>
/// Verify that a an associate exists in AssociateInfo
/// </summary>
public class ValidateAssociateExistsAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var dbFactory = validationContext.GetService<IDbContextFactory<AuthResetDbContext>>();
        var al = (AssociateLine)validationContext.ObjectInstance;

        using var context = dbFactory!.CreateDbContext();

        // FK Check: Does Associate exist?
        if (!context.AssociateInfo.Any(a => a.AssocNum == al.AssocNum))
            return new ValidationResult($"Associate #{al.AssocNum} does not exist.", [nameof(AssociateLine.AssocNum)]);

        return ValidationResult.Success;
    }
}

/// <summary>
/// Verify that a line exists in CmmsToLineName
/// </summary>
public class ValidateLineExistsAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var dbFactory = validationContext.GetService<IDbContextFactory<AuthResetDbContext>>();
        var al = (AssociateLine)validationContext.ObjectInstance;

        using var context = dbFactory!.CreateDbContext();

        // FK Check: Does Line exist?
        if (!context.CmmsToLineName.Any(l => l.LineName == al.Line))
            return new ValidationResult($"Line '{al.Line}' is not valid. If it should be, please add it in the 'Update CMMS' section", [nameof(AssociateLine.Line)]);

        return ValidationResult.Success;
    }
}

/// <summary>
/// Verify that a target associate and line are not already linked
/// </summary>
public class ValidateLineAssignedToAssociateAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var dbFactory = validationContext.GetService<IDbContextFactory<AuthResetDbContext>>();
        var al = (AssociateLine)validationContext.ObjectInstance;

        using var context = dbFactory!.CreateDbContext();

        // PK Check: Is this pair already linked?
        if (context.AssociateToLine.Any(x => x.AssocNum == al.AssocNum && x.Line == al.Line))
            return new ValidationResult("This associate is already assigned to this line.", [nameof(AssociateLine.Line)]);

        return ValidationResult.Success;
    }
}

/// <summary>
/// Marks a property that should not be displayed in UniversalTable
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class NotDisplayedAttribute : Attribute
{
}