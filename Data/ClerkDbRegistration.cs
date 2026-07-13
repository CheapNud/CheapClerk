using CheapClerk.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CheapClerk.Data;

public static class ClerkDbRegistration
{
    public static IServiceCollection AddClerkDb(this IServiceCollection services, CacheOptions cacheOptions)
    {
        if (string.Equals(cacheOptions.Provider, "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(cacheOptions.ConnectionString))
                throw new InvalidOperationException("Cache provider 'Postgres' requires Cache:ConnectionString.");
            services.AddDbContextFactory<ClerkDbContext>(dbOpt => dbOpt.UseNpgsql(cacheOptions.ConnectionString));
        }
        else if (string.Equals(cacheOptions.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContextFactory<ClerkDbContext>(dbOpt => dbOpt.UseSqlite($"Data Source={cacheOptions.DatabasePath}"));
        }
        else
        {
            throw new InvalidOperationException($"Unknown cache provider '{cacheOptions.Provider}'. Valid options: Sqlite, Postgres.");
        }
        return services;
    }
}
