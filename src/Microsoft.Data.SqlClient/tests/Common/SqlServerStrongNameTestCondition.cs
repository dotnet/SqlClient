// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.TestCommon;

/// <summary>
/// Provides a shared xUnit conditional check for tests that depend on the SqlServer assembly.
/// </summary>
public static class SqlServerStrongNameTestCondition
{
    /// <summary>
    /// Returns true when an unsigned SqlServer assembly should be usable in conjunction with other
    /// assemblies that explicitly depend on a strongly-named SqlServer assembly.
    /// </summary>
    /// <remarks>
    /// Why this exists: in local builds and PR pipeline runs, the <c>Microsoft.SqlServer.Server</c>
    /// assembly is likely to be produced unsigned.  Some UDT tests reference
    /// <c>Microsoft.SqlServer.Types</c> (a publicly published NuGet package, not owned by us),
    /// which requires a strongly named <c>Microsoft.SqlServer.Server</c> assembly.  The .NET
    /// runtime doesn't enforce this relationship, and the tests run without incident regardless of
    /// the signed-ness of the SqlServer assembly.  However, .NET Framework _does_ enforce that the
    /// SqlServer assembly is signed, and the tests fail to compile and/or run.
    ///
    /// This situation can occur in both Project and Package based test runs, the latter when the
    /// consumed SqlServer package was produced from an unsigned assembly.
    ///
    /// What this checks: on .NET Framework, it loads the UDT attribute type from
    /// <c>Microsoft.SqlServer.Server</c> and verifies that the assembly has a non-empty public key
    /// token (is strongly named). On .NET, this always returns <see langword="true"/> because
    /// runtime strong-name validation is not enforced the same way.
    ///
    /// When to use it: add this condition to tests that execute SQL Server UDT paths and are known
    /// to fail in unsigned .NET Framework runs, regardless of whether the assembly comes from
    /// project references or locally produced packages.
    /// </remarks>
    public static bool IsUnsignedSqlServerAssemblyUsable
    {
        get
        {
            #if NETFRAMEWORK

            Type udtAttributeType = Type.GetType(
                "Microsoft.SqlServer.Server.SqlUserDefinedTypeAttribute, Microsoft.SqlServer.Server",
                throwOnError: false);
            if (udtAttributeType is null)
            {
                return false;
            }

            byte[] token = udtAttributeType.Assembly.GetName().GetPublicKeyToken();
            return token is { Length: > 0 };

            #else

            return true;

            #endif
        }
    }
}
