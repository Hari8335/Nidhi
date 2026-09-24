using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nidhi.Infrastructure.Persistence;

namespace Nidhi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NidhiDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("NidhiDb")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:NidhiDb before using persistence.")));
        return services;
    }
}
