// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    internal class SniCommon
    {
        // Each error number maps to SNI_ERROR_* in String.resx
        internal const int ConnTerminatedError = 2;
        internal const int InvalidParameterError = 5;
        internal const int ProtocolNotSupportedError = 8;
        internal const int ConnTimeoutError = 11;
        internal const int ConnNotUsableError = 19;
        internal const int InvalidConnStringError = 25;
        internal const int ErrorLocatingServerInstance = 26;
        internal const int HandshakeFailureError = 31;
        internal const int InternalExceptionError = 35;
        internal const int ConnOpenFailedError = 40;
        internal const int ErrorSpnLookup = 44;
        internal const int LocalDBErrorCode = 50;
        internal const int MultiSubnetFailoverWithMoreThan64IPs = 47;
        internal const int MultiSubnetFailoverWithInstanceSpecified = 48;
        internal const int MultiSubnetFailoverWithNonTcpProtocol = 49;
        internal const int MaxErrorValue = 50157;
        internal const int LocalDBNoInstanceName = 51;
        internal const int LocalDBNoInstallation = 52;
        internal const int LocalDBInvalidConfig = 53;
        internal const int LocalDBNoSqlUserInstanceDllPath = 54;
        internal const int LocalDBInvalidSqlUserInstanceDllPath = 55;
        internal const int LocalDBFailedToLoadDll = 56;
        internal const int LocalDBBadRuntime = 57;

        /// <summary>
        /// We either validate that the provided 'validationCert' matches the 'serverCert', or we validate that the server name in the 'serverCert' matches 'targetServerName'.
        /// Certificate validation and chain trust validations are done by SSLStream class [System.Net.Security.SecureChannel.VerifyRemoteCertificate method]
        /// This method is called as a result of callback for SSL Stream Certificate validation.
        /// </summary>
        /// <remarks>
        /// When <paramref name="validationCertFileName"/> is supplied, the presented server
        /// certificate is always compared against it, even when <paramref name="policyErrors"/> is
        /// <see cref="SslPolicyErrors.None"/>. An exact match satisfies certificate validation. A
        /// configured certificate that cannot be loaded or parsed, or that does not match, fails
        /// the connection; it is never silently ignored.
        /// <para>
        /// The one condition a match does not satisfy is a missing server certificate. SslStream
        /// reports that as <see cref="SslPolicyErrors.RemoteCertificateNotAvailable"/> and passes a
        /// null <paramref name="serverCert"/>; the two are expected to agree, but both are checked
        /// so that neither an absent certificate nor the contradictory combination can be accepted
        /// on the basis of a comparison.
        /// </para>
        /// </remarks>
        /// <param name="connectionId">Connection ID/GUID for tracing</param>
        /// <param name="targetServerName">Server that client is expecting to connect to</param>
        /// <param name="hostNameInCertificate">Optional hostname to use for server certificate validation</param>
        /// <param name="serverCert">X.509 certificate from the server</param>
        /// <param name="validationCertFileName">Path to an X.509 certificate file from the application to compare with the serverCert</param>
        /// <param name="policyErrors">Policy errors</param>
        /// <returns>True if certificate is valid</returns>
        internal static bool ValidateSslServerCertificate(Guid connectionId, string targetServerName, string hostNameInCertificate, X509Certificate serverCert, string validationCertFileName, SslPolicyErrors policyErrors)
        {
            ServerCertificateVerdict verdict = default;
            return ValidateSslServerCertificate(connectionId, targetServerName, hostNameInCertificate, serverCert, validationCertFileName, policyErrors, callbackChain: null, ref verdict);
        }

        /// <summary>
        /// Validates the server certificate exactly as
        /// <see cref="ValidateSslServerCertificate(Guid, string, string, X509Certificate, string, SslPolicyErrors)"/>
        /// does, and additionally records SqlClient's effective verdict in <paramref name="verdict"/>.
        /// </summary>
        /// <remarks>
        /// Recording never changes the trust decision, never performs additional validation or
        /// network work, and never alters exception messages or trace text. <paramref name="verdict"/>
        /// is written immediately before this method returns or throws its validation exception, so
        /// it is observable by the caller in both cases. If validation is aborted for any other
        /// reason (for example the <c>ServerCertificate</c> file cannot be loaded), the validation
        /// errors are left unavailable because no comparison was completed. If the host name check
        /// throws (for example for the "." named-pipe target), the errors already established before
        /// it (such as <see cref="SqlServerCertificateValidationErrors.ChainError"/>) and the chain
        /// status remain recorded; the exception itself is not caught or changed.
        /// <para>
        /// <see cref="ServerCertificateVerdict.ChainStatus"/> is the bitwise OR of the status flags of
        /// the original <paramref name="callbackChain"/> supplied to the TLS validation callback. It is only
        /// recorded when that chain is consulted (no <c>ServerCertificate</c> pin and a certificate
        /// was presented); it is <see langword="null"/> in pin mode, when no certificate was
        /// presented, when no chain was supplied, or when chain errors were reported but the chain
        /// carries no status flags.
        /// </para>
        /// </remarks>
        internal static bool ValidateSslServerCertificate(Guid connectionId, string targetServerName, string hostNameInCertificate, X509Certificate serverCert, string validationCertFileName, SslPolicyErrors policyErrors, X509Chain callbackChain, ref ServerCertificateVerdict verdict)
        {
            verdict = default;

            using (SqlClientSNIEventScope.Create(nameof(SniCommon)))
            {
                bool pinConfigured = !string.IsNullOrEmpty(validationCertFileName);

                // The callback chain is only consulted when trust is decided by the platform chain,
                // i.e. no ServerCertificate pin is configured and a certificate was presented.
                X509ChainStatusFlags? callbackChainStatus = null;
                if (!pinConfigured && serverCert is not null && !policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
                {
                    callbackChainStatus = GetChainStatus(callbackChain, policyErrors);
                }

                // When a pin is configured, require it to match even if the platform trusts the cert.
                if (!pinConfigured && policyErrors == SslPolicyErrors.None)
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniCommon), EventType.INFO, "Connection Id {0}, targetServerName {1}, SSL Server certificate has no policy errors and no ServerCertificate was supplied; skipping certificate comparison.", args0: connectionId, args1: targetServerName);
                    verdict = new ServerCertificateVerdict(SqlServerCertificateValidationErrors.None, callbackChainStatus);
                    return true;
                }

                string serverNameToValidate;
                if (!string.IsNullOrEmpty(hostNameInCertificate))
                {
                    serverNameToValidate = hostNameInCertificate;
                }
                else
                {
                    serverNameToValidate = targetServerName;
                }

                if (pinConfigured)
                {
                    // There is no trustworthy certificate to compare against when the server
                    // presented none. SslStream reports that as RemoteCertificateNotAvailable, and
                    // serverCert is null in that case; both are checked because a matching
                    // ServerCertificate must never satisfy validation when the presented
                    // certificate is absent or cannot be relied upon.
                    if (serverCert is null || policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniCommon), EventType.ERR, "Connection Id {0}, ServerCertificate was specified but the server presented no certificate. Certificate validation failed.", args0: connectionId);
                        verdict = new ServerCertificateVerdict(SqlServerCertificateValidationErrors.ServerCertificateMissing, chainStatus: null);
                        throw ADP.SSLCertificateAuthenticationException(Strings.SQL_ServerCertificateNotAvailable);
                    }

                    using X509Certificate validationCertificate = LoadValidationCertificate(connectionId, validationCertFileName);

                    if (serverCert.GetRawCertData().AsSpan().SequenceEqual(validationCertificate.GetRawCertData().AsSpan()))
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniCommon), EventType.INFO, "Connection Id {0}, ServerCertificate matches the certificate provided by the server. Certificate validation passed.", args0: connectionId);
                        verdict = new ServerCertificateVerdict(SqlServerCertificateValidationErrors.None, chainStatus: null);
                        return true;
                    }
                    else
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniCommon), EventType.ERR, "Connection Id {0}, ServerCertificate doesn't match the certificate provided by the server. Certificate validation failed.", args0: connectionId);
                        verdict = new ServerCertificateVerdict(SqlServerCertificateValidationErrors.PinnedCertificateMismatch, chainStatus: null);
                        throw ADP.SSLCertificateAuthenticationException(Strings.SQL_RemoteCertificateDoesNotMatchServerCertificate);
                    }
                }

                // If we get to this point then there is a ssl policy flag.
                SqlServerCertificateValidationErrors validationErrors = SqlServerCertificateValidationErrors.None;
                StringBuilder messageBuilder = new();
                if (policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
                {
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniCommon), EventType.ERR, "Connection Id {0}, targetServerName {1}, SSL Server certificate not validated as PolicyErrors set to RemoteCertificateNotAvailable.", args0: connectionId, args1: targetServerName);
                    messageBuilder.AppendLine(Strings.SQL_RemoteCertificateNotAvailable);
                    validationErrors |= SqlServerCertificateValidationErrors.ServerCertificateMissing;
                }

                if (policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
                {
                    validationErrors |= SqlServerCertificateValidationErrors.ChainError;
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniCommon), EventType.ERR, "Connection Id {0}, targetServerName {0}, SslPolicyError {1}, SSL Policy certificate chain has errors.", args0: connectionId, args1: targetServerName, args2: policyErrors);

                    // get the chain status from the certificate
                    X509Certificate2 cert2 = serverCert as X509Certificate2;
                    X509Chain chain = new();
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Offline;
                    StringBuilder chainStatusInformation = new();
                    bool chainIsValid = chain.Build(cert2);
                    Debug.Assert(!chainIsValid, "RemoteCertificateChainError flag is detected, but certificate chain is valid.");
                    if (!chainIsValid)
                    {
                        foreach (X509ChainStatus chainStatus in chain.ChainStatus)
                        {
                            chainStatusInformation.Append($"{chainStatus.StatusInformation}, [Status: {chainStatus.Status}]");
                            chainStatusInformation.AppendLine();
                        }
                    }
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniCommon), EventType.ERR, "Connection Id {0}, targetServerName {1}, SslPolicyError {2}, SSL Policy certificate chain has errors. ChainStatus {3}", args0: connectionId, args1: targetServerName, args2: policyErrors, args3: chainStatusInformation);
                    messageBuilder.AppendFormat(Strings.SQL_RemoteCertificateChainErrors, chainStatusInformation);
                    messageBuilder.AppendLine();
                }

                if (serverCert is null)
                {
                    validationErrors |= SqlServerCertificateValidationErrors.ServerCertificateMissing;
                }

                if (policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
                {
                    // The host name check can throw before the final verdict is recorded (for
                    // example MatchesHostname rejects the "." target used by named pipes). Record
                    // the errors already established so they survive that exception. A provisional
                    // None is not recorded because it would report a validation that never passed.
                    if (validationErrors != SqlServerCertificateValidationErrors.None)
                    {
                        verdict = new ServerCertificateVerdict(validationErrors, callbackChainStatus);
                    }

                    X509Certificate2 cert2 = serverCert as X509Certificate2;
                    if (!cert2.MatchesHostname(serverNameToValidate))
                    {
                        SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniCommon), EventType.ERR, "Connection Id {0}, serverNameToValidate {1}, Target Server name or HNIC does not match the Subject/SAN in Certificate.", args0: connectionId, args1: serverNameToValidate);
                        messageBuilder.AppendLine(Strings.SQL_RemoteCertificateNameMismatch);
                        validationErrors |= SqlServerCertificateValidationErrors.HostNameMismatch;
                    }
                }

                verdict = new ServerCertificateVerdict(validationErrors, callbackChainStatus);

                if (messageBuilder.Length > 0)
                {
                    throw ADP.SSLCertificateAuthenticationException(messageBuilder.ToString());
                }

                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniCommon), EventType.INFO, "Connection Id {0}, certificate with subject: {1}, validated successfully.", args0: connectionId, args1: serverCert.Subject);
                return true;
            }
        }

        /// <summary>
        /// Combines the status flags of the chain supplied to the TLS validation callback without
        /// building or re-evaluating the chain.
        /// </summary>
        /// <param name="chain">The original callback chain, if any.</param>
        /// <param name="policyErrors">The policy errors supplied to the callback.</param>
        /// <returns>
        /// The bitwise OR of the chain element status flags, <see cref="X509ChainStatusFlags.NoError"/>
        /// for a clean chain, or <see langword="null"/> when the chain is unavailable or chain errors
        /// were reported without any status flags.
        /// </returns>
        internal static X509ChainStatusFlags? GetChainStatus(X509Chain chain, SslPolicyErrors policyErrors)
        {
            return chain is null ? null : CombineChainStatus(chain.ChainStatus, policyErrors);
        }

        /// <summary>
        /// Combines chain status entries into a single set of flags.
        /// </summary>
        /// <param name="statuses">The chain status entries.</param>
        /// <param name="policyErrors">The policy errors supplied to the callback.</param>
        /// <returns>See <see cref="GetChainStatus(X509Chain, SslPolicyErrors)"/>.</returns>
        internal static X509ChainStatusFlags? CombineChainStatus(X509ChainStatus[] statuses, SslPolicyErrors policyErrors)
        {
            X509ChainStatusFlags flags = X509ChainStatusFlags.NoError;
            foreach (X509ChainStatus status in statuses ?? Array.Empty<X509ChainStatus>())
            {
                flags |= status.Status;
            }

            if (flags == X509ChainStatusFlags.NoError && policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
            {
                // A chain error was reported but the chain does not describe it; report the chain
                // status as unavailable rather than as a clean chain.
                return null;
            }

            return flags;
        }

        /// <summary>
        /// Creates the transport-level certificate capture for one TLS validation callback
        /// invocation.
        /// </summary>
        /// <param name="serverCert">The certificate presented by the server, if any.</param>
        /// <param name="verdict">The verdict recorded by validation; default when not evaluated.</param>
        /// <param name="sender">The <see cref="SslStream"/> raising the validation callback, if any.</param>
        /// <returns>The capture. Never <see langword="null"/>.</returns>
        internal static ServerCertificateCapture CreateServerCertificateCapture(X509Certificate serverCert, in ServerCertificateVerdict verdict, object sender = null)
        {
            byte[] hash = null;
            if (serverCert is not null)
            {
                try
                {
                    hash = SHA256.HashData(serverCert.GetRawCertData());
                }
                catch (CryptographicException)
                {
                    // Capture must never affect the handshake outcome; leave the hash unavailable.
                }
            }

            return new ServerCertificateCapture(hash, verdict.ValidationErrors, verdict.ChainStatus, GetHandshakeProtocol(sender));
        }

        /// <summary>
        /// Reads the TLS protocol version agreed for the handshake in progress. SslStream raises the
        /// certificate validation callback after the protocol version has been agreed, so this
        /// reflects what was observed for the attempt even if the certificate is then rejected.
        /// </summary>
        /// <returns>The protocol, or <see langword="null"/> when it is not available.</returns>
        private static SslProtocols? GetHandshakeProtocol(object sender)
        {
            if (sender is not SslStream sslStream)
            {
                return null;
            }

            try
            {
                SslProtocols protocol = sslStream.SslProtocol;
                return protocol == SslProtocols.None ? null : protocol;
            }
            catch (Exception)
            {
                // Capture must never affect the handshake outcome.
                return null;
            }
        }

        /// <summary>
        /// Loads the certificate specified by the <c>ServerCertificate</c> option, failing closed
        /// when it cannot be loaded or parsed.
        /// </summary>
        /// <param name="connectionId">Connection ID/GUID for tracing</param>
        /// <param name="validationCertFileName">Path to the X.509 certificate file</param>
        /// <exception cref="System.Security.Authentication.AuthenticationException">
        /// Thrown when the file cannot be loaded or parsed.
        /// </exception>
        private static X509Certificate LoadValidationCertificate(Guid connectionId, string validationCertFileName)
        {
            try
            {
#if NET9_0_OR_GREATER
                return X509CertificateLoader.LoadCertificateFromFile(validationCertFileName);
#else
                return new X509Certificate(validationCertFileName);
#endif
            }
            catch (Exception e)
            {
                // Fail closed: the caller explicitly asked us to compare against a specific
                // certificate, so if we cannot load / parse it we must not fall back to validating
                // by host name alone. The exception details are traced separately; the user-facing
                // message identifies the configured file path so operators can locate the
                // misconfiguration.
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniCommon), EventType.ERR, "Connection Id {0}, Exception occurred loading specified ServerCertificate '{1}': {2}. Failing certificate validation.", args0: connectionId, args1: validationCertFileName, args2: e.Message);
                throw ADP.SSLCertificateAuthenticationException(StringsHelper.GetString(Strings.SQL_ServerCertificateFileLoadFailed, validationCertFileName));
            }
        }

        internal static IPAddress[] GetDnsIpAddresses(string serverName, TimeoutTimer timeout)
        {
            IPAddress[] ipAddresses = GetDnsIpAddresses(serverName);

            // We cannot timeout accurately in sync code above, so throw TimeoutException if we've now exceeded the timeout.
            if (timeout.IsExpired)
            {
                throw new TimeoutException();
            }
            return ipAddresses;
        }

        internal static IPAddress[] GetDnsIpAddresses(string serverName)
        {
            using (SqlClientSNIEventScope.Create(nameof(SniCommon)))
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniCommon), EventType.INFO, "Getting DNS host entries for serverName {0}.", args0: serverName);
                return Dns.GetHostAddresses(serverName);
            }
        }

        /// <summary>
        /// Sets last error encountered for SNI
        /// </summary>
        /// <param name="provider">SNI provider</param>
        /// <param name="nativeError">Native error code</param>
        /// <param name="sniError">SNI error code</param>
        /// <param name="errorMessage">Error message</param>
        /// <returns></returns>
        internal static uint ReportSNIError(SniProviders provider, int nativeError, uint sniError, string errorMessage)
        {
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniCommon), EventType.ERR, "Provider = {0}, native Error = {1}, SNI Error = {2}, Error Message = {3}", args0: provider, args1: nativeError, args2: sniError, args3: errorMessage);
            return ReportSNIError(new SniError(provider, nativeError, sniError, errorMessage));
        }

        /// <summary>
        /// Sets last error encountered for SNI
        /// </summary>
        /// <param name="provider">SNI provider</param>
        /// <param name="sniError">SNI error code</param>
        /// <param name="sniException">SNI Exception</param>
        /// <param name="nativeErrorCode">Native SNI error code</param>
        /// <returns></returns>
        internal static uint ReportSNIError(SniProviders provider, uint sniError, Exception sniException, int nativeErrorCode = 0)
        {
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniCommon), EventType.ERR, "Provider = {0}, SNI Error = {1}, Exception = {2}", args0: provider, args1: sniError, args2: sniException?.Message);
            return ReportSNIError(new SniError(provider, sniError, sniException, nativeErrorCode));
        }

        /// <summary>
        /// Sets last error encountered for SNI
        /// </summary>
        /// <param name="error">SNI error</param>
        /// <returns></returns>
        internal static uint ReportSNIError(SniError error)
        {
            SniLoadHandle.LastError = error;
            return TdsEnums.SNI_ERROR;
        }
    }

    /// <summary>
    /// SqlClient's effective server certificate validation verdict for one TLS validation callback.
    /// Default values (all <see langword="null"/>) mean the certificate was not evaluated.
    /// </summary>
    internal readonly struct ServerCertificateVerdict
    {
        internal ServerCertificateVerdict(SqlServerCertificateValidationErrors? validationErrors, X509ChainStatusFlags? chainStatus)
        {
            ValidationErrors = validationErrors;
            ChainStatus = chainStatus;
        }

        internal SqlServerCertificateValidationErrors? ValidationErrors { get; }

        internal X509ChainStatusFlags? ChainStatus { get; }
    }
}

#endif
