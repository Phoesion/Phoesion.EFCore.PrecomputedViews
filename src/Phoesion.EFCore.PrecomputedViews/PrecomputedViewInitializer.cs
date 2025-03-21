using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Phoesion.EFCore.PrecomputedViews
{
    /// <summary>
    /// Provides a way to initialize a precomputed view in batches.
    /// </summary>
    public static class PrecomputedViewInitializer
    {
        /// <summary>
        /// Initializes a precomputed view in batches.
        /// </summary>
        /// <param name="context"> the db context </param>
        /// <param name="dbSet"> the DbSet[Entity] selector</param>
        /// <param name="keySelector"> the key selector used for collecting batches. Must be an ordered numerics field (eg. Id, date-added etc) </param>
        /// <param name="batchSize"> the batch size </param>
        /// <param name="runHandlerForQuery"> handler to run for computing each batch </param>
        /// <param name="cancellationToken">the cancellation token</param>
        /// <returns></returns>
        public static Task InititializeViewAsync<TDBContext, TEntity, TKey>(TDBContext context, Func<TDBContext, DbSet<TEntity>> dbSet, Expression<Func<TEntity, TKey>> keySelector, int batchSize, Func<TDBContext, IQueryable<TEntity>, Task> runHandlerForQuery)
            where TDBContext : DbContext
            where TKey : struct, INumber<TKey>
            where TEntity : class
            => InititializeViewAsync(context, dbSet, keySelector, batchSize, runHandlerForQuery, CancellationToken.None);

        /// <summary>
        /// Initializes a precomputed view in batches.
        /// </summary>
        /// <param name="context"> the db context </param>
        /// <param name="dbSet"> the DbSet[Entity] selector</param>
        /// <param name="keySelector"> the key selector used for collecting batches. Must be an ordered numerics field (eg. Id, date-added etc) </param>
        /// <param name="batchSize"> the batch size </param>
        /// <param name="runHandlerForQuery"> handler to run for computing each batch </param>
        /// <param name="cancellationToken">the cancellation token</param>
        /// <returns></returns>
        public static async Task InititializeViewAsync<TDBContext, TEntity, TKey>(TDBContext context, Func<TDBContext, DbSet<TEntity>> dbSet, Expression<Func<TEntity, TKey>> keySelector, int batchSize, Func<TDBContext, IQueryable<TEntity>, Task> runHandlerForQuery, CancellationToken cancellationToken)
            where TDBContext : DbContext
            where TKey : struct, INumber<TKey>
            where TEntity : class
        {
            await foreach (var _ in InititializeViewWithStatusAsync(context, dbSet, keySelector, batchSize, runHandlerForQuery, cancellationToken).WithCancellation(cancellationToken))
            {
                //do nothing, just iterate
            }
        }

        /// <summary>
        /// Initializes a precomputed view in batches, reporting the progress of the operation.
        /// </summary>
        /// <param name="context"> the db context </param>
        /// <param name="dbSet"> the DbSet[Entity] selector</param>
        /// <param name="keySelector"> the key selector used for collecting batches. Must be an ordered numerics field (eg. Id, date-added etc) </param>
        /// <param name="batchSize"> the batch size </param>
        /// <param name="runHandlerForQuery"> handler to run for computing each batch </param>
        /// <param name="cancellationToken">the cancellation token</param>
        /// <returns></returns>
        public static IAsyncEnumerable<(TKey, TKey)> InititializeViewWithStatusAsync<TDBContext, TEntity, TKey>(TDBContext context, Func<TDBContext, DbSet<TEntity>> dbSet, Expression<Func<TEntity, TKey>> keySelector, int batchSize, Func<TDBContext, IQueryable<TEntity>, Task> runHandlerForQuery)
            where TDBContext : DbContext
            where TKey : struct, INumber<TKey>
            where TEntity : class
            => InititializeViewWithStatusAsync(context, dbSet, keySelector, batchSize, runHandlerForQuery, CancellationToken.None);

        /// <summary>
        /// Initializes a precomputed view in batches, reporting the progress of the operation.
        /// </summary>
        /// <param name="context"> the db context </param>
        /// <param name="dbSet"> the DbSet[Entity] selector</param>
        /// <param name="keySelector"> the key selector used for collecting batches. Must be an ordered numerics field (eg. Id, date-added etc) </param>
        /// <param name="batchSize"> the batch size </param>
        /// <param name="runHandlerForQuery"> handler to run for computing each batch </param>
        /// <param name="cancellationToken">the cancellation token</param>
        /// <returns></returns>
        public static async IAsyncEnumerable<(TKey, TKey)> InititializeViewWithStatusAsync<TDBContext, TEntity, TKey>(TDBContext context, Func<TDBContext, DbSet<TEntity>> dbSet, Expression<Func<TEntity, TKey>> keySelector, int batchSize, Func<TDBContext, IQueryable<TEntity>, Task> runHandlerForQuery, [EnumeratorCancellation] CancellationToken cancellationToken)
            where TDBContext : DbContext
            where TKey : struct, INumber<TKey>
            where TEntity : class
        {
            var name = GetPropertyName(keySelector);
            TKey cursor = default;
            while (true)
            {
                //check cancellation
                cancellationToken.ThrowIfCancellationRequested();

                //detect next batch limits
                var upToNext = (TKey?)(await dbSet(context)
                                            .Where(BuildGreaterThanExpression(keySelector, cursor))//.Where(kpi => kpi.Id > cursor)
                                            .OrderBy(keySelector)
                                            .Take(batchSize)
                                            .Select(ConvertToNullableKey(keySelector))//.Select(kpi => (TKey?)kpi.Id)
                                            .LastOrDefaultAsync(cancellationToken));

                //detect if we reached the end
                if (upToNext == null || cursor >= upToNext.Value)
                    break;

                //check cancellation
                cancellationToken.ThrowIfCancellationRequested();

                //report indent
                yield return (cursor, upToNext.Value);

                // limit search space to affected courses only (otherwise the query will update all course entries)
                var entities = dbSet(context).Where(BuildRangeExpression(keySelector, cursor, upToNext.Value)); //.Where(kpi => kpi.Id >= cursor && kpi.Id <= upToNext);

                //execute update
                await runHandlerForQuery(context, entities);

                //move cursor
                cursor = upToNext.Value;
            }
        }

        static string GetPropertyName<TDbSet, TKey>(Expression<Func<TDbSet, TKey>> keySelector)
        {
            if (keySelector.Body is MemberExpression memberExpression)
                return memberExpression.Member.Name;

            // If the expression is a UnaryExpression (e.g., boxing/unboxing), unwrap it
            if (keySelector.Body is UnaryExpression unaryExpression &&
                unaryExpression.Operand is MemberExpression innerMember)
                return innerMember.Member.Name;

            throw new ArgumentException("Invalid expression format. Expected a simple property access like x => x.Id.");
        }

        static Expression<Func<TDbSet, bool>> BuildGreaterThanExpression<TDbSet, TKey>(Expression<Func<TDbSet, TKey>> keySelector, TKey cursor)
        {
            // Parameter from the original expression (e.g., x in x => x.Id)
            var parameter = keySelector.Parameters[0];

            // Access the body (e.g., x.Id)
            Expression propertyAccess = keySelector.Body;

            // In case of value conversion (e.g., Convert(x.Id)), unwrap it
            if (propertyAccess is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                propertyAccess = unary.Operand;

            // Create constant expression for the cursor value
            var cursorConstant = Expression.Constant(cursor, typeof(TKey));

            // Build the binary expression: x.Id > cursor
            var comparison = Expression.GreaterThan(propertyAccess, cursorConstant);

            // Create and return the full lambda expression: x => x.Id > cursor
            return Expression.Lambda<Func<TDbSet, bool>>(comparison, parameter);
        }

        static Expression<Func<TDbSet, Nullable<TKey>>> ConvertToNullableKey<TDbSet, TKey>(Expression<Func<TDbSet, TKey>> keySelector) where TKey : struct, INumber<TKey>
        {
            // Get the parameter from the original expression (e.g., kpi)
            var parameter = keySelector.Parameters[0];

            // Get the property access (e.g., kpi.Id)
            Expression propertyAccess = keySelector.Body;

            // Create a convert expression to Nullable<TKey> => (TKey?)kpi.Id
            var convertExpression = Expression.Convert(propertyAccess, typeof(Nullable<TKey>));

            // Return the new lambda expression
            return Expression.Lambda<Func<TDbSet, Nullable<TKey>>>(convertExpression, parameter);
        }

        static Expression<Func<TDbSet, bool>> BuildRangeExpression<TDbSet, TKey>(Expression<Func<TDbSet, TKey>> keySelector, TKey cursor, TKey upToNext)
            where TKey : struct, INumber<TKey>
        {
            var parameter = keySelector.Parameters[0];
            Expression propertyAccess = keySelector.Body;

            // Unwrap conversion if needed (e.g., boxing)
            if (propertyAccess is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                propertyAccess = unary.Operand;

            var cursorConstant = Expression.Constant(cursor, typeof(TKey));
            var upToNextConstant = Expression.Constant(upToNext, typeof(TKey));

            var greaterThanOrEqual = Expression.GreaterThanOrEqual(propertyAccess, cursorConstant);
            var lessThan = Expression.LessThanOrEqual(propertyAccess, upToNextConstant);

            var combined = Expression.AndAlso(greaterThanOrEqual, lessThan);

            return Expression.Lambda<Func<TDbSet, bool>>(combined, parameter);
        }
    }
}
