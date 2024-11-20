// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Interop.Windows.Secur32
{
    internal enum ContextAttribute
    {
        // sspi.h
        SECPKG_ATTR_SIZES = 0,
        SECPKG_ATTR_NAMES = 1,
        SECPKG_ATTR_LIFESPAN = 2,
        SECPKG_ATTR_DCE_INFO = 3,
        SECPKG_ATTR_STREAM_SIZES = 4,
        SECPKG_ATTR_AUTHORITY = 6,
        SECPKG_ATTR_PACKAGE_INFO = 10,
        SECPKG_ATTR_NEGOTIATION_INFO = 12,
        SECPKG_ATTR_UNIQUE_BINDINGS = 25,
        SECPKG_ATTR_ENDPOINT_BINDINGS = 26,
        SECPKG_ATTR_CLIENT_SPECIFIED_TARGET = 27,
        SECPKG_ATTR_APPLICATION_PROTOCOL = 35,

        // minschannel.h
        SECPKG_ATTR_REMOTE_CERT_CONTEXT = 0x53,    // returns PCCERT_CONTEXT
        SECPKG_ATTR_LOCAL_CERT_CONTEXT = 0x54,     // returns PCCERT_CONTEXT
        SECPKG_ATTR_ROOT_STORE = 0x55,             // returns HCERTCONTEXT to the root store
        SECPKG_ATTR_ISSUER_LIST_EX = 0x59,         // returns SecPkgContext_IssuerListInfoEx
        SECPKG_ATTR_CONNECTION_INFO = 0x5A,        // returns SecPkgContext_ConnectionInfo
        SECPKG_ATTR_UI_INFO = 0x68, // sets SEcPkgContext_UiInfo  
    }
}
