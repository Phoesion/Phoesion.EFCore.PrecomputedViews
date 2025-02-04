using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Phoesion.EFCore.PrecomputedViews
{
    [Flags]
    public enum DependencyEvents
    {
        None = 0,
        Added = 1 << 0,
        Removed = 1 << 1,
        Modified = 1 << 2,
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public abstract class DependsOnAttribute : Attribute
    {
        public abstract Type DependencyType { get; }
        public DependencyEvents Events { get; }
        public Type HandlerType { get; }

        public DependsOnAttribute(DependencyEvents events, Type handlerType)
        {
            this.Events = events;
            this.HandlerType = handlerType;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class DependsOnAttribute<T> : DependsOnAttribute
    {
        public override Type DependencyType => typeof(T);
        public DependsOnAttribute(DependencyEvents events) : base(events, null) { }
        public DependsOnAttribute(DependencyEvents events, Type handlerType) : base(events, handlerType) { }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public abstract class PrecomputedViewAttribute : Attribute
    {
        public PrecomputedViewAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public class PrecomputedViewAttribute<T> : PrecomputedViewAttribute where T : IComputableView
    {
        public PrecomputedViewAttribute() : base() { }
    }
}
