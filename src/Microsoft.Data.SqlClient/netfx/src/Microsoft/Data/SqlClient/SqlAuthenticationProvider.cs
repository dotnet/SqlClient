// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient {

    /// <summary>
    /// Defines the core behavior of authentication providers and provides a base class for derived classes.
    /// Implementations of this class must provide a default constructor if they are intended to be instantiated from app.config.
    /// </summary>
    public abstract class SqlAuthenticationProvider {

        /// <summary>
        /// Called from constructors in derived classes to initialize the <see cref="Microsoft.Data.SqlClient.SqlAuthenticationProvider" /> class.
        /// </summary>
        /// <param name="authenticationMethod"></param>
        /// <returns></returns>
        /// <remarks>To be added.</remarks>
        public static SqlAuthenticationProvider GetProvider(SqlAuthenticationMethod authenticationMethod) {
            return SqlAuthenticationProviderManager.Instance.GetProvider(authenticationMethod);
        }

        /// <summary>
        /// Set an authentication provider by method.
        /// </summary>
        /// <param name="authenticationMethod">Authentication method.</param>
        /// <param name="provider">Authentication provider.</param>
        /// <returns>True if the operation succeeded; otherwise, <see langword="false" /> (for example, the existing provider disallows overriding).</returns>
        /// <returns><see langword="true" /> if the operation succeeded.</returns>
        /// <remarks>To be added.</remarks>
        public static bool SetProvider(SqlAuthenticationMethod authenticationMethod, SqlAuthenticationProvider provider) {
            return SqlAuthenticationProviderManager.Instance.SetProvider(authenticationMethod, provider);
        }

        /// <summary>
        /// This is called immediately before the provider is added in SQL driver's registry.
        /// Avoid performing long-waiting task in this method as it can block other threads from accessing provider registry.
        /// </summary>
        /// <param name="authenticationMethod">The authentication type in lower case.</param>
        /// <remarks>Avoid performing long-waiting tasks in this method, since it can block other threads from accessing the provider registry.</remarks>
        public virtual void BeforeLoad(SqlAuthenticationMethod authenticationMethod) { }

        /// <summary>
        /// This is called immediately before the provider is removed from SQL driver's registry.
        /// E.g., when a different provider with the same authentication overrides this provider in SQL driver's registry.
        /// Avoid performing long-waiting task in this method as it can block other threads from accessing provider registry.
        /// </summary>
        /// <param name="authenticationMethod">The authentication type in lower case.</param>
        /// <remarks>For example, this method is called when a different provider with the same authentication method overrides this provider in the SQL drivers registry. Avoid performing long-waiting task in this method, since it can block other threads from accessing the provider registry.</remarks>
        public virtual void BeforeUnload(SqlAuthenticationMethod authenticationMethod) { }

        /// <summary>
        /// A query method to check whether an authentication method is supported.
        /// </summary>
        /// <param name="authenticationMethod">Authentication method in lower case.</param>
        /// <remarks>To be added.</remarks>
        /// <returns><see langword="true"/> if the specified authentication method is supported; otherwise, <see langword="false" />.</returns>
        public abstract bool IsSupported(SqlAuthenticationMethod authenticationMethod);

        /// <summary>
        /// Acquires a security token from the authority.
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns>Represents an asynchronous operation that returns the AD authentication token.</returns>
        public abstract Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters);
    }
}
