// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.TestCommon;

/// <summary>
/// Provides a shared xUnit conditional check for tests that depend on SQL Server UDT types.
/// </summary>
/// <remarks>
/// Why this exists: in PR and local project-reference builds, the <c>Microsoft.SqlServer.Server</c>
/// assembly can be produced unsigned on .NET Framework when no signing key is available.  Some UDT
/// tests use <c>Microsoft.SqlServer.Types</c> (a publicly published NuGet package, not owned by
/// us), which requires a strongly named <c>Microsoft.SqlServer.Server</c> assembly and fail with
/// <c>FileLoadException</c> ( <c>0x80131044</c>) when that requirement is not met.
///
/// This can also happen in package-based test runs when the consumed package was produced from an
/// unsigned assembly (for example, package-mode restore from a local feed containing CI/dev
/// artifacts), not only when using direct project references.
///
/// What this checks: on .NET Framework, it loads the UDT attribute type from
/// <c>Microsoft.SqlServer.Server</c> and verifies that the assembly has a non-empty public key
/// token (is strongly named). On .NET, this always returns <see langword="true"/> because runtime
/// strong-name validation is not enforced the same way.
///
/// When to use it: add this condition to tests that execute SQL Server UDT paths and are known to
/// fail in unsigned net462 runs, regardless of whether the assembly comes from project references
/// or locally produced packages.
/// </remarks>
public static class SqlServerStrongNameTestCondition
{
    /// <summary>
    /// Gets whether SQL Server UDT tests are safe to run in the current runtime/signing context.
    /// </summary>
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
