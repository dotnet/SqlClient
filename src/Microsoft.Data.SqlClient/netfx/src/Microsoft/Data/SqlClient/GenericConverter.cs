// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    // This leverages the same assumptions in SqlBuffer that the JIT will optimize out the boxing / unboxing when TIn == TOut
    // This behavior is proven out in the NoBoxingValueTypes BulkCopy unit test that benchmarks and measures the allocations
    internal static class GenericConverter
    {
        public static TOut Convert<TIn, TOut>(TIn value)
        {
            return (TOut)(object)value;
        }            
    }
}
