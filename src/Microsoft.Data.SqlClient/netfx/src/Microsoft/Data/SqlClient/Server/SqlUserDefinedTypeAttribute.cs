// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\Format.xml' path='docs/members[@name="Format"]/Format/*' />
    public enum Format
    { //: byte
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\Format.xml' path='docs/members[@name="Format"]/Unknown/*' />
        Unknown = 0,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\Format.xml' path='docs/members[@name="Format"]/Native/*' />
        Native = 1,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\Format.xml' path='docs/members[@name="Format"]/UserDefined/*' />
        UserDefined = 2,
    }

    /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedTypeAttribute.xml' path='docs/members[@name="SqlUserDefinedTypeAttribute"]/SqlUserDefinedTypeAttribute/*' />
    // This custom attribute indicates that the given type is
    // a SqlServer udt. The properties on the attribute reflect the
    // physical attributes that will be used when the type is registered
    // with SqlServer.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public sealed class SqlUserDefinedTypeAttribute : Attribute
    {
        private int m_MaxByteSize;
        private bool m_IsFixedLength;
        private bool m_IsByteOrdered;
        private Format m_format;
        private string m_fName;

        // The maximum value for the maxbytesize field, in bytes.
        internal const int YukonMaxByteSizeValue = 8000;
        private String m_ValidationMethodName = null;

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedTypeAttribute.xml' path='docs/members[@name="SqlUserDefinedTypeAttribute"]/ctor/*' />
        // A required attribute on all udts, used to indicate that the
        // given type is a udt, and its storage format.
        public SqlUserDefinedTypeAttribute(Format format)
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

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedTypeAttribute.xml' path='docs/members[@name="SqlUserDefinedTypeAttribute"]/MaxByteSize/*' />
        // The maximum size of this instance, in bytes. Does not have to be
        // specified for Native serialization. The maximum value
        // for this property is specified by MaxByteSizeValue.
        public int MaxByteSize
        {
            get
            {
                return this.m_MaxByteSize;
            }
            set
            {
                if (value < -1)
                {
                    throw ADP.ArgumentOutOfRange("MaxByteSize");
                }
                this.m_MaxByteSize = value;
            }
        }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedTypeAttribute.xml' path='docs/members[@name="SqlUserDefinedTypeAttribute"]/IsFixedLength/*' />
        // Are all instances of this udt the same size on disk?
        public bool IsFixedLength
        {
            get
            {
                return this.m_IsFixedLength;
            }
            set
            {
                this.m_IsFixedLength = value;
            }
        }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedTypeAttribute.xml' path='docs/members[@name="SqlUserDefinedTypeAttribute"]/IsByteOrdered/*' />
        // Is this type byte ordered, i.e. is the on disk representation
        // consistent with the ordering semantics for this type?
        // If true, the binary representation of the type will be used
        // in comparison by SqlServer. This property enables indexing on the
        // udt and faster comparisons.
        public bool IsByteOrdered
        {
            get
            {
                return this.m_IsByteOrdered;
            }
            set
            {
                this.m_IsByteOrdered = value;
            }
        }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedTypeAttribute.xml' path='docs/members[@name="SqlUserDefinedTypeAttribute"]/Format/*' />
        // The on-disk format for this type.
        public Format Format
        {
            get
            {
                return this.m_format;
            }
        }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedTypeAttribute.xml' path='docs/members[@name="SqlUserDefinedTypeAttribute"]/ValidationMethodName/*' />
        // An Optional method used to validate this UDT
        // Signature: bool &lt;ValidationMethodName&gt;();
        public String ValidationMethodName
        {
            get
            {
                return this.m_ValidationMethodName;
            }
            set
            {
                this.m_ValidationMethodName = value;
            }
        }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\SqlUserDefinedTypeAttribute.xml' path='docs/members[@name="SqlUserDefinedTypeAttribute"]/Name/*' />
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
