// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/SqlUserDefinedAggregateAttribute/*' />
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

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/MaxByteSizeValue/*' />
        // The maximum value for the maxbytesize field, in bytes.
        public const int MaxByteSizeValue = 8000;

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/ctor/*' />
        // A required attribute on all UD Aggs, used to indicate that the
        // given type is a UD Agg, and its storage format.
        public SqlUserDefinedAggregateAttribute(Format format)
        {
            switch (format)
            {
                case Format.Unknown:
                    throw ADP.NotSupportedUserDefinedTypeSerializationFormat(format, nameof(format));
                case Format.Native:
                case Format.UserDefined:
                    _format = format;
                    break;
                default:
                    throw ADP.InvalidUserDefinedTypeSerializationFormat(format);
            }
        }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/MaxByteSize/*' />
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
                    throw ADP.ArgumentOutOfRange(StringsHelper.GetString(Strings.SQLUDT_MaxByteSizeValue), nameof(MaxByteSize), value);
                }
                _maxByteSize = value;
            }
        }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/IsInvariantToDuplicates/*' />
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

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/IsInvariantToNulls/*' />
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

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/IsInvariantToOrder/*' />
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

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/IsNullIfEmpty/*' />
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

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/Format/*' />
        // The on-disk format for this type.
        public Format Format => _format;

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/Name/*' />
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
