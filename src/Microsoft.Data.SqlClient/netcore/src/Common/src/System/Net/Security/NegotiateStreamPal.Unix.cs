// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace System.Net.Security
{
    //
    // The class maintains the state of the authentication process and the security context.
    // It encapsulates security context and does the real work in authentication and
    // user data encryption with NEGO SSPI package.
    //
    internal static partial class NegotiateStreamPal
    {
        // value should match the Windows sspicli NTE_FAIL value
        // defined in winerror.h
        private const int NTE_FAIL = unchecked((int)0x80090020);

        private static bool GssInitSecurityContext(
            ref SafeGssContextHandle context,
            SafeGssCredHandle credential,
            bool isNtlm,
            SafeGssNameHandle targetName,
            Interop.NetSecurityNative.GssFlags inFlags,
            byte[] buffer,
            out byte[] outputBuffer,
            out uint outFlags,
            out int isNtlmUsed)
        {
            outputBuffer = null;
            outFlags = 0;

            // EstablishSecurityContext is called multiple times in a session.
            // In each call, we need to pass the context handle from the previous call.
            // For the first call, the context handle will be null.
            if (context == null)
            {
                context = new SafeGssContextHandle();
            }

            Interop.NetSecurityNative.GssBuffer token = default;
            Interop.NetSecurityNative.Status status;

            try
            {
                Interop.NetSecurityNative.Status minorStatus;
                status = Interop.NetSecurityNative.InitSecContext(out minorStatus,
                                                          credential,
                                                          ref context,
                                                          isNtlm,
                                                          targetName,
                                                          (uint)inFlags,
                                                          buffer,
                                                          (buffer == null) ? 0 : buffer.Length,
                                                          ref token,
                                                          out outFlags,
                                                          out isNtlmUsed);

                if ((status != Interop.NetSecurityNative.Status.GSS_S_COMPLETE) && (status != Interop.NetSecurityNative.Status.GSS_S_CONTINUE_NEEDED))
                {
                    throw new Interop.NetSecurityNative.GssApiException(status, minorStatus);
                }

                outputBuffer = token.ToByteArray();
            }
            finally
            {
                token.Dispose();
            }

            return status == Interop.NetSecurityNative.Status.GSS_S_COMPLETE;
        }

        private static SecurityStatusPal EstablishSecurityContext(
          SafeFreeNegoCredentials credential,
          ref SafeDeleteContext context,
          string targetName,
          ContextFlagsPal inFlags,
          SecurityBuffer inputBuffer,
          SecurityBuffer outputBuffer,
          ref ContextFlagsPal outFlags)
        {
            bool isNtlmOnly = credential.IsNtlmOnly;

            if (context == null)
            {
                // Empty target name causes the failure on Linux, hence passing a non-empty string  
                context = isNtlmOnly ? new SafeDeleteNegoContext(credential, credential.UserName) : new SafeDeleteNegoContext(credential, targetName);
            }

            SafeDeleteNegoContext negoContext = (SafeDeleteNegoContext)context;
            try
            {
                Interop.NetSecurityNative.GssFlags inputFlags = ContextFlagsAdapterPal.GetInteropFromContextFlagsPal(inFlags, isServer: false);
                uint outputFlags;
                int isNtlmUsed;
                SafeGssContextHandle contextHandle = negoContext.GssContext;
                bool done = GssInitSecurityContext(
                   ref contextHandle,
                   credential.GssCredential,
                   isNtlmOnly,
                   negoContext.TargetName,
                   inputFlags,
                   inputBuffer?.token,
                   out outputBuffer.token,
                   out outputFlags,
                   out isNtlmUsed);

                Debug.Assert(outputBuffer.token != null, "Unexpected null buffer returned by GssApi");
                outputBuffer.size = outputBuffer.token.Length;
                outputBuffer.offset = 0;
                outFlags = ContextFlagsAdapterPal.GetContextFlagsPalFromInterop((Interop.NetSecurityNative.GssFlags)outputFlags, isServer: false);
                Debug.Assert(negoContext.GssContext == null || contextHandle == negoContext.GssContext);

                // Save the inner context handle for further calls to NetSecurity
                Debug.Assert(negoContext.GssContext == null || contextHandle == negoContext.GssContext);
                if (null == negoContext.GssContext)
                {
                    negoContext.SetGssContext(contextHandle);
                }

                // Populate protocol used for authentication
                if (done)
                {
                    negoContext.SetAuthenticationPackage(Convert.ToBoolean(isNtlmUsed));
                }

                SecurityStatusPalErrorCode errorCode = done ?
                    (negoContext.IsNtlmUsed && outputBuffer.size > 0 ? SecurityStatusPalErrorCode.OK : SecurityStatusPalErrorCode.CompleteNeeded) :
                    SecurityStatusPalErrorCode.ContinueNeeded;
                return new SecurityStatusPal(errorCode);
            }
            catch (Exception ex)
            {
                if (NetEventSource.IsEnabled) NetEventSource.Error(null, ex);
                return new SecurityStatusPal(SecurityStatusPalErrorCode.InternalError, ex);
            }
        }

        internal static SecurityStatusPal InitializeSecurityContext(
            SafeFreeCredentials credentialsHandle,
            ref SafeDeleteContext securityContext,
            string spn,
            ContextFlagsPal requestedContextFlags,
            SecurityBuffer[] inSecurityBufferArray,
            SecurityBuffer outSecurityBuffer,
            ref ContextFlagsPal contextFlags)
        {
            // TODO (Issue #3718): The second buffer can contain a channel binding which is not supported
            if ((null != inSecurityBufferArray) && (inSecurityBufferArray.Length > 1))
            {
                throw new PlatformNotSupportedException(Strings.net_nego_channel_binding_not_supported);
            }

            SafeFreeNegoCredentials negoCredentialsHandle = (SafeFreeNegoCredentials)credentialsHandle;

            if (negoCredentialsHandle.IsDefault && string.IsNullOrEmpty(spn))
            {
                throw new PlatformNotSupportedException(Strings.net_nego_not_supported_empty_target_with_defaultcreds);
            }

            SecurityStatusPal status = EstablishSecurityContext(
                negoCredentialsHandle,
                ref securityContext,
                spn,
                requestedContextFlags,
                ((inSecurityBufferArray != null && inSecurityBufferArray.Length != 0) ? inSecurityBufferArray[0] : null),
                outSecurityBuffer,
                ref contextFlags);

            // Confidentiality flag should not be set if not requested
            if (status.ErrorCode == SecurityStatusPalErrorCode.CompleteNeeded)
            {
                ContextFlagsPal mask = ContextFlagsPal.Confidentiality;
                if ((requestedContextFlags & mask) != (contextFlags & mask))
                {
                    throw new PlatformNotSupportedException(Strings.net_nego_protection_level_not_supported);
                }
            }

            return status;
        }

        internal static int QueryMaxTokenSize(string package)
        {
            // This value is not used on Unix
            return 0;
        }
        
        internal static SafeFreeCredentials AcquireDefaultCredential(string package, bool isServer)
        {
            return AcquireCredentialsHandle(package, isServer, new NetworkCredential(string.Empty, string.Empty, string.Empty));
        }

        internal static SafeFreeCredentials AcquireCredentialsHandle(string package, bool isServer, NetworkCredential credential)
        {
            if (isServer)
            {
                throw new PlatformNotSupportedException(Strings.net_nego_server_not_supported);
            }

            bool isEmptyCredential = string.IsNullOrWhiteSpace(credential.UserName) ||
                                     string.IsNullOrWhiteSpace(credential.Password);
            bool ntlmOnly = string.Equals(package, NegotiationInfoClass.NTLM, StringComparison.OrdinalIgnoreCase);
            if (ntlmOnly && isEmptyCredential)
            {
                // NTLM authentication is not possible with default credentials which are no-op 
                throw new PlatformNotSupportedException(Strings.net_ntlm_not_possible_default_cred);
            }

            try
            {
                return isEmptyCredential ?
                    new SafeFreeNegoCredentials(false, string.Empty, string.Empty, string.Empty) :
                    new SafeFreeNegoCredentials(ntlmOnly, credential.UserName, credential.Password, credential.Domain);
            }
            catch(Exception ex)
            {
                throw new Win32Exception(NTE_FAIL, ex.Message);
            }
        }
    }
}
