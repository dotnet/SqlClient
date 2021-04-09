// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Configurable retry logic manager;
    /// Receive the default providers by a loader and feeds the connections and commands.
    /// </summary>
    internal sealed partial class SqlConfigurableRetryLogicManager
    {
        private static readonly Lazy<SqlConfigurableRetryLogicLoader> s_loader =
            new Lazy<SqlConfigurableRetryLogicLoader>(() =>
            {
                ISqlConfigurableRetryConnectionSection cnnConfig = null;
                ISqlConfigurableRetryCommandSection cmdConfig = null;

                // Fetch the section attributes values from the configuration section of the app config file.
                cnnConfig = AppConfigManager.FetchConfigurationSection<SqlConfigurableRetryConnectionSection>(SqlConfigurableRetryConnectionSection.Name);
                cmdConfig = AppConfigManager.FetchConfigurationSection<SqlConfigurableRetryCommandSection>(SqlConfigurableRetryCommandSection.Name);
#if !NETFRAMEWORK
                IAppContextSwitchOverridesSection appContextSwitch = AppConfigManager.FetchConfigurationSection<AppContextSwitchOverridesSection>(AppContextSwitchOverridesSection.Name);
                try
                {
                    SqlAppContextSwitchManager.ApplyContextSwitches(appContextSwitch);
                }
                catch (Exception e)
                {
                    // Don't throw an exception for an invalid config file
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO>: {2}", TypeName, MethodBase.GetCurrentMethod().Name, e);
                }
#endif
                return new SqlConfigurableRetryLogicLoader(cnnConfig, cmdConfig);
            });
    }

    /// <summary>
    /// Configurable retry logic loader
    /// This class shouldn't throw exceptions;
    /// All exceptions should be handled internally and logged with Event Source.
    /// </summary>
    internal sealed partial class SqlConfigurableRetryLogicLoader
    {
        public SqlConfigurableRetryLogicLoader(ISqlConfigurableRetryConnectionSection connectionRetryConfigs,
                                               ISqlConfigurableRetryCommandSection commandRetryConfigs,
                                               string cnnSectionName = SqlConfigurableRetryConnectionSection.Name,
                                               string cmdSectionName = SqlConfigurableRetryCommandSection.Name)
        {
#if !NETFRAMEWORK
            // Just only one subscription to this event is required.
            // This class isn't supposed to be called more than one time;
            // SqlConfigurableRetryLogicManager manages a single instance of this class.
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving -= Default_Resolving;
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += Default_Resolving;
#endif
            AssignProviders(connectionRetryConfigs == null ? null : CreateRetryLogicProvider(cnnSectionName, connectionRetryConfigs),
                            commandRetryConfigs == null ? null : CreateRetryLogicProvider(cmdSectionName, commandRetryConfigs));
        }

        private static SqlRetryLogicBaseProvider CreateRetryLogicProvider(string sectionName, ISqlConfigurableRetryConnectionSection configSection)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Entry point.", TypeName, methodName);

            try
            {
                // Create a SqlRetryLogicOption object from the discovered retry logic values
                var retryOption = new SqlRetryLogicOption()
                {
                    NumberOfTries = configSection.NumberOfTries,
                    DeltaTime = configSection.DeltaTime,
                    MinTimeInterval = configSection.MinTimeInterval,
                    MaxTimeInterval = configSection.MaxTimeInterval
                };

                // Prepare the transient error lists
                if (!string.IsNullOrEmpty(configSection.TransientErrors))
                {
                    retryOption.TransientErrors = configSection.TransientErrors.Split(',').Select(x => Convert.ToInt32(x)).ToList();
                }

                // Prepare the authorized SQL statement just for SqlCommands
                if (configSection is ISqlConfigurableRetryCommandSection cmdSection && !string.IsNullOrEmpty(cmdSection.AuthorizedSqlCondition))
                {
                    retryOption.AuthorizedSqlCondition = (x) => Regex.IsMatch(x, cmdSection.AuthorizedSqlCondition);
                }
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Successfully created a {2} object to use on creating a retry logic provider from the section '{3}'.",
                                                       TypeName, methodName, nameof(SqlRetryLogicOption), sectionName);

                // Extract the SqlRetryLogicBaseProvider object from the given information
                var provider = ResolveRetryLogicProvider(configSection.RetryLogicType, configSection.RetryMethod, retryOption);
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Successfully created a {2} object from the section '{3}'.",
                                                       TypeName, methodName, nameof(SqlRetryLogicBaseProvider), sectionName);
                return provider;
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> {2}",
                                                       TypeName, methodName, e);
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Due to an exception, the default non-retriable logic will be applied.",
                                                    TypeName, methodName);
            // Return default provider if an exception occured.
            return SqlConfigurableRetryFactory.CreateNoneRetryProvider();
        }

        private static SqlRetryLogicBaseProvider ResolveRetryLogicProvider(string configurableRetryType, string retryMethod, SqlRetryLogicOption option)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Entry point.", TypeName, methodName);

            if (string.IsNullOrEmpty(retryMethod))
            {
                throw new ArgumentNullException($"Failed to create {nameof(SqlRetryLogicBaseProvider)} object because the {nameof(retryMethod)} value is null or empty.");
            }

            Type type = null;
            try
            {
                // Resolve a Type object from the given type name
                // Different implementation in .NET Framework & .NET Core
                type = LoadType(configurableRetryType);
            }
            catch (Exception e)
            {
                // Try to use 'SqlConfigurableRetryFactory' as a default type to discover retry methods
                // if there is a problem, resolve using the 'configurableRetryType' type.
                type = typeof(SqlConfigurableRetryFactory);
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Unable to load the '{2}' type; Trying to use the internal `{3}` type: {4}",
                                                       TypeName, methodName, configurableRetryType, type.FullName, e);
            }

            // Run the function by using the resolved values to get the SqlRetryLogicBaseProvider object
            try
            {
                // Create an instance from the discovered type by its default constructor
                object result = CreateInstance(type, retryMethod, option);

                if (result is SqlRetryLogicBaseProvider provider)
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> The created instace is a {2} type.",
                                                           TypeName, methodName, typeof(SqlRetryLogicBaseProvider).FullName);
                    provider.Retrying += OnRetryingEvent;
                    return provider;
                }
            }
            catch (Exception e)
            {
                // In order to invoke a function dynamically, any type of exception can occur here;
                // The main exception and its stack trace will be accessible through the inner exception.
                // i.e: Opening a connection or executing a command while invoking a function 
                // runs the application to the `TargetInvocationException`.
                // And using an isolated zone like a specific AppDomain results in an infinite loop.
                throw new Exception($"Exception occurred when running the `{type.FullName}.{retryMethod}()` method.", e);
            }

            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Unable to resolve a valid provider; Returns `null`.", TypeName, methodName);
            return null;
        }

        private static object CreateInstance(Type type, string retryMethodName, SqlRetryLogicOption option)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Entry point.", TypeName, methodName);

            if (type == typeof(SqlConfigurableRetryFactory) || type == null)
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> The given type `{2}` infers as internal `{3}` type."
                                                      , TypeName, methodName, type?.Name, typeof(SqlConfigurableRetryFactory).FullName);
                MethodInfo internalMethod = typeof(SqlConfigurableRetryFactory).GetMethod(retryMethodName);
                if (internalMethod == null)
                {
                    throw new InvalidOperationException($"Failed to resolve the '{retryMethodName}' method from `{typeof(SqlConfigurableRetryFactory).FullName}` type.");
                }

                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> The `{2}.{3}()` method has been discovered as the `{4}` method name."
                                                      , TypeName, methodName, internalMethod.ReflectedType.FullName, internalMethod.Name, retryMethodName);
                object[] internalFuncParams = PrepareParamValues(internalMethod.GetParameters(), option, retryMethodName);
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Parameters are prepared to invoke the `{2}.{3}()` method."
                                                      , TypeName, methodName, internalMethod.ReflectedType.FullName, internalMethod.Name);
                return internalMethod.Invoke(null, internalFuncParams);
            }

            // Searches for the public MethodInfo from the specified type by the given method name
            // The search is case-sensitive
            MethodInfo method = type.GetMethod(retryMethodName);
            if (method == null)
            {
                throw new InvalidOperationException($"Failed to resolve the '{retryMethodName}' method from `{type.FullName}` type.");
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> The `{2}` method metadata has been extracted from the `{3}` type by using the `{4}` method name."
                                                    , TypeName, methodName, method.Name, type.FullName, retryMethodName);

            if (!typeof(SqlRetryLogicBaseProvider).IsAssignableFrom(method.ReturnType))
            {
                throw new InvalidCastException($"Invalid return type; Return type must be of `{typeof(SqlRetryLogicBaseProvider).FullName}` type.");
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> The return type of the `{2}.{3}()` method is valid."
                                                    , TypeName, methodName, type.FullName, method.Name);

            // Prepare the function parameters values
            object[] funcParams = PrepareParamValues(method.GetParameters(), option, retryMethodName);

            if (method.IsStatic)
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Run the static `{2}` method without object creation of `{3}` type.",
                                                       TypeName, methodName, method.Name, type.FullName);
                return method.Invoke(null, funcParams);
            }

            // Since there is no information about the parameters of the possible constructors, 
            // the only acceptable constructor is the default constructor.
            object obj = Activator.CreateInstance(type);
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> An instance of `{2}` type is created to invoke the `{3}` method.",
                                                   TypeName, methodName, type.FullName, method.Name);
            return method.Invoke(obj, funcParams);
        }

        private static object[] PrepareParamValues(ParameterInfo[] parameterInfos, SqlRetryLogicOption option, string retryMethod)
        {
            // The retry method must have at least one `SqlRetryLogicOption`
            if (parameterInfos.FirstOrDefault(x => x.ParameterType == typeof(SqlRetryLogicOption)) == null)
            {
                string message = $"Failed to create {nameof(SqlRetryLogicBaseProvider)} object because of invalid {retryMethod}'s parameters." +
                    $"{Environment.NewLine}The function must have a paramter of type '{nameof(SqlRetryLogicOption)}'.";
                throw new InvalidOperationException(message);
            }

            object[] funcParams = new object[parameterInfos.Length];
            for (int i = 0; i < parameterInfos.Length; i++)
            {
                ParameterInfo paramInfo = parameterInfos[i];

                // Create parameters with default values that are not a SqlRetryLogicOption type.
                if (paramInfo.HasDefaultValue && paramInfo.ParameterType != typeof(SqlRetryLogicOption))
                {
                    funcParams[i] = paramInfo.DefaultValue;
                }

                // Assign the 'option' object to the first parameter with 'SqlRetryLogicOption' type
                // neither it doesn't have default value 
                // or there isn't another parameter with the same type and without a default value.
                else if (paramInfo.ParameterType == typeof(SqlRetryLogicOption))
                {
                    if (!paramInfo.HasDefaultValue
                        || (paramInfo.HasDefaultValue
                            && parameterInfos.FirstOrDefault(x => x != paramInfo && !x.HasDefaultValue && x.ParameterType == typeof(SqlRetryLogicOption)) == null))
                    {
                        funcParams[i] = option;
                    }
                    else
                    {
                        funcParams[i] = paramInfo.DefaultValue;
                    }
                }
                else
                {
                    string message = $"Failed to create {nameof(SqlRetryLogicBaseProvider)} object because of invalid {nameof(retryMethod)}'s parameters."
                        + $"{Environment.NewLine}Parameter '{paramInfo.ParameterType.Name} {paramInfo.Name}' doesn't have default value.";
                    throw new InvalidOperationException(message);
                }
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Parameters are prepared to invoke the `{2}.{3}()` method."
                                                  , TypeName, MethodBase.GetCurrentMethod().Name, typeof(SqlConfigurableRetryFactory).FullName, retryMethod);
            return funcParams;
        }

        /// <summary>
        /// Used to log attempts
        /// </summary>
        private static void OnRetryingEvent(object sender, SqlRetryingEventArgs args)
        {
            var lastException = args.Exceptions[args.Exceptions.Count - 1];
            var msg = string.Format("<sc.{0}.OnRetryingEvent|INFO>: Default configurable retry logic for {1} object. attempts count:{2}, upcoming delay:{3}",
                                    TypeName, sender.GetType().Name, args.RetryCount, args.Delay);

            SqlClientEventSource.Log.TryTraceEvent("{0}, Last exception:<{1}>", msg, lastException.Message);
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<ADV>{0}, Last exception: {1}", msg, lastException);
        }
    }
}
