// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    ///
    /// </summary>
    internal sealed class ActiveDirectoryTokenCallbackAuthenticationProvider : SqlAuthenticationProvider
    {
        private readonly Func<AadTokenRequestContext, CancellationToken, Task<SqlAuthenticationToken>> _tokenCallback;
        private readonly string _type = typeof(ActiveDirectoryTokenCallbackAuthenticationProvider).Name;
        private readonly SqlClientLogger _logger = new();

        /// <summary>
        ///
        /// </summary>
        public ActiveDirectoryTokenCallbackAuthenticationProvider(Func<AadTokenRequestContext, CancellationToken, Task<SqlAuthenticationToken>> tokenCallback)
        {
            _tokenCallback = tokenCallback;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="authentication"></param>
        /// <returns></returns>
        public override bool IsSupported(SqlAuthenticationMethod authentication) => authentication switch
        {
            SqlAuthenticationMethod.ActiveDirectoryTokenCredential => true,
            SqlAuthenticationMethod.ActiveDirectoryIntegrated => true,
            SqlAuthenticationMethod.ActiveDirectoryPassword => true,
            SqlAuthenticationMethod.ActiveDirectoryInteractive => true,
            SqlAuthenticationMethod.ActiveDirectoryServicePrincipal => true,
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow => true,
            SqlAuthenticationMethod.ActiveDirectoryManagedIdentity => true,
            SqlAuthenticationMethod.ActiveDirectoryMSI => true,
            SqlAuthenticationMethod.ActiveDirectoryDefault => true,
            _ => false
        };

        /// <summary>
        ///
        /// </summary>
        /// <param name="authentication"></param>
        public override void BeforeLoad(SqlAuthenticationMethod authentication)
        {
            _logger.LogInfo(_type, "BeforeLoad", $"being loaded into SqlAuthProviders for {authentication}.");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="authentication"></param>
        public override void BeforeUnload(SqlAuthenticationMethod authentication)
        {
            _logger.LogInfo(_type, "BeforeUnload", $"being unloaded from SqlAuthProviders for {authentication}.");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            _logger.LogInfo(_type, nameof(AcquireTokenAsync), "Calling token callback.");
            CancellationTokenSource cts = new();

            // Use Connection timeout value to cancel token acquire request after certain period of time.
            cts.CancelAfter(parameters.ConnectionTimeout * 1000); // Convert to milliseconds
            return await _tokenCallback(new AadTokenRequestContext(parameters.Resource), cts.Token);
        }
    }
}
