// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Provides regression tests for the UDT assembly load hardening, driving
/// <see cref="SqlConnection.CheckGetExtendedUDTInfo"/> directly with the kind of
/// assembly-qualified name a hostile or compromised server could return.
/// </summary>
/// <remarks>
/// Before the fix, <see cref="SqlConnection"/> handed any server-supplied
/// assembly name straight to <see cref="Assembly.Load(AssemblyName)"/>, which
/// runs the target assembly's module initializer, and then invoked a static
/// member on the resolved type without checking that it was a user-defined type
/// at all, which runs the type's static constructor.  These tests assert that
/// neither happens.
/// </remarks>
[Collection(AppContextSwitchTestCollection.Name)]
public class UdtAssemblyLoadHardeningTest
{
    /// <summary>
    /// A connection string that is never opened.  Only the parsed connection
    /// options are needed, so that the type system assembly version the policy
    /// pins against is available.
    /// </summary>
    private const string ConnectionString = "Data Source=localhost;Integrated Security=true";

    /// <summary>
    /// The assembly-qualified name of a type in an assembly that is neither
    /// loaded into the test process nor referenced by anything that is.
    /// </summary>
    private const string HostileAssemblyQualifiedName =
        "Contoso.Evil.Payload, Contoso.Evil, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

    #region Assembly load policy

    /// <summary>
    /// Verifies that resolving a UDT whose assembly is not permitted never
    /// reaches the assembly loader, and reports a policy failure rather than
    /// silently succeeding.
    /// </summary>
    [Fact]
    public void CheckGetExtendedUDTInfo_UnknownAssembly_IsNeverLoaded()
    {
        using PolicyScope scope = new();
        using AssemblyLoadRecorder recorder = new();

        SqlConnection connection = new(ConnectionString);
        SqlMetaDataPriv metaData = CreateUdtMetaData(HostileAssemblyQualifiedName);

        Exception exception = Record.Exception(
            () => connection.CheckGetExtendedUDTInfo(metaData, fThrow: true));

        Assert.NotNull(exception);
        Assert.Null(metaData.udt.Type);
        Assert.DoesNotContain("Contoso.Evil", recorder.LoadedNames);
    }

    /// <summary>
    /// Verifies that the non-throwing call sites (for example
    /// SqlDataReader.GetFieldType) still tolerate a denied assembly, leaving the
    /// resolved type null instead of faulting the read.
    /// </summary>
    [Fact]
    public void CheckGetExtendedUDTInfo_UnknownAssembly_DoesNotThrowWhenNotRequested()
    {
        using PolicyScope scope = new();
        using AssemblyLoadRecorder recorder = new();

        SqlConnection connection = new(ConnectionString);
        SqlMetaDataPriv metaData = CreateUdtMetaData(HostileAssemblyQualifiedName);

        connection.CheckGetExtendedUDTInfo(metaData, fThrow: false);

        Assert.Null(metaData.udt.Type);
        Assert.DoesNotContain("Contoso.Evil", recorder.LoadedNames);
    }

    /// <summary>
    /// Verifies that the legacy switch restores the pre-fix behavior, so an
    /// application that depends on it has a documented escape hatch.  The load
    /// is still expected to fail, because the assembly does not exist, but it
    /// must fail in the loader rather than in the policy.
    /// </summary>
    [Fact]
    public void CheckGetExtendedUDTInfo_LegacyMode_ReachesTheLoader()
    {
        using PolicyScope scope = new(legacy: true);

        SqlConnection connection = new(ConnectionString);
        SqlMetaDataPriv metaData = CreateUdtMetaData(HostileAssemblyQualifiedName);

        Exception exception = Record.Exception(
            () => connection.CheckGetExtendedUDTInfo(metaData, fThrow: true));

        // The loader, not the policy, is what refuses the load in legacy mode.
        Assert.IsAssignableFrom<System.IO.FileNotFoundException>(exception);
    }

    #endregion

    #region User-defined type validation

    /// <summary>
    /// Verifies that a type which resolves successfully but is not annotated
    /// with SqlUserDefinedTypeAttribute is rejected before any of its code can
    /// run.
    /// </summary>
    /// <remarks>
    /// This is the second half of the vulnerability: GetUdtValue's null branch
    /// calls InvokeMember("Null", ... Static ...) on the resolved type, which
    /// runs its static constructor.  Rejecting the type in
    /// CheckGetExtendedUDTInfo is the last point at which the driver can decline
    /// without executing anything, because reading custom attributes does not
    /// trigger a static constructor.
    /// </remarks>
    [Fact]
    public void CheckGetExtendedUDTInfo_TypeWithoutUdtAttribute_IsRejected()
    {
        using PolicyScope scope = new();

        // This test assembly is loaded, so the assembly load policy permits it
        // in the default Restricted mode; only the attribute check stands
        // between the server-supplied name and the type's code.
        SqlConnection connection = new(ConnectionString);
        SqlMetaDataPriv metaData = CreateUdtMetaData(
            typeof(NotAUserDefinedType).AssemblyQualifiedName!);

        Exception exception = Record.Exception(
            () => connection.CheckGetExtendedUDTInfo(metaData, fThrow: true));

        Assert.NotNull(exception);
        Assert.Null(metaData.udt.Type);
        Assert.False(
            StaticConstructorMarker.Ran,
            "The type's static constructor must not have been triggered.");
    }

    /// <summary>
    /// Verifies that a legitimate user-defined type in a permitted assembly is
    /// still resolved, so the hardening does not break the supported scenario.
    /// </summary>
    [Fact]
    public void CheckGetExtendedUDTInfo_UserDefinedType_IsResolved()
    {
        using PolicyScope scope = new();

        SqlConnection connection = new(ConnectionString);
        SqlMetaDataPriv metaData = CreateUdtMetaData(
            typeof(AUserDefinedType).AssemblyQualifiedName!);

        connection.CheckGetExtendedUDTInfo(metaData, fThrow: true);

        Assert.Equal(typeof(AUserDefinedType), metaData.udt.Type);
    }

    /// <summary>
    /// Verifies that legacy mode also bypasses the user-defined type check, so
    /// the switch fully restores the previous behavior.
    /// </summary>
    [Fact]
    public void CheckGetExtendedUDTInfo_LegacyMode_SkipsUdtAttributeCheck()
    {
        using PolicyScope scope = new(legacy: true);

        SqlConnection connection = new(ConnectionString);
        SqlMetaDataPriv metaData = CreateUdtMetaData(
            typeof(NotAUserDefinedType).AssemblyQualifiedName!);

        connection.CheckGetExtendedUDTInfo(metaData, fThrow: true);

        Assert.Equal(typeof(NotAUserDefinedType), metaData.udt.Type);
    }

    /// <summary>
    /// Verifies that a type name carrying no assembly part is still rejected.
    /// </summary>
    /// <remarks>
    /// Type.GetType resolves a bare type name against the core library without
    /// ever consulting the assembly resolver, so the assembly load policy is
    /// structurally bypassed for such a name.  The SqlUserDefinedTypeAttribute
    /// check is the only gate that stands in its way, and this test locks that
    /// in: a server that sends "System.String" must not end up with the driver
    /// invoking members on System.String.
    /// </remarks>
    [Theory]
    [InlineData("System.String")]
    [InlineData("System.Diagnostics.Process")]
    public void CheckGetExtendedUDTInfo_TypeNameWithoutAssembly_IsRejected(string typeName)
    {
        using PolicyScope scope = new();

        SqlConnection connection = new(ConnectionString);
        SqlMetaDataPriv metaData = CreateUdtMetaData(typeName);

        Exception exception = Record.Exception(
            () => connection.CheckGetExtendedUDTInfo(metaData, fThrow: true));

        Assert.NotNull(exception);
        Assert.Null(metaData.udt.Type);
    }

    /// <summary>
    /// Verifies that a bare type name is rejected without throwing at the call
    /// sites that ask not to throw, which is how GetFieldType probes UDT
    /// metadata.
    /// </summary>
    [Fact]
    public void CheckGetExtendedUDTInfo_TypeNameWithoutAssembly_DoesNotThrowWhenNotRequested()
    {
        using PolicyScope scope = new();

        SqlConnection connection = new(ConnectionString);
        SqlMetaDataPriv metaData = CreateUdtMetaData("System.String");

        connection.CheckGetExtendedUDTInfo(metaData, fThrow: false);

        Assert.Null(metaData.udt.Type);
    }

    #endregion

    #region Helpers

    private static SqlMetaDataPriv CreateUdtMetaData(string assemblyQualifiedName) =>
        new()
        {
            udt = new SqlMetaDataUdt
            {
                DatabaseName = "db",
                SchemaName = "dbo",
                TypeName = "udt",
                AssemblyQualifiedName = assemblyQualifiedName,
            },
        };

    /// <summary>
    /// Forces the policy switches to known values and clears the allow list and
    /// every policy cache for the duration of a test.
    /// </summary>
    private sealed class PolicyScope : IDisposable
    {
        private readonly LocalAppContextSwitchesHelper _switches;
        private readonly object? _originalAllowList;

        public PolicyScope(bool legacy = false)
        {
            _switches = new LocalAppContextSwitchesHelper();
            _originalAllowList =
                AppContext.GetData(UdtAssemblyPolicy.AllowListAppContextDataName);

            _switches.UseLegacyUdtAssemblyLoad = legacy;

            AppDomain.CurrentDomain.SetData(
                UdtAssemblyPolicy.AllowListAppContextDataName,
                null);
            UdtAssemblyPolicy.ResetCache();
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.SetData(
                UdtAssemblyPolicy.AllowListAppContextDataName,
                _originalAllowList);
            UdtAssemblyPolicy.ResetCache();
            _switches.Dispose();
        }
    }

    /// <summary>
    /// Records the simple name of every assembly loaded into the process while
    /// it is alive, so a test can assert that a load never happened.
    /// </summary>
    private sealed class AssemblyLoadRecorder : IDisposable
    {
        private readonly List<string> _loadedNames = new();

        public AssemblyLoadRecorder()
        {
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        public IReadOnlyList<string> LoadedNames
        {
            get
            {
                lock (_loadedNames)
                {
                    return _loadedNames.ToArray();
                }
            }
        }

        public void Dispose() =>
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;

        private void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
        {
            string? name = args.LoadedAssembly.GetName().Name;
            if (name is not null)
            {
                lock (_loadedNames)
                {
                    _loadedNames.Add(name);
                }
            }
        }
    }

    /// <summary>
    /// A type that a hostile server could name but that is not a user-defined
    /// type.  Its static constructor records that it ran so a test can prove it
    /// did not.
    /// </summary>
    private sealed class NotAUserDefinedType
    {
        static NotAUserDefinedType()
        {
            StaticConstructorMarker.Ran = true;
        }
    }

    /// <summary>
    /// Holds the flag that <see cref="NotAUserDefinedType"/>'s static
    /// constructor sets.  It lives in a separate class so that reading it does
    /// not itself trigger the constructor under test.
    /// </summary>
    private static class StaticConstructorMarker
    {
        internal static bool Ran;
    }

    /// <summary>
    /// A well-formed user-defined type, used to prove the hardening does not
    /// reject legitimate types.
    /// </summary>
    [SqlUserDefinedType(Format.UserDefined, MaxByteSize = 8)]
    private sealed class AUserDefinedType
    {
    }

    #endregion
}
