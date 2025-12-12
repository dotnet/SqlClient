// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Interop.Windows.NtDll
{
    /// <summary>
    /// <a href="https://msdn.microsoft.com/en-us/library/windows/hardware/ff557749.aspx">OBJECT_ATTRIBUTES</a> structure.
    /// The OBJECT_ATTRIBUTES structure specifies attributes that can be applied to objects or object handles by routines
    /// that create objects and/or return handles to objects.
    /// </summary>
    internal unsafe struct ObjectAttributes
    {
        public uint Length;

        /// <summary>
        /// Optional handle to root object directory for the given ObjectName.
        /// Can be a file system directory or object manager directory.
        /// </summary>
        public IntPtr RootDirectory;

        /// <summary>
        /// Name of the object. Must be fully qualified if RootDirectory isn't set.
        /// Otherwise, is relative to RootDirectory.
        /// </summary>
        public UnicodeString* ObjectName;

        public ObjectAttributeFlags Attributes;

        /// <summary>
        /// If null, object will receive default security settings.
        /// </summary>
        public void* SecurityDescriptor;

        /// <summary>
        /// Optional quality of service to be applied to the object. Used to indicate
        /// security impersonation level and context tracking mode (dynamic or static).
        /// </summary>
        public SecurityQualityOfService* SecurityQoS;

        /// <summary>
        /// Equivalent of InitializeObjectAttributes macro with the exception that you can directly set SQOS.
        /// </summary>
        public ObjectAttributes(
            UnicodeString* objectName,
            ObjectAttributeFlags attributes,
            IntPtr rootDirectory,
            SecurityQualityOfService* securityQos)
        {
            Length = (uint)sizeof(ObjectAttributes);
            RootDirectory = rootDirectory;
            ObjectName = objectName;
            Attributes = attributes;
            SecurityDescriptor = null;
            SecurityQoS = securityQos;
        }
    }
}
