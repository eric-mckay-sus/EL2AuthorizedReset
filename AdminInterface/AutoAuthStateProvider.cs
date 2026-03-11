using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace AdminInterface;
public class AutoAuthStateProvider(IDbContextFactory<AuthResetDbContext> dbFactory) : AuthenticationStateProvider
{
    private readonly IDbContextFactory<AuthResetDbContext> _dbFactory = dbFactory; // the DB context factory to generate a context for looking up the associate number
    private static readonly string[] stanleyPrefixes = ["SUSU", "SUSD"]; // The prefixes to strip from the username to get associate number. FIRST MATCH IS USED (e.g. if SUS is needed, add it AFTER everything longer that includes SUS)

    /// <summary>
    /// Gets the authentication state, which can be one of three things:
    /// Unauthenticated (associate number not in DB, HTTP 401 on attempted admin page access)
    /// Unauthorized (associate number in DB without admin privileges, HTTP 403 on attempted admin page access)
    /// Authorized (associate number in DB with admin privileges, successful navigation on admin page access)
    /// </summary>
    /// <returns>The authentication state of the current user</returns>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Get Windows username (e.g. SUSU4502, SUSD2946)
        string associateString = Environment.UserName;
        
        // Try each prefix, matching the first
        foreach(string prefix in stanleyPrefixes) associateString = associateString.Replace(prefix, "");
        
        // Extract last 4 digits as associate number
        if (int.TryParse(associateString, out int assocNum))
        {
            using var context = _dbFactory.CreateDbContext();
            var associate = await context.Set<Associate>()
                .FirstOrDefaultAsync(a => a.AssocNum == assocNum);

            if (associate != null)
            {
                // Create Identity, adding "Admin" role if isAdmin is true in DB
                var claims = new List<Claim> { 
                    new(ClaimTypes.Name, associate.Name),
                    new("AssocNum", assocNum.ToString()) 
                };

                if (associate.IsAdmin) // Assuming your Associate model has this bool
                {
                    claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                }

                var identity = new ClaimsIdentity(claims, "CustomAutoAuth");
                return new AuthenticationState(new ClaimsPrincipal(identity));
            }
        }

        // Return "Anonymous" if not found in DB
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }
}