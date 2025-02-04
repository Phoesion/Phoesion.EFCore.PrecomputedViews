using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Phoesion.EFCore.PrecomputedViews
{
    /// <summary>
    /// The EFCore interceptor that will detect dependencies and run the IComputableView handlers
    /// </summary>
    public class PrecomputedViewInterceptor : SaveChangesInterceptor
    {
        static readonly ConcurrentDictionary<Type, DBContextInfo> s_sharedDBContextInfo = new();

        delegate ValueTask handlerInvokerDelegate(DbContext context, DependencyEvents ev, List<object> items, IComputableView handler);
        static ConcurrentDictionary<Type, handlerInvokerDelegate> invokers = new();

        readonly PrecomputedViewsOptions options;
        DBContextInfo dbContextInfo;
        bool initialized = false;

        Dictionary<(Type depends, Type handler, DependencyEvents ev), List<object>> pendingViewUpdates;
        IDbContextTransaction pendingTransaction;

        Dictionary<Type, IComputableView> handlerInstances;

        /// <summary> </summary>
        public PrecomputedViewInterceptor() { }
        /// <summary> </summary>
        public PrecomputedViewInterceptor(PrecomputedViewsOptions options) { this.options = options; }

        private void InitializeIfNeeded(DbContext context)
        {
            if (initialized)
                return;

            //try get DBContextInfo
            var contextType = context.GetType();
            if (s_sharedDBContextInfo.TryGetValue(contextType, out dbContextInfo))
            {
                initialized = true;
                return;
            }

            //create (shared) DBContextInfo
            lock (s_sharedDBContextInfo)
                if (!s_sharedDBContextInfo.TryGetValue(contextType, out dbContextInfo))
                {
                    //init db-context info
                    dbContextInfo = new DBContextInfo();

                    //function to process a dependency
                    void processDependency(Type contextType, Type baseType, Type dependencyType, DependencyEvents events, Type handlerType)
                    {
                        //check if handler is valid computable view type
                        if (!typeof(IComputableView<,>).MakeGenericType([contextType, dependencyType]).IsAssignableFrom(handlerType))
                            throw new Exception($"View handler type '{handlerType.Name}' is not a valid IComputableView<,> type. (declared on {baseType.Namespace}.{baseType.Name})");

                        //add to inverse-dependencies
                        {
                            List<(DependencyEvents, Type)> list;
                            if (!dbContextInfo.InverseDependenciesCache.TryGetValue(dependencyType, out list))
                                dbContextInfo.InverseDependenciesCache[dependencyType] = list = new();

                            //add with separate events
                            if (events.HasFlag(DependencyEvents.Added))
                                list.Add((DependencyEvents.Added, handlerType));
                            if (events.HasFlag(DependencyEvents.Removed))
                                list.Add((DependencyEvents.Removed, handlerType));
                            if (events.HasFlag(DependencyEvents.Modified))
                                list.Add((DependencyEvents.Modified, handlerType));
                        }

                        //add to invokers
                        if (!invokers.ContainsKey(dependencyType))
                        {
                            var del = typeof(InvokerHelpers<,>)
                                            .MakeGenericType(contextType, dependencyType)
                                            .GetMethod("handlerInvoker")
                                            .CreateDelegate<handlerInvokerDelegate>();
                            invokers.TryAdd(dependencyType, del);
                        }
                    }

                    //process entity types from dbContext models
                    foreach (var entityType in context.Model.GetEntityTypes())
                    {
                        //get [DependsOn] attributes
                        var clrType = entityType.ClrType;
                        var dependsAttrs = clrType.GetCustomAttributes<DependsOnAttribute>();
                        if (dependsAttrs == null)
                            continue;

                        //process [DependsOn] attributes
                        foreach (var dependsAttr in dependsAttrs)
                            if (dependsAttr != null)
                            {
                                //get handler type
                                var handlerType = dependsAttr.HandlerType ?? clrType;

                                //process dependency
                                processDependency(contextType, clrType, dependsAttr.DependencyType, dependsAttr.Events, handlerType);
                            }
                    }

                    //process entity from Options
                    if (options?.ExtraDependencies != null)
                        foreach (var depEntry in options.ExtraDependencies)
                        {
                            //get handler type
                            var baseType = depEntry.BaseType;
                            var handlerType = depEntry.HandlerType ?? baseType;

                            //process dependency
                            processDependency(contextType, baseType, depEntry.DependencyType, depEntry.Events, handlerType);
                        }

                    //cache it
                    s_sharedDBContextInfo[contextType] = dbContextInfo;
                }

            //mark as initialized
            initialized = true;
        }

        /// <inheritdoc />
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            //ensure we have a proper context, else continue pipeline
            var context = eventData.Context;
            if (context == null || !context.ChangeTracker.HasChanges())
                return base.SavingChanges(eventData, result);

            //handle
            OnSavingChangesCore(context).Wait();

            //call base
            return base.SavingChanges(eventData, result);
        }

        /// <inheritdoc />
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            //ensure we have a proper context, else continue pipeline
            var context = eventData.Context;
            if (context == null || !context.ChangeTracker.HasChanges())
                return await base.SavingChangesAsync(eventData, result);

            //handle
            await OnSavingChangesCore(context);

            //call base
            return await base.SavingChangesAsync(eventData, result);
        }

        private async Task OnSavingChangesCore(DbContext context)
        {
            //make sure we have built our dependency caches after the model is fully configured.
            if (!initialized)
                InitializeIfNeeded(context);

            //reset state
            pendingViewUpdates?.Clear();
            if (pendingTransaction != null)
                throw new Exception("DBContext is corrupted. (should not have a PendingTransaction at this point)");

            // Find which view entities depend on these changed types            
            foreach (var changedEntry in context.ChangeTracker.Entries())
            {
                //map event
                var changeEvent = changedEntry.State switch
                {
                    EntityState.Added => DependencyEvents.Added,
                    EntityState.Deleted => DependencyEvents.Removed,
                    EntityState.Modified => DependencyEvents.Modified,
                    _ => DependencyEvents.None,
                };

                //if not a proper event continue
                if (changeEvent == DependencyEvents.None)
                    continue;

                //get entity info
                var changedEntity = changedEntry.Entity;
                var changedType = changedEntity.GetType();
                if (dbContextInfo.InverseDependenciesCache.TryGetValue(changedType, out var viewTypes))
                    foreach (var (ev, vt) in viewTypes)
                        if (changeEvent == ev)
                        {
                            //ensure collection
                            pendingViewUpdates ??= new();

                            //get or create item list
                            List<object> items;
                            if (!pendingViewUpdates.TryGetValue((changedType, vt, changeEvent), out items))
                                pendingViewUpdates[(changedType, vt, changeEvent)] = items = new();

                            //add items to pending
                            items.Add(changedEntity);
                        }
            }

            //setup new transaction (if none and needed)
            if (context.Database.CurrentTransaction == null && pendingViewUpdates != null && pendingViewUpdates.Count > 0)
                pendingTransaction = await context.Database.BeginTransactionAsync();
        }

        /// <inheritdoc />
        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            try
            {
                //save changes
                var changesSaved = base.SavedChanges(eventData, result);

                //no changes?
                if (changesSaved <= 0 || pendingViewUpdates == null || pendingViewUpdates.Count <= 0 || eventData.Context == null)
                    return changesSaved;

                //handle
                return OnSavedChangesCore(eventData.Context, changesSaved).Result;
            }
            finally
            {
                //reset state
                pendingViewUpdates?.Clear();
                //commit changes
                pendingTransaction?.Commit();
                Interlocked.Exchange(ref pendingTransaction, null)?.Dispose();
            }
        }

        /// <inheritdoc />
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            try
            {
                //save changes
                var changesSaved = await base.SavedChangesAsync(eventData, result, cancellationToken);

                //no changes?
                if (changesSaved <= 0 || pendingViewUpdates == null || pendingViewUpdates.Count <= 0 || eventData.Context == null)
                    return changesSaved;

                //handle
                return await OnSavedChangesCore(eventData.Context, changesSaved);
            }
            finally
            {
                //reset state
                pendingViewUpdates?.Clear();
                //commit changes
                await (pendingTransaction?.CommitAsync() ?? Task.CompletedTask);
                Interlocked.Exchange(ref pendingTransaction, null)?.Dispose();
            }
        }

        /// <inheritdoc />
        private async ValueTask<int> OnSavedChangesCore(DbContext context, int changesSaved)
        {
            //copy handler (avoid recursion issues)
            var pendingForUpdate = Interlocked.Exchange(ref pendingViewUpdates, null);

            //try get service provider
            var serviceProvider = context.GetService<IServiceProvider>();

            //run handlers
            foreach (var ((viewType, handlerType, ev), items) in pendingForUpdate)
            {
                //ensure cached handler set
                handlerInstances ??= new();

                //get or create handler
                IComputableView handler = null;
                if (!handlerInstances.TryGetValue(handlerType, out handler))
                {
                    //try get from DI
                    if (handler == null && serviceProvider != null)
                        handler = serviceProvider.GetService(handlerType) as IComputableView;

                    //create with DI or directly
                    if (handler == null && serviceProvider != null)
                        handler = ActivatorUtilities.CreateInstance(serviceProvider, handlerType) as IComputableView;

                    //try create directly
                    if (handler == null)
                        handler = Activator.CreateInstance(handlerType, true) as IComputableView;

                    //cache it 
                    handlerInstances[handlerType] = handler;
                }

                //get invoker and invoke
                if (invokers.TryGetValue(viewType, out var invoker))
                    await invoker(context, ev, items, handler);
            }
            return changesSaved;
        }

        /// <inheritdoc />
        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            try
            {
                //call base
                base.SaveChangesFailed(eventData);
            }
            finally
            {
                //reset state
                pendingViewUpdates?.Clear();
                //rollback
                pendingTransaction?.Rollback();
                Interlocked.Exchange(ref pendingTransaction, null)?.Dispose();
            }
        }

        /// <inheritdoc />
        public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            try
            {
                //call base
                await base.SaveChangesFailedAsync(eventData, cancellationToken);
            }
            finally
            {
                //reset state
                pendingViewUpdates?.Clear();
                //rollback
                await (pendingTransaction?.RollbackAsync() ?? Task.CompletedTask);
                Interlocked.Exchange(ref pendingTransaction, null)?.Dispose();
            }
        }

#if NET7_0_OR_GREATER
        public override async Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
        {
            try
            {
                //call base
                await base.SaveChangesCanceledAsync(eventData, cancellationToken);
            }
            finally
            {
                //reset state
                pendingViewUpdates?.Clear();
                //dispose transaction
                await (pendingTransaction?.RollbackAsync() ?? Task.CompletedTask);
                Interlocked.Exchange(ref pendingTransaction, null)?.Dispose();
            }
        }

        public override void SaveChangesCanceled(DbContextEventData eventData)
        {
            try
            {
                //call base
                base.SaveChangesCanceled(eventData);
            }
            finally
            {
                //reset state
                pendingViewUpdates?.Clear();
                //dispose transaction
                pendingTransaction?.Rollback();
                Interlocked.Exchange(ref pendingTransaction, null)?.Dispose();
            }
        }
#endif

        //helper for invoking strong-typed interfaces        
        static class InvokerHelpers<TDbContext, TEntity> where TDbContext : DbContext
        {
            public static ValueTask handlerInvoker(DbContext context, DependencyEvents ev, List<object> items, IComputableView handler)
            {
                var typedContext = context as TDbContext;
                var typedHandler = (IComputableView<TDbContext, TEntity>)handler;
                var typedItems = items.Cast<TEntity>();
                return typedHandler.ComputeView(typedContext, ev, typedItems);
            }
        }
    }
}