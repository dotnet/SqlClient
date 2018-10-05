//------------------------------------------------------------------------------
// <copyright file="ConnectionState.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">markash</owner>
// <owner current="true" primary="false">laled</owner>
//------------------------------------------------------------------------------
using System;

namespace Microsoft.Data {

    [Flags()]
    public enum ConnectionState {
        Closed     = 0,
        Open       = 1,
        Connecting = 2,
        Executing  = 4,
        Fetching   = 8,
        Broken     = 16,
    }
}
