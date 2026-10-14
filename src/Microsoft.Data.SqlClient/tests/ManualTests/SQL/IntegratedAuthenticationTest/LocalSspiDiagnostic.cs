// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>Observes local SSPI context names and accepted token SIDs without SQL or network traffic.</summary>
    internal static class LocalSspiDiagnostic
    {
        /// <summary>
        /// Runs a bounded handshake, retaining the acquisition and client-call impersonation scope,
        /// and returns the accepted context's token user SID.
        /// </summary>
        internal static string Run(SafeAccessTokenHandle token, string label, string expectedSid, ITestOutputHelper output)
        {
            using var server = new Session(false);
            using Session client = token == null ? new Session(true)
                : WindowsIdentity.RunImpersonated(token, () => new Session(true));
            byte[] incoming = Array.Empty<byte>();
            try
            {
                for (int round = 0; round < 4; round++)
                {
                    byte[] clientBlob = token == null ? client.Step(incoming)
                        : WindowsIdentity.RunImpersonated(token, () => client.Step(incoming));
                    Array.Clear(incoming, 0, incoming.Length);
                    incoming = Array.Empty<byte>();
                    try
                    {
                        if (!server.Complete) incoming = server.Step(clientBlob);
                        else Assert.Empty(clientBlob);
                    }
                    finally { Array.Clear(clientBlob, 0, clientBlob.Length); }
                    if (server.Complete && client.Complete)
                    {
                        return server.Observe(label, expectedSid, output);
                    }
                }
                throw new InvalidOperationException("Local SSPI handshake exceeded four rounds.");
            }
            finally { Array.Clear(incoming, 0, incoming.Length); }
        }

        /// <summary>Owns one credential/context pair and releases native resources on every exit.</summary>
        private sealed class Session : IDisposable
        {
            private Handle _credential;
            private Handle _context;
            private bool _hasContext;
            private readonly bool _client;
            internal bool Complete { get; private set; }

            internal Session(bool client)
            {
                _client = client;
                Check(AcquireCredentialsHandle(null, "Negotiate", client ? 2 : 1,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out _credential, out _));
            }

            /// <summary>Exchanges an opaque token without publishing any authentication bytes.</summary>
            internal byte[] Step(byte[] incoming)
            {
                IntPtr previous = Marshal.AllocHGlobal(Marshal.SizeOf<Handle>());
                IntPtr inputBytes = IntPtr.Zero;
                IntPtr inputBuffer = IntPtr.Zero;
                IntPtr inputDescriptor = IntPtr.Zero;
                IntPtr outputBuffer = IntPtr.Zero;
                Buffer result = default;
                try
                {
                    Marshal.StructureToPtr(_context, previous, false);
                    if (incoming.Length != 0)
                    {
                        inputBytes = Marshal.AllocHGlobal(incoming.Length);
                        Marshal.Copy(incoming, 0, inputBytes, incoming.Length);
                        inputBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<Buffer>());
                        Marshal.StructureToPtr(new Buffer { Size = incoming.Length, Type = 2, Data = inputBytes }, inputBuffer, false);
                        inputDescriptor = Marshal.AllocHGlobal(Marshal.SizeOf<Descriptor>());
                        Marshal.StructureToPtr(new Descriptor { Count = 1, Buffers = inputBuffer }, inputDescriptor, false);
                    }
                    outputBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<Buffer>());
                    Marshal.StructureToPtr(new Buffer { Type = 2 }, outputBuffer, false);
                    var descriptor = new Descriptor { Count = 1, Buffers = outputBuffer };
                    Handle next;
                    int status = _client
                        ? InitializeSecurityContext(ref _credential, _hasContext ? previous : IntPtr.Zero,
                            "MSSQLSvc/" + Environment.MachineName + ":1433", 0x900, 0, 16,
                            inputDescriptor, 0, out next, ref descriptor, out _, out _)
                        : AcceptSecurityContext(ref _credential, _hasContext ? previous : IntPtr.Zero,
                            inputDescriptor, 0x900, 16, out next, ref descriptor, out _, out _);
                    if (next.Lower != IntPtr.Zero || next.Upper != IntPtr.Zero)
                    {
                        _context = next;
                        _hasContext = true;
                    }
                    result = Marshal.PtrToStructure<Buffer>(outputBuffer);
                    if (status == 0x90313 || status == 0x90314)
                    {
                        Check(CompleteAuthToken(ref _context, ref descriptor));
                        result = Marshal.PtrToStructure<Buffer>(outputBuffer);
                    }
                    if (status != 0 && status != 0x90312 && status != 0x90313 && status != 0x90314)
                        Check(status);
                    Complete = status == 0 || status == 0x90313;
                    if (result.Size < 0 || result.Size > 1024 * 1024)
                        throw new InvalidOperationException("Invalid local SSPI output length.");
                    byte[] blob = new byte[result.Size];
                    if (blob.Length != 0) Marshal.Copy(result.Data, blob, 0, blob.Length);
                    return blob;
                }
                finally
                {
                    int freeStatus = result.Data == IntPtr.Zero ? 0 : FreeContextBuffer(result.Data);
                    Marshal.FreeHGlobal(outputBuffer);
                    Marshal.FreeHGlobal(inputDescriptor);
                    Marshal.FreeHGlobal(inputBuffer);
                    if (inputBytes != IntPtr.Zero)
                    {
                        for (int i = 0; i < incoming.Length; i++) Marshal.WriteByte(inputBytes, i, 0);
                        Marshal.FreeHGlobal(inputBytes);
                    }
                    Marshal.FreeHGlobal(previous);
                    Check(freeStatus);
                }
            }

            /// <summary>Separately queries the SSPI name, negotiated package, and accepted access token.</summary>
            internal string Observe(string label, string expectedSid, ITestOutputHelper output)
            {
                Check(QueryContextAttributes(ref _context, 1, out IntPtr name));
                string contextName;
                try { contextName = Marshal.PtrToStringUni(name); }
                finally { Check(FreeContextBuffer(name)); }
                Check(QueryNegotiationInfo(ref _context, 12, out NegotiationInfo negotiation));
                string package;
                try { package = Marshal.PtrToStringUni(Marshal.PtrToStructure<PackageInfo>(negotiation.Package).Name); }
                finally { Check(FreeContextBuffer(negotiation.Package)); }
                Check(QuerySecurityContextToken(ref _context, out SafeAccessTokenHandle token));
                using (token)
                using (var identity = new WindowsIdentity(token.DangerousGetHandle()))
                {
                    output.WriteLine($"Local SSPI/{label}: contextName={contextName}; tokenName={identity.Name}; "
                        + $"tokenSid={identity.User.Value}; package={package}; impersonation={identity.ImpersonationLevel}.");
                    Assert.True(package == "NTLM" || package == "Kerberos",
                        $"Unexpected negotiated SSPI package '{package}'.");
                    if (expectedSid != null) Assert.Equal(expectedSid, identity.User.Value);
                    return identity.User.Value;
                }
            }

            public void Dispose()
            {
                try { if (_hasContext) Check(DeleteSecurityContext(ref _context)); }
                finally { Check(FreeCredentialsHandle(ref _credential)); }
            }
        }

        /// <summary>Surfaces native failures without emitting token contents.</summary>
        private static void Check(int status)
        {
            if (status != 0) throw new Win32Exception(status, $"Local SSPI status 0x{status:X8}.");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Handle { internal IntPtr Lower; internal IntPtr Upper; }
        [StructLayout(LayoutKind.Sequential)]
        private struct Buffer { internal int Size; internal int Type; internal IntPtr Data; }
        [StructLayout(LayoutKind.Sequential)]
        private struct Descriptor { internal int Version; internal int Count; internal IntPtr Buffers; }
        [StructLayout(LayoutKind.Sequential)]
        private struct NegotiationInfo { internal IntPtr Package; internal uint State; }
        [StructLayout(LayoutKind.Sequential)]
        private struct PackageInfo
        {
            internal uint Capabilities;
            internal ushort Version;
            internal ushort RpcId;
            internal uint MaxToken;
            internal IntPtr Name;
            internal IntPtr Comment;
        }

        [DllImport("secur32.dll", EntryPoint = "AcquireCredentialsHandleW", CharSet = CharSet.Unicode)]
        private static extern int AcquireCredentialsHandle(string principal, string package, int use,
            IntPtr logonId, IntPtr auth, IntPtr key, IntPtr keyArg, out Handle handle, out long expiry);
        [DllImport("secur32.dll", EntryPoint = "InitializeSecurityContextW", CharSet = CharSet.Unicode)]
        private static extern int InitializeSecurityContext(ref Handle credential, IntPtr context, string target,
            uint flags, int reserved, uint representation, IntPtr input, int reserved2,
            out Handle next, ref Descriptor output, out uint attributes, out long expiry);
        [DllImport("secur32.dll")]
        private static extern int AcceptSecurityContext(ref Handle credential, IntPtr context, IntPtr input,
            uint flags, uint representation, out Handle next, ref Descriptor output, out uint attributes, out long expiry);
        [DllImport("secur32.dll")]
        private static extern int CompleteAuthToken(ref Handle context, ref Descriptor token);
        [DllImport("secur32.dll", EntryPoint = "QueryContextAttributesW")]
        private static extern int QueryContextAttributes(ref Handle context, uint attribute, out IntPtr value);
        [DllImport("secur32.dll", EntryPoint = "QueryContextAttributesW")]
        private static extern int QueryNegotiationInfo(ref Handle context, uint attribute, out NegotiationInfo value);
        [DllImport("secur32.dll")]
        private static extern int QuerySecurityContextToken(ref Handle context, out SafeAccessTokenHandle token);
        [DllImport("secur32.dll")]
        private static extern int DeleteSecurityContext(ref Handle context);
        [DllImport("secur32.dll")]
        private static extern int FreeCredentialsHandle(ref Handle credential);
        [DllImport("secur32.dll")]
        private static extern int FreeContextBuffer(IntPtr buffer);
    }
}
