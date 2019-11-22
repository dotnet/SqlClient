// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/SqlUserDefinedAggregateAttribute/*' />
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class SqlUserDefinedAggregateAttribute : Attribute
    {
        private int m_MaxByteSize;
        private bool m_fInvariantToDup;
        private bool m_fInvariantToNulls;
        private bool m_fInvariantToOrder = true;
        private bool m_fNullIfEmpty;
        private Format m_format;
        private string m_fName;

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/MaxByteSizeValue/*' />
        // The maximum value for the maxbytesize field, in bytes.
        public const int MaxByteSizeValue = 8000;

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/ctor/*' />
        // A required attribute on all udaggs, used to indicate that the
        // given type is a udagg, and its storage format.
        public SqlUserDefinedAggregateAttribute(Format format)
        {
            switch (format)
            {
                case Format.Unknown:
                    throw ADP.NotSupportedUserDefinedTypeSerializationFormat((Microsoft.Data.SqlClient.Server.Format)format, "format");
                case Format.Native:
                case Format.UserDefined:
                    this.m_format = format;
                    break;
                default:
                    throw ADP.InvalidUserDefinedTypeSerializationFormat((Microsoft.Data.SqlClient.Server.Format)format);
            }
        }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/MaxByteSize/*' />
        // The maximum size of this instance, in bytes. Does not have to be
        // specified for Native format serialization. The maximum value
        // for this property is specified by MaxByteSizeValue.
        public int MaxByteSize
        {
            get
            {
                return this.m_MaxByteSize;
            }
            set
            {
                // MaxByteSize of -1 means 2GB and is valid, as well as 0 to MaxByteSizeValue
                if (value < -1 || value > MaxByteSizeValue)
                {
                    throw ADP.ArgumentOutOfRange(StringsHelper.GetString(Strings.SQLUDT_MaxByteSizeValue), "MaxByteSize", value);
                }
                this.m_MaxByteSize = value;
            }
        }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/IsInvariantToDuplicates/*' />
        public bool IsInvariantToDuplicates
        {
            get
            {
                return this.m_fInvariantToDup;
            }
            set
            {
                this.m_fInvariantToDup = value;
            }
        }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/IsInvariantToNulls/*' />
        public bool IsInvariantToNulls
        {
            get
            {
                return this.m_fInvariantToNulls;
            }
            set
            {
                this.m_fInvariantToNulls = value;
            }
        }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/IsInvariantToOrder/*' />
        public bool IsInvariantToOrder
        {
            get
            {
                return this.m_fInvariantToOrder;
            }
            set
            {
                this.m_fInvariantToOrder = value;
            }
        }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/IsNullIfEmpty/*' />
        public bool IsNullIfEmpty
        {
            get
            {
                return this.m_fNullIfEmpty;
            }
            set
            {
                this.m_fNullIfEmpty = value;
            }
        }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/Format/*' />
        // The on-disk format for this type.
        public Format Format
        {
            get
            {
                return this.m_format;
            }
        }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedAggregateAttribute.xml' path='docs/members[@name="SqlUserDefinedAggregateAttribute"]/Name/*' />
        public string Name
        {
            get
            {
                return m_fName;
            }
            set
            {
                m_fName = value;
            }
        }
    }
}
