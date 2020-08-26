// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlCommandSet
    {

        private const string SqlIdentifierPattern = "^@[\\p{Lo}\\p{Lu}\\p{Ll}\\p{Lm}_@#][\\p{Lo}\\p{Lu}\\p{Ll}\\p{Lm}\\p{Nd}\uff3f_@#\\$]*$";
        private static readonly Regex SqlIdentifierParser = new Regex(SqlIdentifierPattern, RegexOptions.ExplicitCapture | RegexOptions.Singleline);

        private List<LocalCommand> _commandList = new List<LocalCommand>();

        private SqlCommand _batchCommand;

        private static int _objectTypeCount; // EventSource Counter
        internal readonly int _objectID = System.Threading.Interlocked.Increment(ref _objectTypeCount);

        private sealed class LocalCommand
        {
            internal readonly string CommandText;
            internal readonly SqlParameterCollection Parameters;
            internal readonly int ReturnParameterIndex;
            internal readonly CommandType CmdType;
            internal readonly SqlCommandColumnEncryptionSetting ColumnEncryptionSetting;

            internal LocalCommand(string commandText, SqlParameterCollection parameters, int returnParameterIndex, CommandType cmdType, SqlCommandColumnEncryptionSetting columnEncryptionSetting)
            {
                Debug.Assert(0 <= commandText.Length, "no text");
                this.CommandText = commandText;
                this.Parameters = parameters;
                this.ReturnParameterIndex = returnParameterIndex;
                this.CmdType = cmdType;
                this.ColumnEncryptionSetting = columnEncryptionSetting;
            }
        }

        internal SqlCommandSet() : base()
        {
            _batchCommand = new SqlCommand();
        }

        private SqlCommand BatchCommand
        {
            get
            {
                SqlCommand command = _batchCommand;
                if (null == command)
                {
                    throw ADP.ObjectDisposed(this);
                }
                return command;
            }
        }

        internal int CommandCount
        {
            get
            {
                return CommandList.Count;
            }
        }

        private List<LocalCommand> CommandList
        {
            get
            {
                List<LocalCommand> commandList = _commandList;
                if (null == commandList)
                {
                    throw ADP.ObjectDisposed(this);
                }
                return commandList;
            }
        }

        internal int CommandTimeout
        {
            /*get {
                return BatchCommand.CommandTimeout;
            }*/
            set
            {
                BatchCommand.CommandTimeout = value;
            }
        }

        internal SqlConnection Connection
        {
            get
            {
                return BatchCommand.Connection;
            }
            set
            {
                BatchCommand.Connection = value;
            }
        }

        internal SqlTransaction Transaction
        {
            /*get {
                return BatchCommand.Transaction;
            }*/
            set
            {
                BatchCommand.Transaction = value;
            }
        }

        internal int ObjectID
        {
            get
            {
                return _objectID;
            }
        }

        internal void Append(SqlCommand command)
        {
            ADP.CheckArgumentNull(command, "command");
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlCommandSet.Append|API> {0}, command={1}, parameterCount={2}", ObjectID, command.ObjectID, command.Parameters.Count);
            string cmdText = command.CommandText;

            if (ADP.IsEmpty(cmdText))
            {
                throw ADP.CommandTextRequired(ADP.Append);
            }

            CommandType commandType = command.CommandType;
            switch (commandType)
            {
                case CommandType.Text:
                case CommandType.StoredProcedure:
                    break;
                case CommandType.TableDirect:
                    Debug.Assert(false, "command.CommandType");
                    throw Microsoft.Data.SqlClient.SQL.NotSupportedCommandType(commandType);
                default:
                    Debug.Assert(false, "command.CommandType");
                    throw ADP.InvalidCommandType(commandType);
            }

            SqlParameterCollection parameters = null;

            SqlParameterCollection collection = command.Parameters;
            if (0 < collection.Count)
            {
                parameters = new SqlParameterCollection();

                // clone parameters so they aren't destroyed
                for (int i = 0; i < collection.Count; ++i)
                {
                    SqlParameter p = new SqlParameter();
                    collection[i].CopyTo(p);
                    parameters.Add(p);

                    // SQL Injection awarene
                    if (!SqlIdentifierParser.IsMatch(p.ParameterName))
                    {
                        throw ADP.BadParameterName(p.ParameterName);
                    }
                }

                foreach (SqlParameter p in parameters)
                {
                    // deep clone the parameter value if byte[] or char[]
                    object obj = p.Value;
                    byte[] byteValues = (obj as byte[]);
                    if (null != byteValues)
                    {
                        int offset = p.Offset;
                        int size = p.Size;
                        int countOfBytes = byteValues.Length - offset;
                        if ((size > 0) && (size < countOfBytes))
                        {
                            countOfBytes = size;
                        }
                        byte[] copy = new byte[Math.Max(countOfBytes, 0)];
                        Buffer.BlockCopy(byteValues, offset, copy, 0, copy.Length);
                        p.Offset = 0;
                        p.Value = copy;
                    }
                    else
                    {
                        char[] charValues = (obj as char[]);
                        if (null != charValues)
                        {
                            int offset = p.Offset;
                            int size = p.Size;
                            int countOfChars = charValues.Length - offset;
                            if ((0 != size) && (size < countOfChars))
                            {
                                countOfChars = size;
                            }
                            char[] copy = new char[Math.Max(countOfChars, 0)];
                            Buffer.BlockCopy(charValues, offset, copy, 0, copy.Length * 2);
                            p.Offset = 0;
                            p.Value = copy;
                        }
                        else
                        {
                            ICloneable cloneable = (obj as ICloneable);
                            if (null != cloneable)
                            {
                                p.Value = cloneable.Clone();
                            }
                        }
                    }
                }
            }

            int returnParameterIndex = -1;
            if (null != parameters)
            {
                for (int i = 0; i < parameters.Count; ++i)
                {
                    if (ParameterDirection.ReturnValue == parameters[i].Direction)
                    {
                        returnParameterIndex = i;
                        break;
                    }
                }
            }
            LocalCommand cmd = new LocalCommand(cmdText, parameters, returnParameterIndex, command.CommandType, command.ColumnEncryptionSetting);
            CommandList.Add(cmd);
        }

        internal static void BuildStoredProcedureName(StringBuilder builder, string part)
        {
            if ((null != part) && (0 < part.Length))
            {
                if ('[' == part[0])
                {
                    int count = 0;
                    foreach (char c in part)
                    {
                        if (']' == c)
                        {
                            count++;
                        }
                    }
                    if (1 == (count % 2))
                    {
                        builder.Append(part);
                        return;
                    }
                }

                // the part is not escaped, escape it now
                SqlServerEscapeHelper.EscapeIdentifier(builder, part);
            }
        }

        internal void Clear()
        {
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlCommandSet.Clear|API> {0}", ObjectID);
            DbCommand batchCommand = BatchCommand;
            if (null != batchCommand)
            {
                batchCommand.Parameters.Clear();
                batchCommand.CommandText = null;
            }
            List<LocalCommand> commandList = _commandList;
            if (null != commandList)
            {
                commandList.Clear();
            }
        }

        internal void Dispose()
        {
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlCommandSet.Dispose|API> {0}", ObjectID);
            SqlCommand command = _batchCommand;
            _commandList = null;
            _batchCommand = null;

            if (null != command)
            {
                command.Dispose();
            }
        }

        internal int ExecuteNonQuery()
        {
            SqlConnection.ExecutePermission.Demand();
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlCommandSet.ExecuteNonQuery|API> {0}", ObjectID);

            try
            {
                if (Connection.IsContextConnection)
                {
                    throw SQL.BatchedUpdatesNotAvailableOnContextConnection();
                }
                ValidateCommandBehavior(ADP.ExecuteNonQuery, CommandBehavior.Default);
                BatchCommand.BatchRPCMode = true;
                BatchCommand.ClearBatchCommand();
                BatchCommand.Parameters.Clear();
                for (int ii = 0; ii < _commandList.Count; ii++)
                {
                    LocalCommand cmd = _commandList[ii];
                    BatchCommand.AddBatchCommand(cmd.CommandText, cmd.Parameters, cmd.CmdType, cmd.ColumnEncryptionSetting);
                }
                return BatchCommand.ExecuteBatchRPCCommand();
            }
            finally
            {
                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
            }
        }

        internal SqlParameter GetParameter(int commandIndex, int parameterIndex)
        {
            return CommandList[commandIndex].Parameters[parameterIndex];
        }

        internal bool GetBatchedAffected(int commandIdentifier, out int recordsAffected, out Exception error)
        {
            error = BatchCommand.GetErrors(commandIdentifier);
            int? affected = BatchCommand.GetRecordsAffected(commandIdentifier);
            recordsAffected = affected.GetValueOrDefault();
            return affected.HasValue;
        }

        internal int GetParameterCount(int commandIndex)
        {
            return CommandList[commandIndex].Parameters.Count;
        }

        private void ValidateCommandBehavior(string method, CommandBehavior behavior)
        {
            if (0 != (behavior & ~(CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection)))
            {
                ADP.ValidateCommandBehavior(behavior);
                throw ADP.NotSupportedCommandBehavior(behavior & ~(CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection), method);
            }
        }
    }
}

