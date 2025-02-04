using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Phoesion.EFCore.PrecomputedViews
{
    sealed class DBContextInfo
    {
        public readonly Dictionary<Type, List<(DependencyEvents, Type)>> InverseDependenciesCache = new();
    }
}
