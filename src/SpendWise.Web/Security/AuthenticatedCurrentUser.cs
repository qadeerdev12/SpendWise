using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace SpendWise.Web.Security;

public sealed class AuthenticatedCurrentUser(AuthenticationStateProvider authentication) : ICurrentUser
{
    public async Task<string> GetRequiredUserIdAsync()
    {
        var principal = (await authentication.GetAuthenticationStateAsync()).User;
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        // A claim alone is not sufficient: the principal must also be authenticated.
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id))
            throw new UnauthorizedAccessException("Sign in to continue.");
        return id;
    }
}
