// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Configurable retry logic manager
    /// </summary>
    internal sealed partial class SqlConfigurableRetryLogicLoader
    {
        #region Type resolution
        /// <summary>
        /// Performs a case-sensitive search to resolve the specified type name 
        /// and its related assemblies in default assembly load context if they aren't loaded yet.
        /// </summary>
        /// <returns>Resolved type if it could resolve the type; otherwise, the `SqlConfigurableRetryFactory` type.</returns>
        private static Type LoadType(string fullyQualifiedName)
        {
            string methodName = nameof(LoadType);
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Entry point.", TypeName, methodName);

            var result = Type.GetType(fullyQualifiedName, AssemblyResolver, TypeResolver);
            if (result != null)
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> The '{2}' type is resolved.",
                                                        TypeName, methodName, result.FullName);
            }
            else
            {
                result = typeof(SqlConfigurableRetryFactory);
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Couldn't resolve the requested type by '{2}'; The internal `{3}` type is returned.",
                                                        TypeName, methodName, fullyQualifiedName, result.FullName);
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Exit point.", TypeName, methodName);
            return result;
        }

        /// <summary>
        /// If the caller does not have sufficient permissions to read the specified file, 
        /// no exception is thrown and the method returns null regardless of the existence of path.
        /// </summary>
        private static string MakeFullPath(string directory, string assemblyName, string extension = ".dll")
        {
            string methodName = nameof(MakeFullPath);
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Looking for '{2}' assembly in '{3}' directory."
                                                    , TypeName, methodName, assemblyName, directory);
            string fullPath = Path.Combine(directory, assemblyName);
            fullPath = string.IsNullOrEmpty(Path.GetExtension(fullPath)) ? fullPath + extension : fullPath;
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Looking for '{2}' assembly by '{3}' full path."
                                                    , TypeName, methodName, assemblyName, fullPath);
            return File.Exists(fullPath) ? fullPath : null;
        }

        private static Assembly AssemblyResolver(AssemblyName arg)
        {
            string methodName = nameof(AssemblyResolver);

            string fullPath = MakeFullPath(Environment.CurrentDirectory, arg.Name);
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Looking for '{2}' assembly by '{3}' full path."
                                                    , TypeName, methodName, arg, fullPath);

            return fullPath == null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }

        private static Type TypeResolver(Assembly arg1, string arg2, bool arg3)
        {
            IEnumerable<Type> types = arg1?.ExportedTypes;
            Type result = null;
            if (types != null)
            {
                foreach (Type type in types)
                {
                    if (type.FullName == arg2)
                    {
                        if (result != null)
                        {
                            throw new InvalidOperationException("Sequence contains more than one matching element");
                        }
                        result = type;
                    }
                }
            }
            if (result == null)
            {
                throw new InvalidOperationException("Sequence contains no matching element");
            }
            return result;
        }

        /// <summary>
        /// Load assemblies on request.
        /// </summary>
        private static Assembly Default_Resolving(AssemblyLoadContext arg1, AssemblyName arg2)
        {
            string methodName = nameof(Default_Resolving);

            string target = MakeFullPath(Environment.CurrentDirectory, arg2.Name);
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Looking for '{2}' assembly that is requested by '{3}' ALC from '{4}' path."
                                                    , TypeName, methodName, arg2, arg1, target);

            return target == null ? null : arg1.LoadFromAssemblyPath(target);
        }
        #endregion
    }
}
