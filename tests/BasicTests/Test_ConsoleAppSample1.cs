using ConsoleAppSample1;
using ConsoleAppSample1.DBModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BasicTests
{
    public class Test_ConsoleAppSample1
    {
        [Fact]
        public async Task Test()
        {
            const string dbFileName = "Test_ConsoleAppSample1.db";

            //delete old file, will regenerate in MigrateAsync()
            if (File.Exists(dbFileName))
                File.Delete(dbFileName);

            //setup host
            var builder = Host.CreateApplicationBuilder([]);

            //comment this line to show logs
            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            //add db context
            builder.Services.AddDbContext<dbContext>(optionsBuilder =>
            {
                //setup SQLite
                optionsBuilder.UseSqlite($"Data Source={dbFileName}");

                //add pre-computed views interceptor
                optionsBuilder.AddPrecomputedViews();
            });

            //build and start
            var host = builder.Build();
            var services = host.Services;
            await host.StartAsync();

            //run
            await using (var scope = services.CreateAsyncScope())
            {
                //get db context
                var db = scope.ServiceProvider.GetService<dbContext>();

                //apply migrations
                await db.Database.MigrateAsync();

                // Create student
                var student1 = new Student() { Id = 1, FirstMidName = "George", LastName = "Papadopoulos", };
                var student2 = new Student() { Id = 2, FirstMidName = "John", LastName = "Doe", };
                db.Add(student1);
                db.Add(student2);
                await db.SaveChangesAsync();

                // create course
                var course1 = new Course() { Id = 1, Title = "C#" };
                var course2 = new Course() { Id = 2, Title = "Java" };
                var course3 = new Course() { Id = 3, Title = "Python" };
                db.Add(course1);
                db.Add(course2);
                db.Add(course3);
                await db.SaveChangesAsync();

                //add student to course 1
                db.Add(new Enrollment() { Student = student1, Course = course1 });
                db.Add(new Enrollment() { Student = student2, Course = course1 });
                await db.SaveChangesAsync(); // <-- will trigger interceptor

                //add student to course 2
                db.Add(new Enrollment() { Student = student1, Course = course2 });
                await db.SaveChangesAsync(); // <-- will trigger interceptor
            }

            //Print results
            await using (var scope = services.CreateAsyncScope())
            {
                //get db context
                var db = scope.ServiceProvider.GetService<dbContext>();

                //Get from Course 1
                var res1 = await db.Courses.Where(c => c.Id == 1).Select(c => c.LatestEnrollmentIdAddedId).FirstOrDefaultAsync();
                Assert.Equal(res1, 2);

                //Get from Course 2
                var res2 = await db.Courses.Where(c => c.Id == 2).Select(c => c.LatestEnrollmentIdAddedId).FirstOrDefaultAsync();
                Assert.Equal(res2, 3);

                //Get from Course 3
                var res3 = await db.Courses.Where(c => c.Id == 3).Select(c => c.LatestEnrollmentIdAddedId).FirstOrDefaultAsync();
                Assert.Equal(res3, null);

                //Get from Student 1
                var res4 = await db.Students.Where(c => c.Id == 1).Select(c => c.EnrollmentCount).FirstOrDefaultAsync();
                Assert.Equal(res4, 2);
            }
        }
    }
}

