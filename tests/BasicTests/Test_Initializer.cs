using ConsoleAppSample2;
using ConsoleAppSample2.DBModels;
using Microsoft.EntityFrameworkCore;
using Phoesion.EFCore.PrecomputedViews;

namespace BasicTests
{
    public class Test_Initializer
    {
        [Fact]
        public async Task Test()
        {
            const int numUsers = 1000;
            const string dbFileName = "Test_ConsoleAppSample2_Initializer.db";

            //delete old file, will regenerate in MigrateAsync()
            if (File.Exists(dbFileName))
                File.Delete(dbFileName);

            //run
            await using (var db = new dbContext(dbFileName))
            {
                //apply migrations
                await db.Database.MigrateAsync();

                // Create accounts
                for (int n = 1; n < numUsers + 1; n++)
                {
                    var account = new Account() { Id = n, Name = "Test user " + n }.WithView(db);
                    db.Add(account);
                }
                await db.SaveChangesAsync();

                //add transactions
                for (int n = 1; n < numUsers + 1; n++)
                    for (int n2 = 0; n2 < 100; n2++)
                        Program.AddTranscation(db, fromId: n, toId: Random.Shared.Next(1, numUsers + 1), amount: Random.Shared.Next(10000));
                await db.SaveChangesAsync(); // <-- will trigger interceptor
            }

            //Get computed balances
            Dictionary<long, decimal> balances;
            await using (var db = new dbContext(dbFileName))
                balances = await db.Account.Select(x => new { x.Id, x.View.Balance }).ToDictionaryAsync(x => x.Id, x => x.Balance);

            //reset views
            await using (var db = new dbContext(dbFileName))
                await db.AccountViews.ExecuteUpdateAsync(v => v.SetProperty(x => x.Balance, 0));

            //run initializer
            await using (var db = new dbContext(dbFileName))
                await PrecomputedViewInitializer.InititializeViewAsync(
                    db,
                    db => db.AccountViews,
                    view => view.Id,
                    100,
                    async (dbctx, entities) => await entities.ExecuteUpdateAsync(e => e.SetProperty(
                                    x => x.Balance,
                                    x => dbctx.Transactions
                                                    .Where(transaction => transaction.AccountId == x.Id)
                                                    .Select(transaction => transaction.TransactionType == Transaction.TransactionTypes.Credit ? transaction.Amount : -transaction.Amount)
                                                    .Sum()))
            );

            //Get newly computed balances
            Dictionary<long, decimal> newBalances;
            await using (var db = new dbContext(dbFileName))
                newBalances = await db.Account.Select(x => new { x.Id, x.View.Balance }).ToDictionaryAsync(x => x.Id, x => x.Balance);

            //examine for match - count
            if (newBalances.Count != balances.Count)
                Assert.Fail("Mismatch in number of accounts");

            //examine for match - values
            foreach (var kvp in balances)
            {
                if (!newBalances.TryGetValue(kvp.Key, out var newBalance))
                    Assert.Fail($"Account {kvp.Key} not found in new balances");
                if (newBalance != kvp.Value)
                    Assert.Fail($"Account {kvp.Key} balance mismatch");
            }
        }
    }
}

