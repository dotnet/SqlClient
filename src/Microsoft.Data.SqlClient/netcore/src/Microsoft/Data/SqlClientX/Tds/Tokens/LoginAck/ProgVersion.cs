// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Tds.Tokens.LoginAck
{
    /// <summary>
    /// Program version.
    /// </summary>
    internal sealed class ProgVersion
    {
        /// <summary>
        /// Major.
        /// </summary>
        public byte Major { get; }

        /// <summary>
        /// Minor.
        /// </summary>
        public byte Minor { get; }

        /// <summary>
        /// Build revision.
        /// </summary>
        public ushort Build { get; }

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="major">Major.</param>
        /// <param name="minor">Minor.</param>
        /// <param name="build">Build revision.</param>
        public ProgVersion(byte major, byte minor, ushort build)
        {
            Major = major;
            Minor = minor;
            Build = build;
        }

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="major">Major.</param>
        /// <param name="minor">Minor.</param>
        /// <param name="buildHi">Build high byte.</param>
        /// <param name="buildLow">Build low byte.</param>
        public ProgVersion(byte major, byte minor, byte buildHi, byte buildLow)
            : this(major, minor, (ushort)((buildHi << 8) | buildLow))
        {
        }

        /// <summary>
        /// Gets a human readable string representation of this object.
        /// </summary>
        /// <returns>Human readable string representation.</returns>
        public override string ToString()
        {
            return $"ProgVersion[Major={Major}, Minor={Minor}, Build={Build}]";
        }

    }
}
