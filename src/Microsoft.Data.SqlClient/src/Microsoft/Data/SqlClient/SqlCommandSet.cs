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
        private static readonly Regex s_sqlIdentifierParser = new(SqlIdentifierPattern, RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.Compiled);

        private static int s_objectTypeCount; // EventSource Counter
        internal readonly int _objectID = System.Threading.Interlocked.Increment(ref s_objectTypeCount);

        private SqlCommand _batchCommand;
        private List<SqlBatchCommand> _commandList;

        internal SqlCommandSet() : base()
        {
            _batchCommand = new SqlCommand();
            _commandList = new List<SqlBatchCommand>();
        }

        internal SqlCommand BatchCommand
        {
            get
            {
                SqlCommand command = _batchCommand;
                if (command == null)
                {
                    throw ADP.ObjectDisposed(this);
                }
                return command;
            }
        }

        internal int CommandCount => CommandList.Count;

        internal List<SqlBatchCommand> CommandList
        {
            get
            {
                List<SqlBatchCommand> commandList = _commandList;
                if (commandList == null)
                {
                    throw ADP.ObjectDisposed(this);
                }
                return commandList;
            }
        }

        internal int CommandTimeout
        {
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
            ADP.CheckArgumentNull(command, nameof(command));
            SqlClientEventSource.Log.TryTraceEvent("SqlCommandSet.Append | API | Object Id {0}, Command '{1}', Parameter Count {2}", ObjectID, command.ObjectID, command.Parameters.Count);
            string cmdText = command.CommandText;
            if (string.IsNullOrEmpty(cmdText))
            {
                throw ADP.CommandTextRequired(nameof(Append));
            }

            CommandType commandType = command.CommandType;
            switch (commandType)
            {
                case CommandType.Text:
                case CommandType.StoredProcedure:
                    break;
                case CommandType.TableDirect:
                    throw SQL.NotSupportedCommandType(commandType);
                default:
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
                    SqlParameter p = new();
                    collection[i].CopyTo(p);
                    parameters.Add(p);

                    // SQL Injection awareness
                    if (!s_sqlIdentifierParser.IsMatch(p.ParameterName))
                    {
                        throw ADP.BadParameterName(p.ParameterName);
                    }
                }

                foreach (SqlParameter p in parameters)
                {
                    // deep clone the parameter value if byte[] or char[]
                    object obj = p.Value;
                    byte[] byteValues = (obj as byte[]);
                    if (byteValues != null)
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
                        if (charValues != null)
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
                            if (cloneable != null)
                            {
                                p.Value = cloneable.Clone();
                            }
                        }
                    }
                }
            }

            SqlBatchCommand cmd = new SqlBatchCommand(cmdText, parameters, command.CommandType, command.ColumnEncryptionSetting);

            CommandList.Add(cmd);
        }

        internal static void BuildStoredProcedureName(StringBuilder builder, string part)
        {
            if (part != null && (0 < part.Length))
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
            SqlClientEventSource.Log.TryTraceEvent("SqlCommandSet.Clear | API | Object Id {0}", ObjectID);
            DbCommand batchCommand = BatchCommand;
            if (batchCommand != null)
            {
                batchCommand.Parameters.Clear();
                batchCommand.CommandText = null;
            }
            List<SqlBatchCommand> commandList = _commandList;
            if (commandList != null)
            {
                commandList.Clear();
            }
        }

        internal void Dispose()
        {
            SqlClientEventSource.Log.TryTraceEvent("SqlCommandSet.Dispose | API | Object Id {0}", ObjectID);
            SqlCommand command = _batchCommand;
            _commandList = null;
            _batchCommand = null;

            if (command != null)
            {
                command.Dispose();
            }
        }

        internal int ExecuteNonQuery()
        {
#if NETFRAMEWORK
            SqlConnection.ExecutePermission.Demand();
#endif
            using (TryEventScope.Create("SqlCommandSet.ExecuteNonQuery | API | Object Id {0}, Commands executed in Batch RPC mode", ObjectID))
            {
                ValidateCommandBehavior(nameof(ExecuteNonQuery), CommandBehavior.Default);

                BatchCommand.SetBatchRPCMode(true);
                BatchCommand.Parameters.Clear();
                for (int index = 0; index < _commandList.Count; index++)
                {
                    BatchCommand.AddBatchCommand(_commandList[index]);
                }
                BatchCommand.SetBatchRPCModeReadyToExecute();
                return BatchCommand.ExecuteNonQuery();
            }
        }

        internal SqlParameter GetParameter(int commandIndex, int parameterIndex)
            => CommandList[commandIndex].Parameters[parameterIndex];

        internal bool GetBatchedAffected(int commandIdentifier, out int recordsAffected, out Exception error)
        {
            error = BatchCommand.GetErrors(commandIdentifier);
            int? affected = BatchCommand.GetRecordsAffected(commandIdentifier);
            recordsAffected = affected.GetValueOrDefault();
            return affected.HasValue;
        }

        internal int GetParameterCount(int commandIndex)
            => CommandList[commandIndex].Parameters.Count;

        internal void ValidateCommandBehavior(string method, CommandBehavior behavior)
        {
            if (0 != (behavior & ~(CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection)))
            {
                ADP.ValidateCommandBehavior(behavior);
                throw ADP.NotSupportedCommandBehavior(behavior & ~(CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection), method);
            }
        }
    }
}
