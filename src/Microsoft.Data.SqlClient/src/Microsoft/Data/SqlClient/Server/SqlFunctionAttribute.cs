// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/DataAccessKind.xml' path='docs/members[@name="DataAccessKind"]/DataAccessKind/*' />
    [Serializable]
    public enum DataAccessKind
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/DataAccessKind.xml' path='docs/members[@name="DataAccessKind"]/None/*' />
        None = 0,
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/DataAccessKind.xml' path='docs/members[@name="DataAccessKind"]/Read/*' />
        Read = 1,
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SystemDataAccessKind.xml' path='docs/members[@name="SystemDataAccessKind"]/SystemDataAccessKind/*' />
    [Serializable]
    public enum SystemDataAccessKind
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SystemDataAccessKind.xml' path='docs/members[@name="SystemDataAccessKind"]/None/*' />
        None = 0,
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SystemDataAccessKind.xml' path='docs/members[@name="SystemDataAccessKind"]/Read/*' />
        Read = 1,
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFunctionAttribute.xml' path='docs/members[@name="SqlFunctionAttribute"]/SqlFunctionAttribute/*' />
    // sql specific attribute
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false), Serializable]
    public class SqlFunctionAttribute : Attribute
    {
        private bool _isDeterministic;
        private DataAccessKind _dataAccess;
        private SystemDataAccessKind _systemDataAccess;
        private bool _isPrecise;
        private string _name;
        private string _tableDefinition;
        private string _fillRowMethodName;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFunctionAttribute.xml' path='docs/members[@name="SqlFunctionAttribute"]/ctor/*' />
        public SqlFunctionAttribute()
        {
            // default values
            _isDeterministic = false;
            _dataAccess = DataAccessKind.None;
            _systemDataAccess = SystemDataAccessKind.None;
            _isPrecise = false;
            _name = null;
            _tableDefinition = null;
            _fillRowMethodName = null;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFunctionAttribute.xml' path='docs/members[@name="SqlFunctionAttribute"]/IsDeterministic/*' />
        public bool IsDeterministic
        {
            get => _isDeterministic;
            set => _isDeterministic = value;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFunctionAttribute.xml' path='docs/members[@name="SqlFunctionAttribute"]/DataAccess/*' />
        public DataAccessKind DataAccess
        {
            get => _dataAccess;
            set => _dataAccess = value;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFunctionAttribute.xml' path='docs/members[@name="SqlFunctionAttribute"]/SystemDataAccess/*' />
        public SystemDataAccessKind SystemDataAccess
        {
            get => _systemDataAccess;
            set => _systemDataAccess = value;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFunctionAttribute.xml' path='docs/members[@name="SqlFunctionAttribute"]/IsPrecise/*' />
        public bool IsPrecise
        {
            get => _isPrecise;
            set => _isPrecise = value;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFunctionAttribute.xml' path='docs/members[@name="SqlFunctionAttribute"]/Name/*' />
        public string Name
        {
            get => _name;
            set => _name = value;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFunctionAttribute.xml' path='docs/members[@name="SqlFunctionAttribute"]/TableDefinition/*' />
        public string TableDefinition
        {
            get => _tableDefinition;
            set => _tableDefinition = value;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlFunctionAttribute.xml' path='docs/members[@name="SqlFunctionAttribute"]/FillRowMethodName/*' />
        public string FillRowMethodName
        {
            get => _fillRowMethodName;
            set => _fillRowMethodName = value;
        }
    }
}
