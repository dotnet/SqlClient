// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.SqlServer.Server
{
    /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlUserDefinedAggregateAttribute.xml' path='Type[@Name="SqlUserDefinedAggregateAttribute"]/Docs/*' />
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class SqlUserDefinedAggregateAttribute : Attribute
    {
        private int _maxByteSize;
        private bool _isInvariantToDup;
        private bool _isInvariantToNulls;
        private bool _isInvariantToOrder = true;
        private bool _isNullIfEmpty;
        private Format _format;
        private string _name;

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlUserDefinedAggregateAttribute.xml' path='Type[@Name="SqlUserDefinedAggregateAttribute"]/Members/Member[@MemberName="MaxByteSizeValue"]/Docs/*' />
        // The maximum value for the maxbytesize field, in bytes.
        public const int MaxByteSizeValue = 8000;

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlUserDefinedAggregateAttribute.xml' path='Type[@Name="SqlUserDefinedAggregateAttribute"]/Members/Member[@MemberName=".ctor"]/Docs/*' />
        // A required attribute on all UD Aggs, used to indicate that the
        // given type is a UD Agg, and its storage format.
        public SqlUserDefinedAggregateAttribute(Format format)
        {
            switch (format)
            {
                case Format.Unknown:
                    throw new ArgumentOutOfRangeException(typeof(Format).Name, string.Format(Strings.ADP_NotSupportedEnumerationValue, typeof(Format).Name, ((int)format).ToString(), nameof(format)));
                case Format.Native:
                case Format.UserDefined:
                    _format = format;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(typeof(Format).Name, string.Format(Strings.ADP_InvalidEnumerationValue, typeof(Format).Name, ((int)format).ToString(CultureInfo.InvariantCulture)));
            }
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlUserDefinedAggregateAttribute.xml' path='Type[@Name="SqlUserDefinedAggregateAttribute"]/Members/Member[@MemberName="MaxByteSize"]/Docs/*' />
        // The maximum size of this instance, in bytes. Does not have to be
        // specified for Native format serialization. The maximum value
        // for this property is specified by MaxByteSizeValue.
        public int MaxByteSize
        {
            get
            {
                return _maxByteSize;
            }
            set
            {
                // MaxByteSize of -1 means 2GB and is valid, as well as 0 to MaxByteSizeValue
                if (value < -1 || value > MaxByteSizeValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaxByteSize), value.ToString(), StringsHelper.GetString(Strings.SQLUDT_MaxByteSizeValue));
                }
                _maxByteSize = value;
            }
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlUserDefinedAggregateAttribute.xml' path='Type[@Name="SqlUserDefinedAggregateAttribute"]/Members/Member[@MemberName="IsInvariantToDuplicates"]/Docs/*' />
        public bool IsInvariantToDuplicates
        {
            get
            {
                return _isInvariantToDup;
            }
            set
            {
                _isInvariantToDup = value;
            }
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlUserDefinedAggregateAttribute.xml' path='Type[@Name="SqlUserDefinedAggregateAttribute"]/Members/Member[@MemberName="IsInvariantToNulls"]/Docs/*' />
        public bool IsInvariantToNulls
        {
            get
            {
                return _isInvariantToNulls;
            }
            set
            {
                _isInvariantToNulls = value;
            }
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlUserDefinedAggregateAttribute.xml' path='Type[@Name="SqlUserDefinedAggregateAttribute"]/Members/Member[@MemberName="IsInvariantToOrder"]/Docs/*' />
        public bool IsInvariantToOrder
        {
            get
            {
                return _isInvariantToOrder;
            }
            set
            {
                _isInvariantToOrder = value;
            }
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlUserDefinedAggregateAttribute.xml' path='Type[@Name="SqlUserDefinedAggregateAttribute"]/Members/Member[@MemberName="IsNullIfEmpty"]/Docs/*' />
        public bool IsNullIfEmpty
        {
            get
            {
                return _isNullIfEmpty;
            }
            set
            {
                _isNullIfEmpty = value;
            }
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlUserDefinedAggregateAttribute.xml' path='Type[@Name="SqlUserDefinedAggregateAttribute"]/Members/Member[@MemberName="Format"]/Docs/*' />
        // The on-disk format for this type.
        public Format Format => _format;

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlUserDefinedAggregateAttribute.xml' path='Type[@Name="SqlUserDefinedAggregateAttribute"]/Members/Member[@MemberName="Name"]/Docs/*' />
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
            }
        }
    }
}
