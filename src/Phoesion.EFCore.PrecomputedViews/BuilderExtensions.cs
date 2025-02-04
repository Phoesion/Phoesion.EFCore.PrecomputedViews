using Microsoft.EntityFrameworkCore;
using Phoesion.EFCore.PrecomputedViews;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary> Builder extensions </summary>
    public static class BuilderExtensions
    {
        /// <summary>
        /// Add Phoesion.EFCore.PrecomputedViews interceptor to the DBContext.
        /// https://github.com/Phoesion/Phoesion.EFCore.PrecomputedViews
        /// </summary>
        public static void AddPrecomputedViews(this DbContextOptionsBuilder builder)
        {
            builder.AddInterceptors(new PrecomputedViewInterceptor());
        }

        /// <summary>
        /// Add Phoesion.EFCore.PrecomputedViews interceptor to the DBContext.
        /// https://github.com/Phoesion/Phoesion.EFCore.PrecomputedViews
        /// </summary>
        public static void AddPrecomputedViews(this DbContextOptionsBuilder builder, PrecomputedViewsOptions options)
        {
            builder.AddInterceptors(new PrecomputedViewInterceptor(options));
        }
    }
}
