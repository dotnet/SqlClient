// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SteAttributeKey
    {
        public static readonly SteAttributeKey SqlDbType = new();
        public static readonly SteAttributeKey MultiValued = new();
        public static readonly SteAttributeKey Value = new();
        public static readonly SteAttributeKey MaxLength = new();
        public static readonly SteAttributeKey Precision = new();
        public static readonly SteAttributeKey Scale = new();
        public static readonly SteAttributeKey LocaleId = new();
        public static readonly SteAttributeKey CompareOptions = new();
        public static readonly SteAttributeKey TypeName = new();
        public static readonly SteAttributeKey Type = new();
        public static readonly SteAttributeKey Fields = new();
        public static readonly SteAttributeKey Offset = new();
        public static readonly SteAttributeKey Length = new();

        public static readonly IList<SteAttributeKey> MetaDataKeys = new List<SteAttributeKey>(
                new SteAttributeKey[] {
                    SteAttributeKey.SqlDbType,
                    SteAttributeKey.MultiValued,
                    SteAttributeKey.MaxLength,
                    SteAttributeKey.Precision,
                    SteAttributeKey.Scale,
                    SteAttributeKey.LocaleId,
                    SteAttributeKey.CompareOptions,
                    SteAttributeKey.TypeName,
                    SteAttributeKey.Type,
                    SteAttributeKey.Fields,
                }
            ).AsReadOnly();

        public static IList<SteAttributeKey> ValueKeys = new List<SteAttributeKey>(
                new SteAttributeKey[] { SteAttributeKey.Value }
            ).AsReadOnly();
    }

    public class SteAttribute
    {
        private readonly SteAttributeKey _key;
        private readonly object _value;

        public SteAttribute(SteAttributeKey key, object value)
        {
            _key = key;
            _value = value;
        }

        public SteAttributeKey Key
        {
            get
            {
                return _key;
            }
        }

        public object Value
        {
            get
            {
                return _value;
            }
        }
    }

}
