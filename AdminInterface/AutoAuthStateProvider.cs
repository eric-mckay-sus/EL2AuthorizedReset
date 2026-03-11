using Microsoft.AspNetCore.Components.Authorization;

namespace AdminInterface;
public class AutoAuthStateProvider(IUserIdentityService identityService) : AuthenticationStateProvider
{
    /// <summary>
    /// Wraps the ClaimsPrincipal (encoding the user's identity) in an AuthenticationState
    /// </summary>
    /// <returns>The authentication state of the current user</returns>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var principal = await identityService.GetUserPrincipalAsync();
        return new AuthenticationState(principal);
    }
}