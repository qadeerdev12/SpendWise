using Microsoft.EntityFrameworkCore;
using SpendWise.Web.Data;
using SpendWise.Web.Security;

namespace SpendWise.Web.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddSpendWiseServices(this IServiceCollection services, string connectionString)
    {
        // A scoped factory keeps Identity's scoped options compatible. Each service still
        // creates and disposes a NEW context; it never retains Identity's request context.
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connectionString),
            ServiceLifetime.Scoped);
        services.AddScoped<ICurrentUser, AuthenticatedCurrentUser>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<CategoryInitializer>();
        services.AddScoped<CategoryService>();
        services.AddScoped<ExpenseService>();
        services.AddScoped<AccountDeletionService>();
        return services;
    }
}
