// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    public class SqlBatchCommand : DbBatchCommand
    {
        private string _text;
        private CommandType _type;
        private SqlParameterCollection _parameters;
        private CommandBehavior _behavior;
        private int _recordsAffected;
        private SqlCommandColumnEncryptionSetting _encryptionSetting;

        public SqlBatchCommand()
        {
            _type = CommandType.Text;
        }

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

        public override string CommandText { get => _text; set => _text = value; }

        public override CommandType CommandType { get => _type; set => SetCommandType(value); }

        public CommandBehavior CommandBehavior { get => _behavior; set => _behavior = value; }

        public override int RecordsAffected { get => _recordsAffected; }

        protected override DbParameterCollection DbParameterCollection => Parameters;

        public new SqlParameterCollection Parameters
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

        public SqlCommandColumnEncryptionSetting ColumnEncryptionSetting { get => _encryptionSetting; set => _encryptionSetting = value; }

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

        internal void SetREcordAffected(int value)
        {
            _recordsAffected = value;
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
