using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace WaveMP.Data.Queries;

internal static class ExpressionExtensions
{
    /// <summary>
    /// A predicate that holds when either predicate holds (<c>OR</c> in the database).
    /// </summary>
    public static Expression<Func<T, bool>> OrElse<T>(
        this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        Expression body = Expression.OrElse(
            left.Body,
            ReplacingExpressionVisitor.Replace(right.Parameters[0], left.Parameters[0], right.Body));
        return Expression.Lambda<Func<T, bool>>(body, left.Parameters[0]);
    }
}
