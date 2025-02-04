using ConsoleAppSample1.DBModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleAppSample1
{
    public class dbContext : DbContext
    {
        public DbSet<Course> Courses { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Student> Students { get; set; }

        public dbContext(DbContextOptions options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Course>(o =>
            {
                o.HasOne(e => e.LatestEnrollmentIdAdded)
                 .WithMany()
                 .HasForeignKey(e => e.LatestEnrollmentIdAddedId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Enrollment>();

            modelBuilder.Entity<Student>();
        }
    }
}
