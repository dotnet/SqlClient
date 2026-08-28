// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Parser;

internal enum SniContext
{
    Undefined = 0,
    Snix_Connect,
    Snix_PreLoginBeforeSuccessfulWrite,
    Snix_PreLogin,
    Snix_LoginSspi,
    Snix_ProcessSspi,
    Snix_Login,
    Snix_EnableMars,
    Snix_AutoEnlist,
    Snix_GetMarsSession,
    Snix_Execute,
    Snix_Read,
    Snix_Close,
    Snix_SendRows,
}
