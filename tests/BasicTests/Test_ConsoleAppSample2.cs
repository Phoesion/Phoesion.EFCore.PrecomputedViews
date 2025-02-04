using ConsoleAppSample2;
using ConsoleAppSample2.DBModels;
using Microsoft.EntityFrameworkCore;

namespace BasicTests
{
    public class Test_ConsoleAppSample2
    {
        [Fact]
        public async Task Test()
        {
            const string dbFileName = "Test_ConsoleAppSample2.db";

            //delete old file, will regenerate in MigrateAsync()
            if (File.Exists(dbFileName))
                File.Delete(dbFileName);

            //run
            await using (var db = new dbContext(dbFileName))
            {
                //apply migrations
                await db.Database.MigrateAsync();

                // Create student
                var account1 = new Account() { Id = 1, Name = "System" }.WithView(db);
                var account2 = new Account() { Id = 2, Name = "George Pompadours", }.WithView(db);
                var account3 = new Account() { Id = 3, Name = "John Doe", }.WithView(db);
                db.Add(account1);
                db.Add(account2);
                db.Add(account3);
                await db.SaveChangesAsync();

                //add transactions
                Program.AddTranscation(db, fromId: 1, toId: 2, amount: 1000);
                Program.AddTranscation(db, fromId: 2, toId: 3, amount: 300);
                await db.SaveChangesAsync(); // <-- will trigger interceptor
            }

            //Print results
            await using (var db = new dbContext(dbFileName))
            {
                //Get from account 1
                var res1 = await db.Account.Where(c => c.Id == 1).Select(c => c.View.Balance).FirstOrDefaultAsync();
                Assert.Equal(res1, -1000);

                //Get from account 2
                var res2 = await db.Account.Where(c => c.Id == 2).Select(c => c.View.Balance).FirstOrDefaultAsync();
                Assert.Equal(res2, 700);

                //Get from account 3
                var res3 = await db.Account.Where(c => c.Id == 3).Select(c => c.View.Balance).FirstOrDefaultAsync();
                Assert.Equal(res3, 300);
            }

            //add more transactions
            await using (var db = new dbContext(dbFileName))
            {
                //add transactions
                Program.AddTranscation(db, fromId: 3, toId: 2, amount: 50);
                await db.SaveChangesAsync(); // <-- will trigger interceptor
            }

            //Print results
            await using (var db = new dbContext(dbFileName))
            {
                var res1 = await db.Account.Where(c => c.Id == 1).Select(c => c.View.Balance).FirstOrDefaultAsync();
                Assert.Equal(res1, -1000);

                //Get from account 2
                var res2 = await db.Account.Where(c => c.Id == 2).Select(c => c.View.Balance).FirstOrDefaultAsync();
                Assert.Equal(res2, 750);

                //Get from account 3
                var res3 = await db.Account.Where(c => c.Id == 3).Select(c => c.View.Balance).FirstOrDefaultAsync();
                Assert.Equal(res3, 250);
            }
        }
    }
}

