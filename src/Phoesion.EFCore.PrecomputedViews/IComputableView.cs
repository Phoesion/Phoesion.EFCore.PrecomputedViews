using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Phoesion.EFCore.PrecomputedViews
{
    public interface IComputableView
    {
    }

    public interface IComputableView<TDbContext, TEntity> : IComputableView where TDbContext : DbContext
    {
        ValueTask ComputeView(TDbContext context, DependencyEvents ev, IEnumerable<TEntity> changedDependencies);
    }
}
