// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.SqlServer.Server
{
    /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/DataAccessKind.xml' path='Type[@Name="DataAccessKind"]/Docs/*' />
    [Serializable]
    public enum DataAccessKind
    {
        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/DataAccessKind.xml' path='Type[@Name="DataAccessKind"]/Members/Member[@MemberName="None"]/Docs/*' />
        None = 0,
        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/DataAccessKind.xml' path='Type[@Name="DataAccessKind"]/Members/Member[@MemberName="Read"]/Docs/*' />
        Read = 1,
    }

    /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SystemDataAccessKind.xml' path='Type[@Name="SystemDataAccessKind"]/Docs/*' />
    [Serializable]
    public enum SystemDataAccessKind
    {
        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SystemDataAccessKind.xml' path='Type[@Name="SystemDataAccessKind"]/Members/Member[@MemberName="None"]/Docs/*' />
        None = 0,
        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SystemDataAccessKind.xml' path='Type[@Name="SystemDataAccessKind"]/Members/Member[@MemberName="Read"]/Docs/*' />
        Read = 1,
    }

    /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFunctionAttribute.xml' path='Type[@Name="SqlFunctionAttribute"]/Docs/*' />
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

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFunctionAttribute.xml' path='Type[@Name="SqlFunctionAttribute"]/Members/Member[@MemberName=".ctor"]/Docs/*' />
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

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFunctionAttribute.xml' path='Type[@Name="SqlFunctionAttribute"]/Members/Member[@MemberName="IsDeterministic"]/Docs/*' />
        public bool IsDeterministic
        {
            get => _isDeterministic;
            set => _isDeterministic = value;
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFunctionAttribute.xml' path='Type[@Name="SqlFunctionAttribute"]/Members/Member[@MemberName="DataAccess"]/Docs/*' />
        public DataAccessKind DataAccess
        {
            get => _dataAccess;
            set => _dataAccess = value;
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFunctionAttribute.xml' path='Type[@Name="SqlFunctionAttribute"]/Members/Member[@MemberName="SystemDataAccess"]/Docs/*' />
        public SystemDataAccessKind SystemDataAccess
        {
            get => _systemDataAccess;
            set => _systemDataAccess = value;
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFunctionAttribute.xml' path='Type[@Name="SqlFunctionAttribute"]/Members/Member[@MemberName="IsPrecise"]/Docs/*' />
        public bool IsPrecise
        {
            get => _isPrecise;
            set => _isPrecise = value;
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFunctionAttribute.xml' path='Type[@Name="SqlFunctionAttribute"]/Members/Member[@MemberName="Name"]/Docs/*' />
        public string Name
        {
            get => _name;
            set => _name = value;
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFunctionAttribute.xml' path='Type[@Name="SqlFunctionAttribute"]/Members/Member[@MemberName="TableDefinition"]/Docs/*' />
        public string TableDefinition
        {
            get => _tableDefinition;
            set => _tableDefinition = value;
        }

        /// <include file='../../doc/snippets/Microsoft.SqlServer.Server/SqlFunctionAttribute.xml' path='Type[@Name="SqlFunctionAttribute"]/Members/Member[@MemberName="FillRowMethodName"]/Docs/*' />
        public string FillRowMethodName
        {
            get => _fillRowMethodName;
            set => _fillRowMethodName = value;
        }
    }
}
