// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Serves to convert generic to out type by casting to object first.  Relies on JIT to optimize out unneccessary casts and prevent double boxing. 
    /// </summary>
    internal static class GenericCastExtensions
    {
        public static TOut GenericCast<TIn, TOut>(this TIn value)
        {
            return (TOut)(object)value;
        }
    }
}
