// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Interop.Windows.Sni
{
    internal enum TransparentNetworkResolutionMode : byte
    {
        DisabledMode = 0,
        SequentialMode,
        ParallelMode
    }
}
