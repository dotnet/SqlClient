// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/SqlBatchCommand/*'/>
    public class SqlBatchCommand
    #if NET
     : DbBatchCommand
    #endif
    {
        private string _text;
        private CommandType _type;
        private SqlParameterCollection _parameters;
        private CommandBehavior _behavior;
        private int _recordsAffected;
        private SqlCommandColumnEncryptionSetting _encryptionSetting;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/ctor1/*'/>
        public SqlBatchCommand()
        {
            _type = CommandType.Text;
        }
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/ctor2/*'/>
        public SqlBatchCommand(string commandText, CommandType commandType = CommandType.Text, IEnumerable<SqlParameter> parameters = null, SqlCommandColumnEncryptionSetting columnEncryptionSetting = SqlCommandColumnEncryptionSetting.UseConnectionSetting)
        {
            if (string.IsNullOrEmpty(commandText))
            {
                throw ADP.CommandTextRequired(nameof(SqlBatchCommand));
            }
            _text = commandText;
            SetCommandType(commandType);
            if (parameters != null)
            {
                SqlParameterCollection parameterCollection = null;
                if (parameters is IList<SqlParameter> list)
                {
                    parameterCollection = new SqlParameterCollection(list.Count);
                    for (int index = 0; index < list.Count; index++)
                    {
                        parameterCollection.Add(list[index]);
                    }
                }
                else
                {
                    parameterCollection = new SqlParameterCollection();
                    foreach (SqlParameter parameter in parameters)
                    {
                        parameterCollection.Add(parameter);
                    }
                }
                _parameters = parameterCollection;
            }
            _encryptionSetting = columnEncryptionSetting;
        }

        // parameter order is reversed for this internal method to avoid ambiguous call sites with the public constructor
        // this overload is used internally to take the parameters passed instead of copying them
        internal SqlBatchCommand(string commandText, SqlParameterCollection parameterCollection, CommandType commandType, SqlCommandColumnEncryptionSetting columnEncryptionSetting)
            : this(commandText, commandType, null, columnEncryptionSetting)
        {
            _parameters = parameterCollection;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CanCreateParameter/*'/>
        public
        #if NET
        override
        #endif
        bool CanCreateParameter => true;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CommandText/*'/>   
        public
        #if NET
        override 
        #endif
        string CommandText { get => _text; set => _text = value; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CommandType/*'/>  
        public
        #if NET
        override 
        #endif
        CommandType CommandType { get => _type; set => SetCommandType(value); }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CommandBehavior/*'/>
        public CommandBehavior CommandBehavior { get => _behavior; set => _behavior = value; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/RecordsAffected/*'/>
        public
        #if NET
        override 
        #endif
        int RecordsAffected { get => _recordsAffected; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/DbParameterCollection/*'/>
        protected
        #if NET
        override
        #else
        virtual
        #endif
        DbParameterCollection DbParameterCollection => Parameters;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/Parameters/*'/>
        public
        #if NET
        new 
        #endif
        SqlParameterCollection Parameters
        {
            get
            {
                if (_parameters is null)
                {
                    _parameters = new SqlParameterCollection();
                }
                return _parameters;
            }
            internal set
            {
                _parameters = value;
            }
        }
        
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/ColumnEncryptionSetting/*'/>
        public SqlCommandColumnEncryptionSetting ColumnEncryptionSetting { get => _encryptionSetting; set => _encryptionSetting = value; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CreateParameter/*'/>
        public
        #if NET
        override
        #endif
        DbParameter CreateParameter() => new SqlParameter();

        private void SetCommandType(CommandType value)
        {
            if (value != _type)
            {
                switch (value)
                {
                    case CommandType.Text:
                    case CommandType.StoredProcedure:
                        _type = value;
                        break;
                    case System.Data.CommandType.TableDirect:
                        throw SQL.NotSupportedCommandType(value);
                    default:
                        throw ADP.InvalidCommandType(value);
                }
            }
        }

        internal void SetRecordAffected(int value)
        {
            _recordsAffected = value;
        }
    }
}
