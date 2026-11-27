// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient.Diagnostics
{
    internal static class OpenTelemetryExtensionBinding
    {
        private const string AssemblyName = "Microsoft.Data.SqlClient.Extensions.OpenTelemetry";
        private const string TypeName =
            "Microsoft.Data.SqlClient.Extensions.OpenTelemetry.SqlClientOpenTelemetry";

        private static readonly Lazy<Type> s_type = new(LoadType);
        private static readonly Lazy<Func<string, IDisposable>> s_begin =
            new(() => Bind<Func<string, IDisposable>>("Begin"));
        private static readonly Lazy<Action<IDisposable, string, string, string, double, int, string>>
            s_record = new(() =>
                Bind<Action<IDisposable, string, string, string, double, int, string>>("Record"));

        internal static IDisposable Begin(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                return null;
            }

            try
            {
                return s_begin.Value?.Invoke(endpoint);
            }
            catch (Exception ex) when (IsUnavailable(ex))
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "OpenTelemetry extension unavailable: {0}", ex.GetType().Name);
                return null;
            }
        }

        internal static IDisposable Retain(IDisposable route)
        {
            try
            {
                return (route as ICloneable)?.Clone() as IDisposable;
            }
            catch (ObjectDisposedException ex)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "OpenTelemetry extension unavailable: {0}", ex.GetType().Name);
                return null;
            }
        }

        // Feeds the route's private metrics at scope end, independent of span sampling.
        internal static void Record(IDisposable route, OperationScope scope, string outcome,
            double seconds)
        {
            try
            {
                int retries = scope.IsRoot && scope.Attempts > 1 ? scope.Attempts - 1 : 0;
                s_record.Value?.Invoke(route, scope.Name, outcome, scope.Category, seconds,
                    retries, scope.RetryReason);
            }
            catch (Exception ex) when (IsUnavailable(ex) || ex is ObjectDisposedException)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "OpenTelemetry extension unavailable: {0}", ex.GetType().Name);
            }
        }

#if NET
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075",
            Justification = "LoadType preserves the extension's public methods.")]
#endif
        private static T Bind<T>(string methodName) where T : Delegate
        {
            try
            {
                Type type = s_type.Value;
                if (type == null)
                {
                    return null;
                }
                MethodInfo method = type.GetMethod(methodName,
                    BindingFlags.Static | BindingFlags.Public)
                    ?? throw new MissingMethodException(TypeName, methodName);
                return (T)Delegate.CreateDelegate(typeof(T), method);
            }
            catch (Exception ex) when (IsUnavailable(ex))
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "OpenTelemetry extension unavailable: {0}", ex.GetType().Name);
                return null;
            }
        }

#if NET
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, TypeName, AssemblyName)]
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "The optional extension's entry point is preserved above.")]
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2035",
            Justification = "The extension is optional.")]
#endif
        private static Type LoadType()
        {
            try
            {
#if STRONG_NAME_SIGNING
                AssemblyName name = new(AssemblyName);
                byte[] expectedToken = [0x23, 0xec, 0x7f, 0xc2, 0xd6, 0xea, 0xa4, 0xa5];
                name.SetPublicKeyToken(expectedToken);
                Assembly assembly = Assembly.Load(name);
                byte[] actualToken = assembly.GetName().GetPublicKeyToken();
                if (actualToken == null || !actualToken.SequenceEqual(expectedToken))
                {
                    throw new FileLoadException("Unexpected extension assembly identity.");
                }
#else
                Assembly assembly = Assembly.Load(AssemblyName);
#endif
                return assembly.GetType(TypeName, throwOnError: true);
            }
            catch (Exception ex) when (IsUnavailable(ex))
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "OpenTelemetry extension unavailable: {0}", ex.GetType().Name);
                return null;
            }
        }

        private static bool IsUnavailable(Exception ex) =>
            ex is FileNotFoundException or FileLoadException or BadImageFormatException or
                TypeLoadException or TypeInitializationException or MissingMethodException or
                MemberAccessException or AmbiguousMatchException or ArgumentException;
    }
}
