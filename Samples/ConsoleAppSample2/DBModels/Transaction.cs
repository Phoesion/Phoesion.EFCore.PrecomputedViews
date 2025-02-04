using Phoesion.EFCore.PrecomputedViews;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ConsoleAppSample2.DBModels
{
    public class Transaction
    {
        [Key]
        public long Id { get; set; }

        public enum TransactionTypes
        {
            Credit = 0,
            Debit = 1,
        }
        public TransactionTypes TransactionType { get; set; }

        public decimal Amount { get; set; }

        public long AccountId { get; set; }
        public Account Account { get; set; }

        public long RefAccountId { get; set; }
        public Account RefAccount { get; set; }

    }
}
