using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Phoesion.EFCore.PrecomputedViews
{
    /// <summary>
    /// Options to configure for Phoesion.EFCore.PrecomputedViews interceptor
    /// </summary>
    public sealed class PrecomputedViewsOptions
    {
        /// <summary>
        /// Record used to define a dependency
        /// </summary>
        /// <param name="BaseType">The type that has a dependency (to another type)</param>
        /// <param name="DependencyType">The type that the baseType depended on</param>
        /// <param name="Events">The events that will trigger this dependency</param>
        /// <param name="HandlerType">The view-compute handler type</param>
        public record DependencyEntry(Type BaseType, Type DependencyType, DependencyEvents Events, Type HandlerType);

        /// <summary>
        /// Register extra dependencies
        /// </summary>
        public List<DependencyEntry> ExtraDependencies { get; set; }


        //Helper functions
        public PrecomputedViewsOptions HasDependency<TBaseEntity, TDependency>(DependencyEvents events, Type handlerType)
        {
            ExtraDependencies ??= new();
            ExtraDependencies.Add(new DependencyEntry(typeof(TBaseEntity), typeof(TDependency), events, handlerType));
            return this;
        }
    }
}
