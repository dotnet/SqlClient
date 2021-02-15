// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Configurable retry logic manager
    /// </summary>
    internal partial class SqlConfigurableRetryLogicManager : SqlConfigurableRetryLogicManagerBase
    {
        private const string NullConst = "null";

        private SqlConfigurableRetryLogicManager()
        {
#if !NETFRAMEWORK
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += Default_Resolving;
#endif
            AssignProviders(CreateRetryLogicProvider<SqlConfigurableRetryConnectionConfigurationSection>(SqlConfigurableRetryConnectionConfigurationSection.Name),
                            CreateRetryLogicProvider<SqlConfigurableRetryCommandConfigurationSection>(SqlConfigurableRetryCommandConfigurationSection.Name));
        }

        private static SqlRetryLogicBaseProvider CreateRetryLogicProvider<T>(string sectionName)
            where T : SqlConfigurableRetryConnectionConfigurationSection
        {
            var methodName = MethodBase.GetCurrentMethod().Name;
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Entry point.", s_typeName, methodName);

            try
            {
                // Fetch the section attributes values from the configuration section from the app config file.
                var _configSection = FetchConfigurationSection<T>(sectionName) as SqlConfigurableRetryConnectionConfigurationSection;
                if (_configSection == null)
                {
                    throw new ConfigurationErrorsException($"Couldn't load a setting for section '{sectionName}'.");
                }
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Successfully loaded the configurable retry logic settings from the configuration file's section '{2}'.",
                                                       s_typeName, methodName, sectionName);

                // Create a SqlRetryLogicOption object from the discovered retry logic values
                var retryOption = new SqlRetryLogicOption()
                {
                    NumberOfTries = _configSection.NumberOfTries,
                    DeltaTime = _configSection.DeltaTime,
                    MinTimeInterval = _configSection.MinTimeInterval,
                    MaxTimeInterval = _configSection.MaxTimeInterval
                };

                // Prepare the transient error lists
                if (!string.IsNullOrEmpty(_configSection.TransientErrors))
                {
                    retryOption.TransientErrors = _configSection.TransientErrors.Split(',').Select(x => Convert.ToInt32(x)).ToList();
                }

                // Prepare the authorized SQL statement just for SqlCommands
                if (_configSection is SqlConfigurableRetryCommandConfigurationSection cmdSection && !string.IsNullOrEmpty(cmdSection.AuthorizedSqlCondition))
                {
                    retryOption.AuthorizedSqlCondition = (x) => Regex.IsMatch(x, cmdSection.AuthorizedSqlCondition);
                }
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Successfully created a {2} object to use on creating a retry logic provider from the section '{3}'.",
                                                       s_typeName, methodName, nameof(SqlRetryLogicOption), sectionName);

                // Extract the SqlRetryLogicBaseProvider object by the given information
                var provider = ResolveRetryLogicProvider(_configSection.RetryLogicType, _configSection.RetryMethod, retryOption);
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Successfully created a {2} object from the section '{3}'.",
                                                       s_typeName, methodName, nameof(SqlRetryLogicBaseProvider), sectionName);
                return provider;
            }
            catch (ConfigurationErrorsException e)
            {
                // Don't throw an error for invalid config files
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO>: Unable to load custom {2}. ConfigurationManager failed to load due to the configuration errors: {3}",
                                                       s_typeName, methodName, sectionName, e);
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> {2}",
                                                       s_typeName, methodName, e);
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Due to the exception occurrence, the default none-retriable logic will apply",
                                                    s_typeName, methodName);
            // Return default provider if an exception occured.
            return SqlConfigurableRetryFactory.CreateNoneRetryProvider();
        }

        private static SqlRetryLogicBaseProvider ResolveRetryLogicProvider(string configurableRetryType, string retryMethod, SqlRetryLogicOption option)
        {
            var methodName = MethodBase.GetCurrentMethod().Name;
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Entry point.", s_typeName, methodName);

            if (string.IsNullOrEmpty(retryMethod))
            {
                throw new ArgumentNullException($"Failed to create {nameof(SqlRetryLogicBaseProvider)} object because the {nameof(retryMethod)} value is null or empty.");
            }

            Type type = null;
            try
            {
                // Resolve an Type object from the given type name
                // Different implementation in .NET Framework & .NET Core
                type = LoadType(configurableRetryType);
            }
            catch (Exception e)
            {
                // Try to use the 'SqlConfigurableRetryFactory' as a default type to discover the retry methods
                // if there was any problem to resolve the 'configurableRetryType' type.
                type = typeof(SqlConfigurableRetryFactory);
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Unable to load the '{2}' type; Tries to use the internal `{3}` type: {4}",
                                                       s_typeName, methodName, configurableRetryType, type.FullName, e);
            }

            // Run the function by the resolved values to get the SqlRetryLogicBaseProvider object
            try
            {
                // Create an instance from the discovered type by its default cunstructor
                object result = CreateInstance(type, retryMethod, option);

                if (result is SqlRetryLogicBaseProvider provider)
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> The created instace is a {2} type.",
                                                           s_typeName, methodName, typeof(SqlRetryLogicBaseProvider).FullName);
                    provider.Retrying += OnRetryingEvent;
                    return provider;
                }
            }
            catch (Exception e)
            {
                // Openning a connection or executing a command inside this function 
                // runs the application to the `TargetInvocationException`.
                // And using an isolated zone like a specific AppDomain makes an infinit loop.
                throw new Exception($"Exception is occured to run the `{type.FullName}.{retryMethod}()` method.", e);
            }

            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Unable to resolve a valid provider; Returns `null`.", s_typeName, methodName);
            return null;
        }

        private static object CreateInstance(Type type, string retryMethodName, SqlRetryLogicOption option)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Entry point.", s_typeName, methodName);

            if (type == typeof(SqlConfigurableRetryFactory) || type == null)
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> The given type `{2}` infers as internal `{3}` type."
                                                      , s_typeName, methodName, type?.Name ?? NullConst, typeof(SqlConfigurableRetryFactory).FullName);
                MethodInfo internalMethod = typeof(SqlConfigurableRetryFactory).GetMethod(retryMethodName);
                if (internalMethod == null)
                {
                    throw new InvalidOperationException($"Failed to resolve the '{retryMethodName}' method from `{typeof(SqlConfigurableRetryFactory).FullName}` type.");
                }

                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> The `{2}.{3}()` method is discovered by `{4}` method name."
                                                      , s_typeName, methodName, internalMethod.ReflectedType.FullName, internalMethod.Name, retryMethodName);
                object[] internalFuncParams = PrepareParamValues(internalMethod.GetParameters(), option, retryMethodName);
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Parameters are prepared to invoke the `{2}.{3}()` method."
                                                      , s_typeName, methodName, internalMethod.ReflectedType.FullName, internalMethod.Name);
                return internalMethod.Invoke(null, internalFuncParams);
            }

            // Searches for the public MethodInfo from the specified type by the given method name
            // It is a case-sensitive search
            MethodInfo method = type.GetMethod(retryMethodName);
            if (method == null)
            {
                throw new InvalidOperationException($"Failed to resolve the '{retryMethodName}' method from `{type.FullName}` type.");
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> The `{2}` method metadata is extracted from the `{3}` type by `{4}` method name."
                                                    , s_typeName, methodName, method.Name, type.FullName, retryMethodName);

            if (!typeof(SqlRetryLogicBaseProvider).IsAssignableFrom(method.ReturnType))
            {
                throw new InvalidCastException($"Invalid return type; It must be as `{typeof(SqlRetryLogicBaseProvider).FullName}` type.");
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> The return type of the `{2}.{3}()` method is valid."
                                                    , s_typeName, methodName, type.FullName, method.Name);

            // Prepare the function parameters values
            object[] funcParams = PrepareParamValues(method.GetParameters(), option, retryMethodName);

            if(method.IsStatic)
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Run the static `{2}` method without object creation of `{3}` type.",
                                                       s_typeName, methodName, method.Name, type.FullName);
                return method.Invoke(null, funcParams);
            }

            // Since there is no information about the parameters of the possible constructors, 
            // the only acceptable constructor is the default constructor.
            object obj = Activator.CreateInstance(type);
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> An instance of `{2}` type is created to invoke the `{3}` method.",
                                                   s_typeName, methodName, type.FullName, method.Name);
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
                var paramInfo = parameterInfos[i];

                // Create parameters with default values that are not as SqlRetryLogicOption type.
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
                                                  , s_typeName, MethodBase.GetCurrentMethod().Name, typeof(SqlConfigurableRetryFactory).FullName, retryMethod);
            return funcParams;
        }

        /// <summary>
        /// Used to log the attempts
        /// </summary>
        private static void OnRetryingEvent(object sender, SqlRetryingEventArgs args)
        {
            var lastException = args.Exceptions[args.Exceptions.Count - 1];
            var msg = string.Format("<sc.{0}.OnRetryingEvent|INFO>: Default configurable retry logic for {1} object. attempts count:{2}, upcoming delay:{3}",
                                    s_typeName, sender.GetType().Name, args.RetryCount, args.Delay);

            SqlClientEventSource.Log.TryTraceEvent("{0}, Last exception:<{1}>", msg, lastException.Message);
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<ADV>{0}, Last exception: {1}", msg, lastException);
        }

        /// <summary>
        /// Fetch the specified configuration section from the configuration file
        /// </summary>
        /// <returns>The specified `T` object or `null` if the section doesn't exist.</returns>
        private static T FetchConfigurationSection<T>(string name)
        {
            Type t = typeof(T);
            object section = ConfigurationManager.GetSection(name);
            if (null != section)
            {
                if (section is ConfigurationSection configSection && configSection.GetType() == t)
                {
                    return (T)section;
                }
                else
                {
                    SqlClientEventSource.Log.TraceEvent("<sc.{0}.FetchConfigurationSection|INFO>: Found a custom {1} configuration but it is not of type {2}.",
                                                        s_typeName, name, t.FullName);
                }
            }
            return default;
        }
    }

    /// <summary>
    /// The configuration section definition for reading a configuration file.
    /// </summary>
    internal class SqlConfigurableRetryConnectionConfigurationSection : ConfigurationSection
    {
        public const string Name = "SqlConfigurableRetryLogicConnection";

        [ConfigurationProperty("retryLogicType")]
        // Fully qualified name
        public string RetryLogicType => this["retryLogicType"] as string;

        [ConfigurationProperty("retryMethod", IsRequired = true)]
        public string RetryMethod => this["retryMethod"] as string;

        [ConfigurationProperty("numberOfTries", IsRequired = true, DefaultValue = 1)]
        [IntegerValidator(MinValue = 1, MaxValue = 60, ExcludeRange = false)]
        public int NumberOfTries => (int)this["numberOfTries"];

        [ConfigurationProperty("deltaTime", IsRequired = true)]
        [TimeSpanValidator(MinValueString = "00:00:00", MaxValueString = "00:02:00", ExcludeRange = false)]
        public TimeSpan DeltaTime => (TimeSpan)this["deltaTime"];

        [ConfigurationProperty("minTime", IsRequired = false, DefaultValue = "00:00:00")]
        [TimeSpanValidator(MinValueString = "00:00:00", MaxValueString = "00:02:00", ExcludeRange = false)]
        public TimeSpan MinTimeInterval => (TimeSpan)this["minTime"];

        [ConfigurationProperty("maxTime", IsRequired = false)]
        [TimeSpanValidator(MinValueString = "00:00:00", MaxValueString = "00:02:00", ExcludeRange = false)]
        public TimeSpan MaxTimeInterval => (TimeSpan)this["maxTime"];

        [ConfigurationProperty("transientErrors", IsRequired = false)]
        [RegexStringValidator(@"^([ \t]*\d+(?:[ \t]*,[ \t]*\d+)*[ \t]*)*$")]
        // Comma-separated error numbers
        public string TransientErrors => this["transientErrors"] as string;
    }

    /// <summary>
    /// The configuration section definition for reading a configuration file.
    /// </summary>
    internal sealed class SqlConfigurableRetryCommandConfigurationSection : SqlConfigurableRetryConnectionConfigurationSection
    {
        public new const string Name = "SqlConfigurableRetryLogicCommand";

        [ConfigurationProperty("authorizedSqlCondition", IsRequired = false)]
        public string AuthorizedSqlCondition => this["authorizedSqlCondition"] as string;
    }
}
