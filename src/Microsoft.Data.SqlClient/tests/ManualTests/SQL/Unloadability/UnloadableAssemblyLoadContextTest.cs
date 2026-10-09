// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Data.SqlClient.UnloadableLibrary;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.Unloadability;

/// <summary>
/// Tests verifying SqlClient's behaviour within unloadable assembly load contexts.
/// </summary>
[Trait("Set", "2")]
[Collection("AssemblyLoadContext")]
public class UnloadableAssemblyLoadContextTest
{
    /// <summary>
    /// Verifies that loading SqlClient into a secondary AssemblyLoadContext does not block that
    /// AssemblyLoadContext from being unloaded after use.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup))]
    public async Task SecondaryAssemblyLoadContext_Unloads()
    {
        string connStr = DataTestUtility.TCPConnectionString;
        string typeName = typeof(EntryPoint).FullName!;
        string libraryPath = typeof(EntryPoint).Assembly.Location;

        WeakReference alcWeakReference = await LoadAndUnloadAssemblyLoadContext(typeName, libraryPath, connStr);

        for (int i = 0; i < 10 && alcWeakReference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(alcWeakReference.IsAlive);
    }

    /// <summary>
    /// Creates an unloadable <see cref="AssemblyLoadContext"/>, then acts within it to open a SqlConnection
    /// and to execute a command.
    /// </summary>
    /// <param name="typeName">The full name of the entry point type to load into the AssemblyLoadContext.</param>
    /// <param name="assemblyPath">The path to the assembly containing the entry point type.</param>
    /// <param name="connectionString">The connection to open.</param>
    /// <returns>A <see cref="WeakReference"/> to the AssemblyLoadContext.</returns>
    /// <remarks>
    /// This method makes use of reflection to avoid accidentally creating a hard reference to the
    /// secondary ALC's type definitions. It is also marked as <see cref="MethodImplOptions.NoInlining"/>
    /// in order to ensure that this weak reference is guaranteed to be out of scope in the method's caller.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> LoadAndUnloadAssemblyLoadContext(string typeName, string assemblyPath, string connectionString)
    {
        // This method loads the entry point type into a new, collectible AssemblyLoadContext and
        // verifies that the entry point type is referencing the SqlConnection type in the correct
        // AssemblyLoadContext.
        // It then manually instantiates this entry point type, and invokes GetDate and GetDateAsync.
        UnloadableAssemblyLoadContext alc = new(nameof(SecondaryAssemblyLoadContext_Unloads), assemblyPath);
        WeakReference weakRef = new(alc, trackResurrection: true);
        Assembly loadedAsm = alc.LoadFromAssemblyPath(assemblyPath);

        Type? unloadableLibraryType = loadedAsm.GetType(typeName);
        Assert.NotNull(unloadableLibraryType);

        PropertyInfo? alcNameProperty = unloadableLibraryType.GetProperty(nameof(EntryPoint.AssemblyLoadContextName), BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(alcNameProperty);
        string? alcName = alcNameProperty.GetValue(null) as string;

        Assert.Equal(alc.Name, alcName);

        object? instantiated = Activator.CreateInstance(unloadableLibraryType, [connectionString]);
        Assert.NotNull(instantiated);

        MethodInfo? getDateMethod = unloadableLibraryType.GetMethod(nameof(EntryPoint.GetDate));
        Assert.NotNull(getDateMethod);

        getDateMethod.Invoke(instantiated, null);

        MethodInfo? getDateAsyncMethod = unloadableLibraryType.GetMethod(nameof(EntryPoint.GetDateAsync));
        Assert.NotNull(getDateAsyncMethod);

        Task? getDateTask = getDateAsyncMethod.Invoke(instantiated, null) as Task;
        Assert.NotNull(getDateTask);

        await getDateTask;

        MethodInfo? disposeMethod = unloadableLibraryType.GetMethod(nameof(EntryPoint.Dispose));
        Assert.NotNull(disposeMethod);

        disposeMethod.Invoke(instantiated, null);

        alc.Unload();

        return weakRef;
    }

    private sealed class UnloadableAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public UnloadableAssemblyLoadContext(string name, string assemblyPath)
            : base(name, isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(assemblyPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? resolvedAssemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

            return resolvedAssemblyPath is not null
                ? LoadFromAssemblyPath(resolvedAssemblyPath)
                : null;
        }
    }
}

/// <summary>
/// Defines a test collection that serializes execution of test classes
/// which load and unload <see cref="AssemblyLoadContext"/>s.
/// </summary>
[CollectionDefinition("AssemblyLoadContext")]
public class UnloadableAssemblyLoadContextCollection
{
}


#endif
