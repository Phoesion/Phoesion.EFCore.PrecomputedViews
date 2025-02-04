using ConsoleAppSample2;
using ConsoleAppSample2.DBModels;
using Microsoft.EntityFrameworkCore;

namespace BasicTests
{
    public class Test_ConsoleAppSample2_Synchronous
    {
        [Fact]
        public void Test()
        {
            const string dbFileName = "Test_ConsoleAppSample2_Synchronous.db";

            //delete old file, will regenerate in MigrateAsync()
            if (File.Exists(dbFileName))
                File.Delete(dbFileName);

            //run
            using (var db = new dbContext(dbFileName))
            {
                //apply migrations
                db.Database.Migrate();

                // Create student
                var account1 = new Account() { Id = 1, Name = "System" }.WithView(db);
                var account2 = new Account() { Id = 2, Name = "George Pompadours", }.WithView(db);
                var account3 = new Account() { Id = 3, Name = "John Doe", }.WithView(db);
                db.Add(account1);
                db.Add(account2);
                db.Add(account3);
                db.SaveChanges();

                //add transactions
                Program.AddTranscation(db, fromId: 1, toId: 2, amount: 1000);
                Program.AddTranscation(db, fromId: 2, toId: 3, amount: 300);
                db.SaveChanges(); // <-- will trigger interceptor
            }

            //Print results
            using (var db = new dbContext(dbFileName))
            {
                //Get from account 1
                var res1 = db.Account.Where(c => c.Id == 1).Select(c => c.View.Balance).FirstOrDefault();
                Assert.Equal(res1, -1000);

                //Get from account 2
                var res2 = db.Account.Where(c => c.Id == 2).Select(c => c.View.Balance).FirstOrDefault();
                Assert.Equal(res2, 700);

                //Get from account 3
                var res3 = db.Account.Where(c => c.Id == 3).Select(c => c.View.Balance).FirstOrDefault();
                Assert.Equal(res3, 300);
            }

            //add more transactions
            using (var db = new dbContext(dbFileName))
            {
                //add transactions
                Program.AddTranscation(db, fromId: 3, toId: 2, amount: 50);
                db.SaveChanges(); // <-- will trigger interceptor
            }

            //Print results
            using (var db = new dbContext(dbFileName))
            {
                var res1 = db.Account.Where(c => c.Id == 1).Select(c => c.View.Balance).FirstOrDefault();
                Assert.Equal(res1, -1000);

                //Get from account 2
                var res2 = db.Account.Where(c => c.Id == 2).Select(c => c.View.Balance).FirstOrDefault();
                Assert.Equal(res2, 750);

                //Get from account 3
                var res3 = db.Account.Where(c => c.Id == 3).Select(c => c.View.Balance).FirstOrDefault();
                Assert.Equal(res3, 250);
            }
        }
    }
}

