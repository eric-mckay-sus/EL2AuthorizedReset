using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AdminInterface;

public interface IUserIdentityService
{
    Task<ClaimsPrincipal> GetUserPrincipalAsync();
}

public class UserIdentityService(IDbContextFactory<AuthResetDbContext> dbFactory, IMemoryCache cache) : IUserIdentityService
{
    // This is fragile, but more secure than simply getting the last 4-5 characters and parsing as an int
    private static readonly string[] stanleyPrefixes = ["SUSU", "SUSD"]; // The prefixes to strip from the username to get the associate number. Put shorter substrings (e.g. SUS for these two) near the end if needed.

    /// <summary>
    /// Gets the authentication state, which can be one of three things:
    /// Unauthenticated (associate number not in DB, HTTP 401 on attempted admin page access)
    /// Unauthorized (associate number in DB without admin privileges, HTTP 403 on attempted admin page access)
    /// Authorized (associate number in DB with admin privileges, successful navigation on admin page access)
    /// </summary>
    /// <returns>The authentication state of the current user</returns>
    public async Task<ClaimsPrincipal> GetUserPrincipalAsync()
    {
        // Get the username from the system (e.g. SUSU1057, SUSD5938)
        string associateString = Environment.UserName;
        string cacheKey = $"UserPrincipal_{associateString}";

        // If there's an identity stored in the cache, use that
        if (cache.TryGetValue(cacheKey, out ClaimsPrincipal? cachedPrincipal) && cachedPrincipal != null)
        {
            return cachedPrincipal;
        }

        // Check each prefix, attempting a replace on each one (hence the "shorter substring" restriction above)
        foreach (string prefix in stanleyPrefixes) 
            associateString = associateString.Replace(prefix, "");

        ClaimsPrincipal principal;
        // Extract associate number from remaining (hopefully all numeric) characters
        if (int.TryParse(associateString, out int assocNum))
        {
            using var context = dbFactory.CreateDbContext();
            var associate = await context.Set<Associate>()
                .FirstOrDefaultAsync(a => a.AssocNum == assocNum);

            if (associate != null)
            {
                // Create identifier, adding admin role if applicable
                var claims = new List<Claim> 
                { 
                    new(ClaimTypes.Name, associate.Name ?? ""),
                    new("AssocNum", assocNum.ToString()) 
                };

                if (associate.IsAdmin)
                {
                    claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                }

                ClaimsIdentity identity = new(claims, "AutoAuth");
                principal = new(identity);
            } else
            {
                principal = new(new ClaimsIdentity());
            }
        // If int.TryParse failed (prefix strip didn't get something that looked like associate number) or an associate with the parsed number wasn't found in the DB, return anonymous
        } else
        {
            principal = new(new ClaimsIdentity());
        }
        cache.Set(cacheKey, principal, TimeSpan.FromMinutes(15));

        return principal;
    }
}