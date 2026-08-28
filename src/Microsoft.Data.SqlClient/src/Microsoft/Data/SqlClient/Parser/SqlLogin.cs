// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security;
using Microsoft.Data.Common.ConnectionString;

namespace Microsoft.Data.SqlClient.Parser;

internal sealed class SqlLogin
{
    internal SqlAuthenticationMethod authentication = SqlAuthenticationMethod.NotSpecified;  // Authentication type
    internal int timeout;                                            // login timeout
    internal bool userInstance = false;                              // user instance
    internal string hostName = "";                                   // client machine name
    internal string userName = "";                                   // user id
    internal string password = "";                                   // password
    internal string applicationName = "";                            // application name
    internal string serverName = "";                                 // server name
    internal string language = "";                                   // initial language
    internal string database = "";                                   // initial database
    internal string attachDBFilename = "";                           // DB filename to be attached
    internal bool useReplication = false;                            // user login for replication
    internal string newPassword = "";                                // new password for reset password
    internal bool useSSPI = false;                                   // use integrated security
    internal int packetSize = DbConnectionStringDefaults.PacketSize; // packet size
    internal bool readOnlyIntent = false;                            // read-only intent
    internal SqlCredential credential;                               // user id and password in SecureString
    internal SecureString newSecurePassword;
}
