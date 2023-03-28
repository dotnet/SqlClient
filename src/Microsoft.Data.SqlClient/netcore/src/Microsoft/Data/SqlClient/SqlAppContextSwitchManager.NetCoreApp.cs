// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Configuration;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// AppContext switch manager
    /// </summary>
    internal sealed class SqlAppContextSwitchManager
    {
        private const string TypeName = nameof(SqlAppContextSwitchManager);
        /// <summary>
        /// To support the AppContext's set switch through the config file for .NET Core; 
        /// .Net Framework supports it internally through the configuration file by 'AppContextSwitchOverrides' element under 'runtime' section
        /// </summary>
        internal static void ApplyContextSwitches(IAppContextSwitchOverridesSection appContextSwitches)
        {
            string methodName = nameof(ApplyContextSwitches);
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Entry point.", TypeName, methodName);
            if (appContextSwitches != null)
            {
                ApplySwitchValues(appContextSwitches.Value?.Split('=', ';'));
            }

            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Exit point.", TypeName, methodName);
        }

        private static bool ApplySwitchValues(string[] switches)
        {
            string methodName = nameof(ApplySwitchValues);
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Entry point.", TypeName, methodName);

            if (switches == null || switches.Length == 0 || switches.Length % 2 == 1)
            { return false; }

            for (int i = 0; i < switches.Length / 2; i++)
            {
                try
                {
                    AppContext.SetSwitch(switches[i], Convert.ToBoolean(switches[i + 1]));
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Successfully assigned the AppContext switch '{2}'={3}.",
                                                           TypeName, methodName, switches[i], switches[i + 1]);
                }
                catch (Exception e)
                {
                    throw new ArgumentException(StringsHelper.GetString(Strings.SqlAppContextSwitchManager_InvalidValue, switches[i], switches[i + 1]), e);
                }
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Exit point.", TypeName, methodName);
            return true;
        }
    }

    /// <summary>
    /// The configuration section definition for reading a configuration file.
    /// </summary>
    internal sealed class AppContextSwitchOverridesSection : ConfigurationSection, IAppContextSwitchOverridesSection
    {
        public const string Name = "AppContextSwitchOverrides";

        [ConfigurationProperty("value", IsRequired = true)]
        public string Value
        {
            get => this["value"] as string;
            set => this["value"] = value;
        }
    }
}
