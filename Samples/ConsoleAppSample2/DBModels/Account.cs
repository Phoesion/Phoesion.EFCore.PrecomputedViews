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
    public class Account
    {
        [Key]
        public long Id { get; set; }

        public string Name { get; set; }

        //keep the View (pre-computed) data in a separate table/model
        public AccountView View { get; set; }

        //Back-reference
        public virtual ICollection<Transaction> FromTransactions { get; set; }
        public virtual ICollection<Transaction> ToTransactions { get; set; }
    }
}
