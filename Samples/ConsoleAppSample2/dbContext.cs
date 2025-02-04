using ConsoleAppSample2.DBModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleAppSample2
{
    public class dbContext : DbContext
    {
        string dbFileName { get; set; } = "ConsoleAppSample2.db";

        public DbSet<Account> Account { get; set; }
        public DbSet<AccountView> AccountViews { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        public dbContext() : base() { }
        public dbContext(string dbFileName) : base() { this.dbFileName = dbFileName; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //setup SQLite
            optionsBuilder.UseSqlite($"Data Source={dbFileName}");

            //add pre-computed views interceptor
            optionsBuilder.AddPrecomputedViews();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>(o =>
            {
                o.HasMany(x => x.FromTransactions)
                 .WithOne(x => x.RefAccount)
                 .HasForeignKey(x => x.RefAccountId)
                 .OnDelete(DeleteBehavior.Restrict);

                o.HasMany(x => x.ToTransactions)
                 .WithOne(x => x.Account)
                 .HasForeignKey(x => x.AccountId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AccountView>(o =>
            {
                o.HasOne(x => x.Account)
                 .WithOne(x => x.View)
                 .HasForeignKey<AccountView>(x => x.Id)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Transaction>(o =>
            {
                o.HasIndex(x => new { x.RefAccountId, x.TransactionType });
            });
        }
    }
}
