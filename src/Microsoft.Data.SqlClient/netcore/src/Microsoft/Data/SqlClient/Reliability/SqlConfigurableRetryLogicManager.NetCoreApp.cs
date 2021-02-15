// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Configurable retry logic manager
    /// </summary>
    internal partial class SqlConfigurableRetryLogicManager : SqlConfigurableRetryLogicManagerBase
    {
        #region Type resolution
        /// <summary>
        /// Performs a case-sensitive search to resolve the specified type name 
        /// and its related assemblies in default assembly load context if they aren't loaded yet.
        /// </summary>
        /// <returns>Resolved type if could resolve the type; otherwise, the `SqlConfigurableRetryFactory` type.</returns>
        private static Type LoadType(string fullyQualifiedName)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Entry point.", s_typeName, methodName);

            var result = Type.GetType(fullyQualifiedName, AssemblyResolver, TypeResolver);
            if (result != null)
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> The '{2}' type is resolved.",
                                                        s_typeName, methodName, result.FullName);
            }
            else
            {
                result = typeof(SqlConfigurableRetryFactory);
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Couldn't resolve the requested type by '{2}'; The internal `{3}` type is returned.",
                                                        s_typeName, methodName, fullyQualifiedName, result.FullName);
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Exit point.", s_typeName, methodName);
            return result;
        }

        /// <summary>
        /// If the caller does not have sufficient permissions to read the specified file, 
        /// no exception is thrown and the method returns null regardless of the existence of path.
        /// </summary>
        private static string MakeFullPath(string directory, string assemblyName, string extension = ".dll")
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Looking for '{2}' assembly in '{3}' directory."
                                                    , s_typeName, methodName, assemblyName ?? NullConst, directory ?? NullConst);
            string fullPath = Path.Combine(directory, assemblyName);
            fullPath = string.IsNullOrEmpty(Path.GetExtension(fullPath)) ? fullPath + extension : fullPath;
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Looking for '{2}' assembly by '{3}' full path."
                                                    , s_typeName, methodName, assemblyName ?? NullConst, fullPath ?? NullConst);
            return File.Exists(fullPath) ? fullPath : null;
        }

        private static Assembly AssemblyResolver(AssemblyName arg)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;

            string fullPath = MakeFullPath(Environment.CurrentDirectory, arg.Name);
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Looking for '{2}' assembly by '{3}' full path."
                                                    , s_typeName, methodName, arg, fullPath ?? NullConst);

            return fullPath == null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }

        private static Type TypeResolver(Assembly arg1, string arg2, bool arg3) => arg1?.ExportedTypes.Single(t => t.FullName == arg2);

        /// <summary>
        /// Load assemblies on a request.
        /// </summary>
        private static Assembly Default_Resolving(AssemblyLoadContext arg1, AssemblyName arg2)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;

            var target = MakeFullPath(Environment.CurrentDirectory, arg2.Name);
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Looking for '{2}' assembly that is requested by '{3}' ALC from '{4}' path."
                                                    , s_typeName, methodName, arg2, arg1, target ?? NullConst);

            return target == null ? null : arg1.LoadFromAssemblyPath(target);
        }
        #endregion

        #region AppContext switch
        protected override void ApplyContextSwitches()
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Entry point.", s_typeName, methodName);
            try
            {
                // Fetch the section attributes values from the configuration section from the app config file.
                var _configSection = FetchConfigurationSection<AppContextSwitchOverridesSection>(AppContextSwitchOverridesSection.Name);
                if (_configSection != null)
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Successfully loaded the AppContext switches from the configuration file's section '{2}'.",
                                                           s_typeName, methodName, AppContextSwitchOverridesSection.Name);

                    ApplySwitchValues(_configSection.Value?.Split('=', ';'));
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Successfully assigned the AppContext switches.",
                                                           s_typeName, methodName);
                }
            }
            catch (Exception e)
            {
                // Don't throw an error for invalid config files
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO>: {2}",
                                                       s_typeName, methodName, e);
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Exit point.", s_typeName, methodName);
        }

        private static void ApplySwitchValues(string[] switches)
        {
            var methodName = MethodBase.GetCurrentMethod().Name;
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Entry point.", s_typeName, methodName);

            if (switches == null || switches.Length == 0 || switches.Length % 2 == 1)
            { return; }

            for (int i = 0; i < switches.Length / 2; i++)
            {
                try
                {
                    AppContext.SetSwitch(switches[i], Convert.ToBoolean(switches[i + 1]));
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Successfully assigned the AppContext switch '{2}'={3}.",
                                                           s_typeName, methodName, switches[i], switches[i + 1]);
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"Exception occurs while trying to set the AppContext switch '{switches[i]}'={switches[i + 1]}", e);
                }
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Exit point.", s_typeName, methodName);
        }
    }

    /// <summary>
    /// The configuration section definition for reading a configuration file.
    /// </summary>
    internal sealed class AppContextSwitchOverridesSection : ConfigurationSection
    {
        public const string Name = "AppContextSwitchOverrides";

        [ConfigurationProperty("value", IsRequired = true)]
        public string Value => this["value"] as string;
    }
    #endregion
}
