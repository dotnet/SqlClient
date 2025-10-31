// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Azure.Identity;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

// Verify that we're running in an environment that supports Azure Pipelines
// Workload Identity Federation authentication.
public class WorkloadIdentityFederationTests
{
    [ConditionalFact(typeof(Config), nameof(Config.HasSystemAccessToken))]
    public async void GetCredential()
    {
        AzurePipelinesCredential credential = new(
            // The tenant ID if the managed identity associated to our workload
            // identity federation service connection.  See:
            //
            // https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/654fffd0-d02d-4894-b1b7-e2dfbc44a665/resourceGroups/aad-testlab-dl797892652000/providers/Microsoft.ManagedIdentity/userAssignedIdentities/dotnetMSI/properties
            "72f988bf-86f1-41af-91ab-2d7cd011db47",
            // The client ID of the managed identity associated to our workload
            // identity federation service connection.  See:
            //
            // https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/654fffd0-d02d-4894-b1b7-e2dfbc44a665/resourceGroups/aad-testlab-dl797892652000/providers/Microsoft.ManagedIdentity/userAssignedIdentities/dotnetMSI/overview 
            "92a44a21-5265-4fdd-9537-45b1cf54aa2d",

            // The Azure Dev Ops service connection ID (resourceId found in the
            // URL) of our workload identity federation setup.  See:
            //
            // https://sqlclientdrivers.visualstudio.com/public/_settings/adminservices?resourceId=ec9623b2-829c-497f-ae1f-7461766f9a9c
            "ec9623b2-829c-497f-ae1f-7461766f9a9c",
            Config.SystemAccessToken);

        // Acquire a token suitable for accessing Azure SQL databases.
        var token = await credential.GetTokenAsync(
            new(["https://database.windows.net/.default"]),
            CancellationToken.None);

        Assert.NotEmpty(token.Token);        
    }
}
