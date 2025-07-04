// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Configuration;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Fetch values from an application configuration file
    /// </summary>
    internal sealed class AppConfigManager
    {
        private const string TypeName = nameof(AppConfigManager);

        /// <summary>
        /// Fetch the specified configuration section from the configuration file
        /// </summary>
        /// <returns>The specified `T` object or default value of `T` if the section doesn't exist.</returns>
        public static T FetchConfigurationSection<T>(string name)
        {
            string methodName = nameof(FetchConfigurationSection);

            object section = null;
            try
            {
                section = ConfigurationManager.GetSection(name);
            }
            catch(Exception e)
            {
                // Doesn't throw an error for invalid config files
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO>: Unable to load section `{2}`. ConfigurationManager failed to load due to configuration errors: {3}",
                                                       TypeName, methodName, name, e);
            }
            if (section != null )
            {
                Type t = typeof(T);
                if (section is ConfigurationSection configSection && configSection.GetType() == t)
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Successfully loaded the configurable retry logic settings from the configuration file's section '{2}'.",
                                                           TypeName, methodName, name);
                    return (T)section;
                }
                else
                {
                    SqlClientEventSource.Log.TraceEvent("<sc.{0}.{1}|INFO>: Found a custom {2} configuration but it is not of type {3}.",
                                                        TypeName, methodName, name, t.FullName);
                }
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO>: Unable to load custom `{2}`. Default value of `{3}` type returns.",
                                                   TypeName, methodName, name, nameof(T));
            return default;
        }
    }

    /// <summary>
    /// The configuration section definition for reading a configuration file.
    /// </summary>
    internal class SqlConfigurableRetryConnectionSection : ConfigurationSection, ISqlConfigurableRetryConnectionSection
    {
        public const string Name = "SqlConfigurableRetryLogicConnection";

        [ConfigurationProperty("retryLogicType")]
        // Fully qualified name
        public string RetryLogicType
        {
            get => this["retryLogicType"] as string;
            set => this["retryLogicType"] = value;
        }

        [ConfigurationProperty("retryMethod", IsRequired = true)]
        public string RetryMethod
        {
            get => this["retryMethod"] as string;
            set => this["retryMethod"] = value;
        }

        [ConfigurationProperty("numberOfTries", IsRequired = true, DefaultValue = 1)]
        [IntegerValidator(MinValue = 1, MaxValue = 60, ExcludeRange = false)]
        public int NumberOfTries
        {
            get => (int)this["numberOfTries"];
            set => this["numberOfTries"] = value;
        }

        [ConfigurationProperty("deltaTime", IsRequired = true)]
        [TimeSpanValidator(MinValueString = "00:00:00", MaxValueString = "00:02:00", ExcludeRange = false)]
        public TimeSpan DeltaTime
        {
            get => (TimeSpan)this["deltaTime"];
            set => this["deltaTime"] = value;
        }

        [ConfigurationProperty("minTime", IsRequired = false, DefaultValue = "00:00:00")]
        [TimeSpanValidator(MinValueString = "00:00:00", MaxValueString = "00:02:00", ExcludeRange = false)]
        public TimeSpan MinTimeInterval
        {
            get => (TimeSpan)this["minTime"];
            set => this["minTime"] = value;
        }

        [ConfigurationProperty("maxTime", IsRequired = false)]
        [TimeSpanValidator(MinValueString = "00:00:00", MaxValueString = "00:02:00", ExcludeRange = false)]
        public TimeSpan MaxTimeInterval
        {
            get => (TimeSpan)this["maxTime"];
            set => this["maxTime"] = value;
        }

        [ConfigurationProperty("transientErrors", IsRequired = false)]
        [RegexStringValidator(@"^([ \t]*(|-)\d+(?:[ \t]*,[ \t]*(|-)\d+)*[ \t]*)*$")]
        // Comma-separated error numbers
        public string TransientErrors
        {
            get => this["transientErrors"] as string;
            set => this["transientErrors"] = value;
        }
    }

    /// <summary>
    /// The configuration section definition for reading a configuration file.
    /// </summary>
    internal sealed class SqlConfigurableRetryCommandSection : SqlConfigurableRetryConnectionSection, ISqlConfigurableRetryCommandSection
    {
        public new const string Name = "SqlConfigurableRetryLogicCommand";

        [ConfigurationProperty("authorizedSqlCondition", IsRequired = false)]
        public string AuthorizedSqlCondition
        {
            get => this["authorizedSqlCondition"] as string;
            set => this["authorizedSqlCondition"] = value;
        }
    }
}
