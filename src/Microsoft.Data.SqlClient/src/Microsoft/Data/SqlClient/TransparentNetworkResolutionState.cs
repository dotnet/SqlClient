// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

// This enum indicates the state of TransparentNetworkIPResolution
// The first attempt when TNIR is on should be sequential. If the first attempt fails next attempts should be parallel.
internal enum TransparentNetworkResolutionState
{
    DisabledMode = 0,
    SequentialMode,
    ParallelMode
}
