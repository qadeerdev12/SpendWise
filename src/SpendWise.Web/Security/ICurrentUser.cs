namespace SpendWise.Web.Security;

/// <summary>Resolves identity from the server's authentication state, never form input.</summary>
public interface ICurrentUser
{
    Task<string> GetRequiredUserIdAsync();
}
