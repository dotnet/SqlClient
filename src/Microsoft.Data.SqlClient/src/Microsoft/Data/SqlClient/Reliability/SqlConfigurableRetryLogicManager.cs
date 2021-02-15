// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Configurable retry logic manager
    /// </summary>
    internal partial class SqlConfigurableRetryLogicManager : SqlConfigurableRetryLogicManagerBase
    {
        private static readonly string s_typeName = typeof(SqlConfigurableRetryLogicManager).Name;

        private static readonly Lazy<SqlConfigurableRetryLogicManager> s_instance =
            new Lazy<SqlConfigurableRetryLogicManager>(() => new SqlConfigurableRetryLogicManager());

        private SqlRetryLogicBaseProvider s_connectionProvoder = null;
        private SqlRetryLogicBaseProvider s_commandProvider = null;

        /// <summary>
        /// The default none retry provider will apply if a parameter passes by null.
        /// </summary>
        private void AssignProviders(SqlRetryLogicBaseProvider cnnProvider = null, SqlRetryLogicBaseProvider cmdProvider = null)
        {
            s_connectionProvoder = cnnProvider ?? SqlConfigurableRetryFactory.CreateNoneRetryProvider();
            s_commandProvider = cmdProvider ?? SqlConfigurableRetryFactory.CreateNoneRetryProvider();
        }

        /// <summary>
        /// Default Retry provider for SqlConnections
        /// </summary>
        internal static SqlRetryLogicBaseProvider ConnectionProvider
        {
            get
            {
                try
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Requested the {1} value."
                                                            , s_typeName, nameof(ConnectionProvider));
                    return s_instance.Value.s_connectionProvoder;
                }
                catch (Exception e)
                {
                    StackTrace stackTrace = new StackTrace();
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> An exception occurred in access to the instance of the class, and the default none-retriable provider will apply: {2}"
                                                            , s_typeName, nameof(ConnectionProvider), e);
                    SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.{0}.{1}|ADV|INFO> An exception occurred in access to the instance of the class, and the default none-retriable provider will apply: {2}"
                                                            , s_typeName, nameof(ConnectionProvider), stackTrace);
                    return SqlConfigurableRetryFactory.CreateNoneRetryProvider();
                }
            }
        }

        /// <summary>
        /// Default Retry provider for SqlCommands
        /// </summary>
        internal static SqlRetryLogicBaseProvider CommandProvider
        {
            get
            {
                try
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Requested the {1} value."
                                                            , s_typeName, nameof(CommandProvider));
                    return s_instance.Value.s_commandProvider;
                }
                catch (Exception e)
                {
                    StackTrace stackTrace = new StackTrace();
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> An exception occurred in access to the instance of the class, and the default none-retriable provider will apply: {2}"
                                                            , s_typeName, nameof(CommandProvider), e);
                    SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.{0}.{1}|ADV|INFO> An exception occurred in access to the instance of the class, and the default none-retriable provider will apply: {2}"
                                                            , s_typeName, nameof(CommandProvider), stackTrace);
                    return SqlConfigurableRetryFactory.CreateNoneRetryProvider();
                }
            }
        }
    }

    internal class SqlConfigurableRetryLogicManagerBase
    {
        public SqlConfigurableRetryLogicManagerBase()
        {
            ApplyContextSwitches();
        }

        /// <summary>
        /// To support the AppContext's set switch through the config file for .NET Core; 
        /// .Net Framework supports it internally through the configuration file by 'AppContextSwitchOverrides' element under 'runtime' section
        /// </summary>
        protected virtual void ApplyContextSwitches()
        { /* No action is required for .Net Framework & .Net Standard */ }
    }
}
