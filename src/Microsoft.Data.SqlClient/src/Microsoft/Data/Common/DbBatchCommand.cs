// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Data.Common
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public abstract class DbBatchCommand
    {
        public abstract string CommandText { get; set; }

        public abstract CommandType CommandType { get; set; }

        public abstract int RecordsAffected { get; }

        public DbParameterCollection Parameters => DbParameterCollection;

        protected abstract DbParameterCollection DbParameterCollection { get; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
