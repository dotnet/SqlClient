// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{
    internal static class GenericConverter
    {
        public static TOut Convert<TIn, TOut>(TIn value)
        {
            return GenericConverterHelper<TIn, TOut>.Convert(value);
        }

        /// <summary>
        /// Note: this file is inherently different because the .NET Core JIT can leverage some better "smarts" to optimize out unnecessary casts
        /// </summary>
        private static class GenericConverterHelper<TIn, TOut>
        {
            /// <summary>
            /// Converts the value to the TOut type
            /// </summary>
            public static readonly Func<TIn, TOut> Convert;

            static GenericConverterHelper()
            {
                var paramExpr = Expression.Parameter(typeof(TIn), "input");

                var lambda = typeof(TIn) != typeof(TOut)
                    ? Expression.Lambda<Func<TIn, TOut>>(Expression.Convert(paramExpr, typeof(TOut)), paramExpr)
                    : Expression.Lambda<Func<TIn, TOut>>(paramExpr, paramExpr)
                ;

                Convert = lambda.Compile();
            }
        }
    }
    
}
