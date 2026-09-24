using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Persistence;
using Xunit;

namespace Nidhi.IntegrationTests.Persistence;

public sealed class PersistenceModelTests
{
    private static NidhiDbContext Context() => new(new DbContextOptionsBuilder<NidhiDbContext>()
        .UseNpgsql("Host=localhost;Database=model_only").Options);

    [Fact]
    public void Financial_storage_preserves_precision_and_has_no_residual()
    {
        using var context = Context();
        var model = context.Model;
        foreach (var entity in model.GetEntityTypes())
        {
            Assert.DoesNotContain(entity.GetProperties(), p => p.Name.Contains("Residual", StringComparison.OrdinalIgnoreCase));
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                var expected = property.Name.Contains("PricePerGram") ? (18, 4)
                    : property.Name.Contains("Grams") || entity.ClrType == typeof(LedgerEntry) ? (20, 8) : (18, 2);
                Assert.Equal(expected.Item1, property.GetPrecision());
                Assert.Equal(expected.Item2, property.GetScale());
            }
        }
    }

    [Theory]
    [InlineData(typeof(Wallet))]
    [InlineData(typeof(GoldHolding))]
    public void Concurrency_uses_system_xmin(Type type)
    {
        using var context = Context();
        var property = context.Model.FindEntityType(type)!.FindProperty("ConcurrencyToken")!;
        Assert.Equal(typeof(uint), property.ClrType);
        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
        Assert.Equal("xmin", property.GetColumnName());
        Assert.Equal("xid", property.GetColumnType());
        Assert.DoesNotContain("xmin", context.Database.GenerateCreateScript(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xmin", context.GetService<IMigrator>().GenerateScript(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Uniqueness_and_relationships_match_financial_scopes()
    {
        using var context = Context();
        var model = context.GetService<IDesignTimeModel>().Model;
        var scope = model.FindEntityType(typeof(IdempotencyRecord))!.GetIndexes()
            .Single(i => i.GetDatabaseName() == "UK_idempotency_scoped_key");
        Assert.True(scope.IsUnique);
        Assert.Equal(new[] { "CustomerId", "Operation", "IdempotencyKey" }, scope.Properties.Select(p => p.Name));
        var active = model.FindEntityType(typeof(SavingsGoal))!.GetIndexes().Single(i => i.IsUnique);
        Assert.Equal("status = 'ACTIVE'", active.GetFilter());
        var ledger = model.FindEntityType(typeof(LedgerEntry))!;
        Assert.Contains(ledger.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(LedgerAccount)
            && f.Properties.Select(p => p.Name).SequenceEqual(new[] { "AccountId", "Unit" }));
        Assert.All(model.GetEntityTypes().Where(e => e.ClrType.Namespace == typeof(Wallet).Namespace)
            .SelectMany(e => e.GetForeignKeys()), f => Assert.Equal(DeleteBehavior.Restrict, f.DeleteBehavior));
        Assert.DoesNotContain(model.FindEntityType(typeof(CustomerProfile))!.GetProperties(), p => p.Name.Contains("Email"));
    }
}
