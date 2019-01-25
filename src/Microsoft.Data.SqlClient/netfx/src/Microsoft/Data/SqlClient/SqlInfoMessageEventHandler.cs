//------------------------------------------------------------------------------
// <copyright file="SqlInfoMessageEventHandler.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">blained</owner>
// <owner current="true" primary="false">laled</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data.SqlClient {


    /// <devdoc>
    ///    <para>
    ///       Represents the method that will handle the <see cref='Microsoft.Data.SqlClient.SQLConnection.InfoMessage'/> event of a <see cref='Microsoft.Data.SqlClient.SQLConnection'/>.
    ///    </para>
    /// </devdoc>
    public delegate void SqlInfoMessageEventHandler(object sender, SqlInfoMessageEventArgs e);
}
