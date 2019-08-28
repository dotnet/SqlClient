// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient {

    /// <summary>
    /// The public base class for auth providers.
    /// Implementations of this class must provide a default constructor if they are intended to be instantiated from app.config.
    /// </summary>
    public abstract class SqlAuthenticationProvider {

        /// Get an authentication provider by method.
        /// </summary>
        /// <param name="authenticationMethod">Authentication method.</param>
        /// <returns>Authentication provider or null if not found.</returns>
        public static SqlAuthenticationProvider GetProvider(SqlAuthenticationMethod authenticationMethod) {
            return SqlAuthenticationProviderManager.Instance.GetProvider(authenticationMethod);
        }

        /// <summary>
        /// Set an authentication provider by method.
        /// </summary>
        /// <param name="authenticationMethod">Authentication method.</param>
        /// <param name="provider">Authentication provider.</param>
        /// <returns>True if succeeded, false otherwise, e.g., the existing provider disallows overriding.</returns>
        public static bool SetProvider(SqlAuthenticationMethod authenticationMethod, SqlAuthenticationProvider provider) {
            return SqlAuthenticationProviderManager.Instance.SetProvider(authenticationMethod, provider);
        }

        /// <summary>
        /// This is called immediately before the provider is added in SQL driver's registry.
        /// Avoid performing long-waiting task in this method as it can block other threads from accessing provider registry.
        /// </summary>
        /// <param name="authenticationMethod">The authentication type in lower case.</param>
        public virtual void BeforeLoad(SqlAuthenticationMethod authenticationMethod) { }

        /// <summary>
        /// This is called immediately before the provider is removed from SQL driver's registry.
        /// E.g., when a different provider with the same authentication overrides this provider in SQL driver's registry.
        /// Avoid performing long-waiting task in this method as it can block other threads from accessing provider registry.
        /// </summary>
        /// <param name="authenticationMethod">The authentication type in lower case.</param>
        public virtual void BeforeUnload(SqlAuthenticationMethod authenticationMethod) { }

        /// <summary>
        /// A query method to check whether an authentication method is supported.
        /// </summary>
        /// <param name="authenticationMethod">Authentication method in lower case.</param>
        public abstract bool IsSupported(SqlAuthenticationMethod authenticationMethod);

        /// <summary>
        /// Get a token.
        /// </summary>
        public abstract Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters);
    }
}
