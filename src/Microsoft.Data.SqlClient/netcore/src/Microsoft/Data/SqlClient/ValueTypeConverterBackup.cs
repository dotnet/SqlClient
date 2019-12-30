using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Delegate representing a converter to pass in by reference.
    /// </summary>
    /// <typeparam name="TIn"></typeparam>
    /// <typeparam name="TOut"></typeparam>
    /// <param name="input"></param>
    /// <returns></returns>
    public delegate TOut RefConverter<TIn, TOut>(ref TIn input);

    /// <summary>
    /// Converts value types from a generic to the desired, underlying type without boxing.
    /// </summary>
    /// <typeparam name="TIn"></typeparam>
    /// <typeparam name="TOut"></typeparam>
    public static class ValueTypeConverterBackup<TIn, TOut>
    {
        /// <summary>
        /// Converts the value to the TOut type
        /// </summary>
        public static readonly RefConverter<TIn, TOut> Convert;

        static ValueTypeConverterBackup()
        {
            var paramExpr = Expression.Parameter(typeof(TIn).MakeByRefType(), "input");

            var lambda = typeof(TIn) != typeof(TOut)
                ? Expression.Lambda<RefConverter<TIn, TOut>>(Expression.Convert(paramExpr, typeof(TOut)), paramExpr)
                : Expression.Lambda<RefConverter<TIn, TOut>>(paramExpr, paramExpr)
            ;

            Convert = lambda.Compile();
        }
    }
}
