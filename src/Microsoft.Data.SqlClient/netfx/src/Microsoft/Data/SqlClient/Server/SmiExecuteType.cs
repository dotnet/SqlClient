//------------------------------------------------------------------------------
// <copyright file="SmiExecuteType.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">alazela</owner>
// <owner current="true" primary="false">stevesta</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data.SqlClient.Server {


    // enum representing the type of execution requested
    internal enum SmiExecuteType {
        NonQuery = 0,
        Reader = 1,
        ToPipe = 2,
    }
}

