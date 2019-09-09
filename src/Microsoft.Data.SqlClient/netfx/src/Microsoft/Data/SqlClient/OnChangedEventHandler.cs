// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient {

    /// <summary>
    /// Handles the <see cref="Microsoft.Data.SqlClient.SqlDependency.OnChange"/> event that is fired when a notification is received for any of the commands associated with a <see cref="Microsoft.Data.SqlClient.SqlDependency" /> object.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    ///<remarks>
    ///<format type="text">
    ///<![CDATA[
    ///## Remarks
    ///The <xref:Microsoft.Data.SqlClient.SqlDependency.OnChange> event does not necessarily imply a change in the data. Other circumstances, such as time-out expired and failure to set the notification request, also generate <xref:Microsoft.Data.SqlClient.SqlDependency.OnChange>. 
    /// ]]>
    ///</format>
    ///</remarks>
    public delegate void OnChangeEventHandler(object sender, SqlNotificationEventArgs e);
}
    
