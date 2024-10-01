// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

internal partial class Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct SecurityQualityOfService
    {
        public SecurityQualityOfService(
            ImpersonationLevel impersonationLevel,
            bool isDynamicTracking,
            bool isEffectiveOnly)
        {
            Length = (uint)sizeof(SecurityQualityOfService);
            ImpersonationLevel = impersonationLevel;
            IsDynamicTracking = isDynamicTracking;
            IsEffectiveOnly = isEffectiveOnly;
        }

        /// <summary>
        /// Specifies the size, in bytes, of this structure.
        /// </summary>
        public uint Length { get; }

        /// <summary>
        /// Specifies the information given to the server about the client, and how the server
        /// may represent, or impersonate, the client. Security impersonation levels govern the
        /// degree to which a server process can act on behalf of a client process.
        /// </summary>
        public ImpersonationLevel ImpersonationLevel { get; set; }

        /// <summary>
        /// Specifies whether the server is to be given a snapshot of the client's security
        /// context (called static tracking), or is to be continually updated to track changes
        /// to the client's security context (called dynamic tracking). Not all communication
        /// mechanisms support dynamic tracking; those that do not will default to static
        /// tracking.
        /// </summary>
        public bool IsDynamicTracking { get; set; }

        /// <summary>
        /// Specifies whether the server may enable or disable privileges and groups that the
        /// client's security context may include.
        /// </summary>
        public bool IsEffectiveOnly { get; set; }
    }

    /// <summary>
    /// Values that specify security impersonation levels. Security impersonation levels govern
    /// the degree to which a server process can act on behalf of a client process.
    /// </summary>
    internal enum ImpersonationLevel
    {
        /// <summary>
        /// The server process cannot obtain identification information about the client, and
        /// it cannot impersonate the client. It is defined with no value given, and this, by
        /// ANSI C rules, defaults to a value of zero.
        /// </summary>
        SecurityAnonymous = 0,

        /// <summary>
        /// The server process can obtain information about the client, such as security
        /// identifiers and privileges, but it cannot impersonate the client. This is useful
        /// for servers that export their own objects, for example, database products that
        /// export tables and views. Using the retrieved client-security information, the
        /// server can make access-validation decision without being able to use other
        /// services that are using the client's security context.
        /// </summary>
        SecurityIdentification = 1,

        /// <summary>
        /// The server process can impersonate the client's security context on its local
        /// system. The server cannot impersonate the client on remote systems.
        /// </summary>
        SecurityImpersonation = 2,

        /// <summary>
        /// The server process can impersonate the client's security context on remote systems.
        /// </summary>
        SecurityDelegation = 3,
    }
}
