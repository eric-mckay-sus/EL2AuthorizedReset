using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdminInterface;

public class AutoAuthenticationHandler(
    IOptionsMonitor<AutoAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISystemClock clock,
    IDbContextFactory<AuthResetDbContext> dbFactory) : AuthenticationHandler<AutoAuthenticationOptions>(options, logger, encoder, clock)
{
    private readonly IDbContextFactory<AuthResetDbContext> _dbFactory = dbFactory;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            // Get system name (e.g., "CORP\JDoe1234")
            string fullUserName = Environment.UserName;

            // Extract last 4 digits as associate number
            if (fullUserName.Length >= 4 && int.TryParse(fullUserName[^4..], out int assocNum))
            {
                using var context = _dbFactory.CreateDbContext();
                var associate = await context.Set<Associate>()
                    .FirstOrDefaultAsync(a => a.AssocNum == assocNum);

                if (associate != null)
                {
                    // Create Identity with claims
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.Name, associate.Name ?? ""),
                        new("AssocNum", assocNum.ToString())
                    };

                    if (associate.IsAdmin)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                    }

                    var identity = new ClaimsIdentity(claims, Scheme.Name);
                    var principal = new ClaimsPrincipal(identity);
                    var ticket = new AuthenticationTicket(principal, Scheme.Name);

                    return AuthenticateResult.Success(ticket);
                }
            }

            // Return failure if not found in DB
            return AuthenticateResult.NoResult();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during authentication");
            return AuthenticateResult.NoResult();
        }
    }
}

public class AutoAuthenticationOptions : AuthenticationSchemeOptions
{
}

public static class AutoAuthenticationExtensions
{
    public static AuthenticationBuilder AddAutoAuthentication(this AuthenticationBuilder builder)
    {
        return builder.AddScheme<AutoAuthenticationOptions, AutoAuthenticationHandler>(
            "AutoAuth", options => { });
    }
}
