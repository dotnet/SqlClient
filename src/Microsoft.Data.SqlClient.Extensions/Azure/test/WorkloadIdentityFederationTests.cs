// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Azure.Identity;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

// Verify that we're running in an environment that supports Azure Pipelines
// Workload Identity Federation authentication.
public class WorkloadIdentityFederationTests
{
    [ConditionalFact(
        typeof(Config),
        nameof(Config.HasSystemAccessToken),
        nameof(Config.HasTenantId),
        nameof(Config.HasUserManagedIdentityClientId),
        nameof(Config.HasWorkloadIdentityFederationServiceConnectionId))]
    public async Task GetCredential()
    {
        AzurePipelinesCredential credential = new(
            // The tenant ID of the managed identity associated to our workload
            // identity federation service connection.  See:
            //
            // https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/654fffd0-d02d-4894-b1b7-e2dfbc44a665/resourceGroups/aad-testlab-dl797892652000/providers/Microsoft.ManagedIdentity/userAssignedIdentities/dotnetMSI/properties
            //
            // Note that we need a service connection configured in each Azure DevOps project
            // (Public and ADO.Net) that uses this tenant ID.
            //
            Config.TenantId,

            // The client ID of the managed identity associated to our workload
            // identity federation service connection.  See:
            //
            // https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/654fffd0-d02d-4894-b1b7-e2dfbc44a665/resourceGroups/aad-testlab-dl797892652000/providers/Microsoft.ManagedIdentity/userAssignedIdentities/dotnetMSI/overview
            //
            Config.UserManagedIdentityClientId,

            // The Azure Dev Ops service connection ID (resourceId found in the
            // URL) of our workload identity federation setup.
            //
            // Note that we need a service connection configured in each Azure
            // DevOps project (Public and ADO.Net).
            //
            // Public project:
            //
            // https://sqlclientdrivers.visualstudio.com/public/_settings/adminservices?resourceId=ec9623b2-829c-497f-ae1f-7461766f9a9c
            //
            // ADO.Net project:
            //
            // https://sqlclientdrivers.visualstudio.com/ADO.Net/_settings/adminservices?resourceId=c29947a8-df6a-4ceb-b2d4-1676c57c37b9
            //
            Config.WorkloadIdentityFederationServiceConnectionId,

            // The system access token provided by Azure Pipelines.
            Config.SystemAccessToken);

        // Acquire a token suitable for accessing Azure SQL databases.
        var token = await credential.GetTokenAsync(
            new(["https://database.windows.net/.default"]),
            CancellationToken.None);

        Assert.NotEmpty(token.Token);
    }
}
