using Microsoft.EntityFrameworkCore;
using Phoesion.EFCore.PrecomputedViews;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;

namespace ConsoleAppSample2.DBModels
{
    [DependsOn<Transaction>(DependencyEvents.Added)]
    public class AccountView : IComputableView<dbContext, Transaction>
    {
        [Key]
        public long Id { get; set; }

        public decimal Balance { get; set; }

        //back-reference
        public Account Account { get; set; }

        //compute view when a new transaction is added
        public async ValueTask ComputeView(dbContext context, DependencyEvents ev, IEnumerable<Transaction> changedDependencies)
        {
            //find accounts that are affected
            var accountIdsAffected = changedDependencies?.Select(x => x.AccountId);

            // limit search space to affected accounts only (otherwise the query will run on all account entities)
            var entities = accountIdsAffected != null ?
                                    context.AccountViews.Where(s => accountIdsAffected.Contains(s.Id)) :
                                    context.AccountViews;

            //execute update 
            await entities.ExecuteUpdateAsync(e => e.SetProperty(
                            x => x.Balance,
                            x => context.Transactions
                                            .Where(transaction => transaction.AccountId == x.Id)
                                            .Select(transaction => transaction.TransactionType == Transaction.TransactionTypes.Credit ? transaction.Amount : -transaction.Amount)
                                            .Sum()));
        }
    }
}
