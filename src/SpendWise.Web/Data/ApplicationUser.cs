using Microsoft.AspNetCore.Identity;

namespace SpendWise.Web.Data;

public class ApplicationUser : IdentityUser
{
    // A durable marker prevents renamed/archived default categories reappearing on login.
    public bool CategoriesInitialized { get; set; }
}
