using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace AdminInterface;
public class AutoAuthStateProvider(IDbContextFactory<AuthResetDbContext> dbFactory) : AuthenticationStateProvider
{
    private readonly IDbContextFactory<AuthResetDbContext> _dbFactory = dbFactory;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Get system name (e.g., "CORP\JDoe1234")
        string fullUserName = Environment.UserName; 
        
        // Extract last 4 digits as associate number
        if (fullUserName.Length >= 4 && int.TryParse(fullUserName[^4..], out int assocNum))
        {
            Console.WriteLine(fullUserName);
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