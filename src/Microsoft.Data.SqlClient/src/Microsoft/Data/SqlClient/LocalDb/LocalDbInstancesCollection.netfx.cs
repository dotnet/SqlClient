// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.Collections;
using System.Configuration;

namespace Microsoft.Data.SqlClient.LocalDb
{
    internal sealed class LocalDbInstancesCollection : ConfigurationElementCollection
    {
        private static readonly TrimOrdinalIgnoreCaseStringComparer s_comparer = new TrimOrdinalIgnoreCaseStringComparer();

        internal LocalDbInstancesCollection()
            : base(s_comparer)
        {
        }

        protected override ConfigurationElement CreateNewElement() =>
            new LocalDbInstanceElement();

        protected override object GetElementKey(ConfigurationElement element) =>
            ((LocalDbInstanceElement)element).Name;

        private class TrimOrdinalIgnoreCaseStringComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                if (x is string xStr)
                {
                    x = xStr.Trim();
                }

                if (y is string yStr)
                {
                    y = yStr.Trim();
                }

                return StringComparer.OrdinalIgnoreCase.Compare(x, y);
            }
        }
    }
}

#endif
