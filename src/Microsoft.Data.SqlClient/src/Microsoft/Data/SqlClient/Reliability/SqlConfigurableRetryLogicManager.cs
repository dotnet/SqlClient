// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Configurable retry logic manager;
    /// Receive the default providers by a loader and feeds connections and commands.
    /// </summary>
    internal sealed partial class SqlConfigurableRetryLogicManager
    {
        private const string TypeName = nameof(SqlConfigurableRetryLogicManager);

        private SqlConfigurableRetryLogicManager() {/*prevent external object creation*/}

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
                                                            , TypeName, nameof(ConnectionProvider));
                    return s_loader.Value.ConnectionProvider;
                }
                catch (Exception e)
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> An exception occurred in access to the instance of the class, and the default non-retriable provider will apply: {2}"
                                                            , TypeName, nameof(ConnectionProvider), e);
                    if (SqlClientEventSource.Log.IsAdvancedTraceOn())
                    {
                        StackTrace stackTrace = new StackTrace();
                        SqlClientEventSource.Log.AdvancedTraceEvent("<sc.{0}.{1}|ADV|INFO> An exception occurred in access to the instance of the class, and the default non-retriable provider will apply: {2}"
                                                            , TypeName, nameof(ConnectionProvider), stackTrace);
                    }
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
                                                            , TypeName, nameof(CommandProvider));
                    return s_loader.Value.CommandProvider;
                }
                catch (Exception e)
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> An exception occurred in access to the instance of the class, and the default non-retriable provider will apply: {2}"
                                                            , TypeName, nameof(CommandProvider), e);
                    if (SqlClientEventSource.Log.IsAdvancedTraceOn())
                    {
                        StackTrace stackTrace = new StackTrace();
                        SqlClientEventSource.Log.AdvancedTraceEvent("<sc.{0}.{1}|ADV|INFO> An exception occurred in access to the instance of the class, and the default non-retriable provider will apply: {2}"
                                                                , TypeName, nameof(CommandProvider), stackTrace);
                    }
                    return SqlConfigurableRetryFactory.CreateNoneRetryProvider();
                }
            }
        }

    }

    /// <summary>
    /// Configurable retry logic loader
    /// </summary>
    internal sealed partial class SqlConfigurableRetryLogicLoader
    {
        private const string TypeName = nameof(SqlConfigurableRetryLogicLoader);

        /// <summary>
        /// The default non retry provider will apply if a parameter passes by null.
        /// </summary>
        private void AssignProviders(SqlRetryLogicBaseProvider cnnProvider = null, SqlRetryLogicBaseProvider cmdProvider = null)
        {
            ConnectionProvider = cnnProvider ?? SqlConfigurableRetryFactory.CreateNoneRetryProvider();
            CommandProvider = cmdProvider ?? SqlConfigurableRetryFactory.CreateNoneRetryProvider();
        }

        /// <summary>
        /// Default Retry provider for SqlConnections
        /// </summary>
        internal SqlRetryLogicBaseProvider ConnectionProvider { get; private set; }

        /// <summary>
        /// Default Retry provider for SqlCommands
        /// </summary>
        internal SqlRetryLogicBaseProvider CommandProvider { get; private set; }
    }

    internal interface IAppContextSwitchOverridesSection
    {
        string Value { get; set; }
    }

    internal interface ISqlConfigurableRetryConnectionSection
    {
        TimeSpan DeltaTime { get; set; }
        TimeSpan MaxTimeInterval { get; set; }
        TimeSpan MinTimeInterval { get; set; }
        int NumberOfTries { get; set; }
        string RetryLogicType { get; set; }
        string RetryMethod { get; set; }
        string TransientErrors { get; set; }
    }

    internal interface ISqlConfigurableRetryCommandSection : ISqlConfigurableRetryConnectionSection
    {
        string AuthorizedSqlCondition { get; set; }
    }
}
