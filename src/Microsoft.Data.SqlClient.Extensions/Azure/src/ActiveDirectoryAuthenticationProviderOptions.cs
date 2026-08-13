// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Identity.Client;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../doc/ActiveDirectoryAuthenticationProviderOptions.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProviderOptions"]/*'/>
    public sealed class ActiveDirectoryAuthenticationProviderOptions
    {
        /// <include file='../doc/ActiveDirectoryAuthenticationProviderOptions.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProviderOptions"]/DeviceCodeFlowCallback/*'/>
        public Func<DeviceCodeResult, Task>? DeviceCodeFlowCallback { get; set; }

        /// <include file='../doc/ActiveDirectoryAuthenticationProviderOptions.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProviderOptions"]/ApplicationClientId/*'/>
        public string? ApplicationClientId { get; set; }
        
        /// <include file='../doc/ActiveDirectoryAuthenticationProviderOptions.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProviderOptions"]/UseWamBroker/*'/>
        public bool UseWamBroker { get; set; }
    }
}
