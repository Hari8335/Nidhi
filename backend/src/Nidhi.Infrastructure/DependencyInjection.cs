using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nidhi.Application.Wallet;
using Nidhi.Infrastructure.Persistence;
using Nidhi.Infrastructure.WalletServices;

namespace Nidhi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NidhiDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("NidhiDb")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:NidhiDb before using persistence.")));
        services.AddScoped<Nidhi.Application.Admin.IAdminService, Nidhi.Infrastructure.Admin.AdminService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<Nidhi.Application.SavingsGoals.ISavingsGoalService, Nidhi.Infrastructure.SavingsGoals.SavingsGoalService>();
        services.AddScoped<Nidhi.Application.CustomerRecords.ICustomerReadService, Nidhi.Infrastructure.CustomerRecords.CustomerReadService>();
        return services;
    }
}
