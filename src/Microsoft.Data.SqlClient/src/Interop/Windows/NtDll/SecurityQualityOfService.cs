// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Interop.Windows.NtDll
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
}
