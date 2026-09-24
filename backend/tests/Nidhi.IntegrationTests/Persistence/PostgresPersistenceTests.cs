using Microsoft.EntityFrameworkCore;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;
using Nidhi.Infrastructure.Persistence;
using Npgsql;
using Xunit;

namespace Nidhi.IntegrationTests.Persistence;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NIDHI_TEST_POSTGRES")))
            Skip = "Set NIDHI_TEST_POSTGRES to a local PostgreSQL 18 connection with CREATEDB permission.";
    }
}

public sealed class PostgresPersistenceTests
{
    // This test owns only a random database; never drops the database supplied in the connection string.
    [PostgresFact]
    public async Task Migrations_constraints_and_xmin_work_on_clean_PostgreSQL()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NIDHI_TEST_POSTGRES"));
        var database = "nidhi_test_" + Guid.NewGuid().ToString("N");
        settings.Database = "postgres";
        await using var admin = new NpgsqlConnection(settings.ConnectionString);
        await admin.OpenAsync();
        Assert.Equal(18, admin.PostgreSqlVersion.Major);
        async Task AdminSql(string sql) => await new NpgsqlCommand(sql, admin).ExecuteNonQueryAsync();
        settings.Database = database;
        settings.Pooling = false;
        var options = new DbContextOptionsBuilder<NidhiDbContext>().UseNpgsql(settings.ConnectionString).Options;
        try
        {
            for (var round = 0; round < 2; round++)
            {
                await AdminSql($"CREATE DATABASE {database}");
                await using (var db = new NidhiDbContext(options))
                {
                    await db.Database.MigrateAsync();
                    Assert.Single(await db.Database.GetAppliedMigrationsAsync());
                    Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                    await Validate(db, options);
                }
                await AdminSql($"DROP DATABASE {database} WITH (FORCE)");
            }
        }
        finally { await AdminSql($"DROP DATABASE IF EXISTS {database} WITH (FORCE)"); }
    }

    private static async Task Validate(NidhiDbContext db, DbContextOptions<NidhiDbContext> options)
    {
        var now = DateTime.UtcNow;
        var user = new ApplicationUser();
        db.Users.Add(user);
        db.CustomerProfiles.Add(new CustomerProfile(user.Id, "Test", now, null));
        var wallet = new Wallet(user.Id, 0m, now, null);
        db.Wallets.Add(wallet);
        db.GoldHoldings.Add(new GoldHolding(user.Id, 0m, now, null));
        await db.SaveChangesAsync();
        Assert.NotEqual(0u, wallet.ConcurrencyToken);
        await using var other = new NidhiDbContext(options);
        var stale = await other.Wallets.SingleAsync();
        db.Entry(wallet).Property(x => x.BalanceLkr).CurrentValue = 100m;
        await db.SaveChangesAsync();
        other.Entry(stale).Property(x => x.BalanceLkr).CurrentValue = 200m;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());

        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        async Task Reject(string sql, string code)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("customer", user.Id);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(code, error.SqlState);
        }
        await Reject("UPDATE wallets SET balance_lkr = -0.01", PostgresErrorCodes.CheckViolation);
        await Reject("UPDATE wallets SET balance_lkr = 5000000.01", PostgresErrorCodes.CheckViolation);
        await Reject("UPDATE gold_holdings SET quantity_grams = -0.00000001", PostgresErrorCodes.CheckViolation);
        await Reject("INSERT INTO wallets(id,customer_id,balance_lkr,created_at_utc) VALUES(gen_random_uuid(),@customer,0,now())", PostgresErrorCodes.UniqueViolation);
        db.SavingsGoals.Add(new SavingsGoal(user.Id, 1m, null, GoalStatus.ACTIVE, now, null));
        db.IdempotencyRecords.Add(new IdempotencyRecord(user.Id, IdempotencyOperation.SimulateFunding, "key", new string('a', 64), null, IdempotencyStatus.IN_PROGRESS, now, null));
        await db.SaveChangesAsync();
        await Reject("INSERT INTO savings_goals(id,customer_id,target_grams,status,created_at_utc) VALUES(gen_random_uuid(),@customer,1,'ACTIVE',now())", PostgresErrorCodes.UniqueViolation);
        await Reject("INSERT INTO idempotency_records(id,customer_id,operation,idempotency_key,request_hash,status,created_at_utc) VALUES(gen_random_uuid(),@customer,'SimulateFunding','key','hash','IN_PROGRESS',now())", PostgresErrorCodes.UniqueViolation);
        await Reject("INSERT INTO gold_prices(id,price_per_gram_lkr,published_at_utc,published_by_admin_id,reason,audit_log_id) VALUES(gen_random_uuid(),0,now(),@customer,'test',gen_random_uuid())", PostgresErrorCodes.CheckViolation);
        await Reject("INSERT INTO financial_transactions(id,customer_id,type,status,amount_lkr,post_wallet_balance_lkr,idempotency_record_id,created_at_utc) SELECT gen_random_uuid(),@customer,'GOLD_PURCHASE','COMPLETED',100,0,id,now() FROM idempotency_records", PostgresErrorCodes.CheckViolation);
        await Reject("INSERT INTO financial_transactions(id,customer_id,type,status,amount_lkr,post_wallet_balance_lkr,idempotency_record_id,created_at_utc) SELECT gen_random_uuid(),@customer,'WALLET_FUNDING','COMPLETED',99,0,id,now() FROM idempotency_records", PostgresErrorCodes.CheckViolation);
        var idempotency = await db.IdempotencyRecords.SingleAsync();
        var funding = new FinancialTransaction(user.Id, TransactionType.WALLET_FUNDING, TransactionStatus.COMPLETED,
            100m, null, null, null, 100m, null, idempotency.IdempotencyRecordId, now);
        var account = new LedgerAccount("test-lkr", "Test wallet", LedgerUnit.LKR, AccountClassification.LIABILITY, user.Id, now);
        db.FinancialTransactions.Add(funding);
        db.LedgerAccounts.Add(account);
        await db.SaveChangesAsync();
        db.LedgerEntries.Add(new LedgerEntry(funding.TransactionId, account.AccountId, LedgerUnit.LKR, EntryDirection.CREDIT, 100m, now));
        await db.SaveChangesAsync();
        await Reject("UPDATE ledger_entries SET amount = 0", PostgresErrorCodes.CheckViolation);
        await Reject("UPDATE ledger_entries SET direction = 'UNKNOWN'", PostgresErrorCodes.CheckViolation);
        await Reject("UPDATE ledger_entries SET unit = 'UNKNOWN'", PostgresErrorCodes.CheckViolation);
        await Reject("UPDATE ledger_entries SET unit = 'GOLD_GRAMS'", PostgresErrorCodes.ForeignKeyViolation);
        await Reject("UPDATE ledger_accounts SET unit = 'UNKNOWN'", PostgresErrorCodes.CheckViolation);
        await Reject("UPDATE financial_transactions SET amount_lkr = 1000000.01", PostgresErrorCodes.CheckViolation);
        await Reject("UPDATE financial_transactions SET gold_quantity_grams = 1", PostgresErrorCodes.CheckViolation);
        await Reject("UPDATE savings_goals SET target_grams = 0", PostgresErrorCodes.CheckViolation);
        // Inactive history may coexist with the one active goal.
        db.SavingsGoals.Add(new SavingsGoal(user.Id, 2m, null, GoalStatus.REPLACED, now, now));
        // The same key is valid for a distinct operation.
        db.IdempotencyRecords.Add(new IdempotencyRecord(user.Id, IdempotencyOperation.SaveGold, "key", new string('b', 64), null, IdempotencyStatus.IN_PROGRESS, now, null));
        await db.SaveChangesAsync();
        var purchaseKey = await db.IdempotencyRecords.SingleAsync(x => x.Operation == IdempotencyOperation.SaveGold);
        var price = new GoldPrice(100m, now, user.Id, "Test price", Guid.CreateVersion7());
        db.GoldPrices.Add(price);
        var purchase = new FinancialTransaction(user.Id, TransactionType.GOLD_PURCHASE, TransactionStatus.COMPLETED,
            100m, 1m, price.PriceVersionId, 100m, 0m, 1m, purchaseKey.IdempotencyRecordId, now);
        db.FinancialTransactions.Add(purchase);
        await db.SaveChangesAsync();
        foreach (var column in new[] { "gold_quantity_grams", "price_version_id", "applied_price_per_gram_lkr", "post_gold_holding_grams" })
            await Reject($"UPDATE financial_transactions SET {column} = NULL WHERE type = 'GOLD_PURCHASE'", PostgresErrorCodes.CheckViolation);
        var holding = await db.GoldHoldings.SingleAsync();
        var staleHolding = await other.GoldHoldings.SingleAsync();
        db.Entry(holding).Property(x => x.QuantityGrams).CurrentValue = 1m;
        await db.SaveChangesAsync();
        other.ChangeTracker.Clear();
        other.Attach(staleHolding);
        other.Entry(staleHolding).Property(x => x.QuantityGrams).CurrentValue = 2m;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
        await using var schema = new NpgsqlCommand("SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND (column_name='xmin' OR column_name LIKE '%residual%')", connection);
        Assert.Equal(0L, await schema.ExecuteScalarAsync());
        await using var tables = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE'", connection);
        Assert.Equal(18L, await tables.ExecuteScalarAsync()); // 10 domain + 7 Identity + EF history
    }
}
