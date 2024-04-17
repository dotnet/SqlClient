// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Configurable retry logic manager;
    /// Receive the default providers by a loader and feeds connections and commands.
    /// </summary>
    internal sealed class SqlConfigurableRetryLogicManager
    {
        private const string TypeName = nameof(SqlConfigurableRetryLogicManager);

#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "Usages of this property annotated with RequiresUnreferencedCode")]
#endif
        private static readonly Lazy<SqlConfigurableRetryLogicLoader> s_loader =
            new Lazy<SqlConfigurableRetryLogicLoader>(InitializeSqlConfigurableRetryLogicProvider);

#if NET6_0_OR_GREATER
        [RequiresUnreferencedCode("Calls Microsoft.Data.SqlClient.SqlConfigurableRetryLogicLoader.SqlConfigurableRetryLogicLoader(ISqlConfigurableRetryConnectionSection, ISqlConfigurableRetryCommandSection, String, String)")]
#endif
        private static SqlConfigurableRetryLogicLoader InitializeSqlConfigurableRetryLogicProvider()
        {
            ISqlConfigurableRetryConnectionSection cnnConfig = null;
            ISqlConfigurableRetryCommandSection cmdConfig = null;

            // Fetch the section attributes values from the configuration section of the app config file.
            cnnConfig = AppConfigManager.FetchConfigurationSection<SqlConfigurableRetryConnectionSection>(SqlConfigurableRetryConnectionSection.Name);
            cmdConfig = AppConfigManager.FetchConfigurationSection<SqlConfigurableRetryCommandSection>(SqlConfigurableRetryCommandSection.Name);

            return new SqlConfigurableRetryLogicLoader(cnnConfig, cmdConfig);
        }

        private SqlConfigurableRetryLogicManager() {/*prevent external object creation*/}

        /// <summary>
        /// Default Retry provider for SqlConnections
        /// </summary>
        internal static SqlRetryLogicBaseProvider ConnectionProvider
        {
#if NET6_0_OR_GREATER
            [RequiresUnreferencedCode("ConnectionProvider may read configuration from app.config which is unsafe for trimming")]
#endif
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
#if NET6_0_OR_GREATER
            [RequiresUnreferencedCode("CommandProvider may read configuration from app.config which is unsafe for trimming")]
#endif
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
