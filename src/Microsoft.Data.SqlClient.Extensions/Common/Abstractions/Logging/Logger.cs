// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Extensions.Abstractions.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;

#nullable enable

namespace Microsoft.Data.SqlClient.Extensions.Abstractions;

internal static class Logger
{
    private const string AssemblyName = "Microsoft.Data.SqlClient.Extensions.Logging";
    private const string LoggerProviderTypeName = "Microsoft.Data.SqlClient.Extensions.Logging.SqlClientEventSourceLoggerProvider";

    private static ILoggerProvider? LoggerProvider
    {
        #if NET
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "All members of the logger provider are preserved by DynamicDependency.")]
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075", Justification = "All members of the logger provider are preserved by DynamicDependency.")]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, LoggerProviderTypeName, AssemblyName)]
        #endif
        get
        {
            if (field is not null)
            {
                return field;
            }

            try
            {
                // Try to load the MDS assembly.
                // TODO(https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/39845):
                // Verify the assembly is signed by us?
                Assembly assembly = Assembly.Load(AssemblyName);

                if (assembly is null)
                {
                    Debug.Fail($"MDS assembly={AssemblyName} not found; " +
                        $"logging will not function.");
                    return null;
                }

                Type? loggerProviderType = assembly.GetType(LoggerProviderTypeName);

                if (loggerProviderType is null)
                {
                    Debug.Fail($"MDS logger provider class={LoggerProviderTypeName} not found; " +
                        "logging will not function");
                    return null;
                }

                bool isILoggerProvider = typeof(ILoggerProvider).IsAssignableFrom(loggerProviderType);
                if (!isILoggerProvider)
                {
                    Debug.Fail($"MDS logger provider class={LoggerProviderTypeName} does not implement ILoggerProvider; " +
                        "logging will not function");
                    return null;
                }

                ConstructorInfo? ctor = loggerProviderType.GetConstructor(Type.EmptyTypes);

                if (ctor is null)
                {
                    Debug.Fail($"MDS logger provider class={LoggerProviderTypeName} does not have a parameterless constructor; " +
                        "logging will not function");
                    return null;
                }

                if (ctor.Invoke(null) is not ILoggerProvider provider)
                {
                    Debug.Fail($"MDS logger provider class={LoggerProviderTypeName} could not be instantiated; " +
                        "logging will not function");
                    return null;
                }

                field = provider;
                return field;
            }
            catch
            {
                // If any exceptions occur, we can only swallow them silently - without
                // the logger provider, we have no way to report them.
                return null;
            }
        }
    }

    public static ILogger? TraceLogger =>
        field ??= LoggerProvider?.CreateLogger(CategoryNames.Trace);
}
