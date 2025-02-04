using ConsoleAppSample2.DBModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConsoleAppSample2
{
    public static class Program
    {
        static async Task Main(string[] args)
        {
            //delete old file, will regenerate in MigrateAsync()
            if (File.Exists("ConsoleAppSample2.db"))
                File.Delete("ConsoleAppSample2.db");

            //run
            await using (var db = new dbContext())
            {
                //apply migrations
                await db.Database.MigrateAsync();

                // Create student
                var account1 = new Account() { Id = 1, Name = "System" }.WithView(db);
                var account2 = new Account() { Id = 2, Name = "George Papadopoulos", }.WithView(db);
                var account3 = new Account() { Id = 3, Name = "John Doe", }.WithView(db);
                db.Add(account1);
                db.Add(account2);
                db.Add(account3);
                await db.SaveChangesAsync();

                //add transactions
                AddTranscation(db, fromId: 1, toId: 2, amount: 1000);
                AddTranscation(db, fromId: 2, toId: 3, amount: 300);
                await db.SaveChangesAsync(); // <-- will trigger interceptor
            }

            //Print results
            Console.WriteLine("Results for step 1 :");
            await using (var db = new dbContext())
            {
                //Get from account 1
                var res1 = await db.Account.Where(c => c.Id == 1).Select(c => c.View.Balance).FirstOrDefaultAsync();
                Console.WriteLine($"Account1.Balance = {res1}");

                //Get from account 2
                var res2 = await db.Account.Where(c => c.Id == 2).Select(c => c.View.Balance).FirstOrDefaultAsync();
                Console.WriteLine($"Account2.Balance = {res2}");

                //Get from account 3
                var res3 = await db.Account.Where(c => c.Id == 3).Select(c => c.View.Balance).FirstOrDefaultAsync();
                Console.WriteLine($"Account3.Balance = {res3}");
            }

            //add more transactions
            await using (var db = new dbContext())
            {
                //add transactions
                AddTranscation(db, fromId: 3, toId: 2, amount: 50);
                await db.SaveChangesAsync(); // <-- will trigger interceptor
            }

            //Print results
            Console.WriteLine("");
            Console.WriteLine("Results for step 2 :");
            await using (var db = new dbContext())
            {
                var res1 = await db.Account.Where(c => c.Id == 1).Select(c => c.View.Balance).FirstOrDefaultAsync();
                Console.WriteLine($"Account1.Balance = {res1}");

                //Get from account 2
                var res2 = await db.Account.Where(c => c.Id == 2).Select(c => c.View.Balance).FirstOrDefaultAsync();
                Console.WriteLine($"Account2.Balance = {res2}");

                //Get from account 3
                var res3 = await db.Account.Where(c => c.Id == 3).Select(c => c.View.Balance).FirstOrDefaultAsync();
                Console.WriteLine($"Account3.Balance = {res3}");
            }

            //done
            Console.WriteLine("");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        //helper for adding double-entry transactions
        public static void AddTranscation(DbContext db, long fromId, long toId, decimal amount)
        {
            db.Add(new Transaction() { AccountId = toId, TransactionType = Transaction.TransactionTypes.Credit, Amount = amount, RefAccountId = fromId });
            db.Add(new Transaction() { AccountId = fromId, TransactionType = Transaction.TransactionTypes.Debit, Amount = amount, RefAccountId = toId });
        }

        //add view to a new account
        public static Account WithView(this Account account, DbContext db)
        {
            //add new view
            var view = new AccountView() { Account = account };
            db.Add(view);
            //assign to account
            account.View = view;
            return account;
        }
    }
}
