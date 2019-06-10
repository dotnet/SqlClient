// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


#include "sni_wrapper.hpp"
#include "sni_error.hpp"

//System::Guid FromGUID(GUID const & guid)
//{
//    return System::Guid(guid.Data1, guid.Data2, guid.Data3,
//        guid.Data4[0], guid.Data4[1],
//        guid.Data4[2], guid.Data4[3],
//        guid.Data4[4], guid.Data4[5],
//        guid.Data4[6], guid.Data4[7]);
//}
//
//static inline
//IUnknown * GetDefaultAppDomain()
//{
//    ICorRuntimeHost *pHost = NULL;
//
//    try
//    {
//        // Throws HR exception on failure.
//        pHost = reinterpret_cast<ICorRuntimeHost*>(
//            RuntimeEnvironment::GetRuntimeInterfaceAsIntPtr(
//                FromGUID(__uuidof(CorRuntimeHost)),
//                FromGUID(__uuidof(ICorRuntimeHost))).ToPointer());
//    }
//    catch (Exception^)
//    {
//        return NULL;
//    }
//
//    // GetDefaultDomain will not throw.
//    IUnknown *pAppDomainPunk = NULL;
//    HRESULT hr = pHost->GetDefaultDomain(&pAppDomainPunk);
//    pHost->Release();
//
//    return SUCCEEDED(hr) ? pAppDomainPunk : NULL;
//}

typedef PVOID(WINAPI *tSqlClientCertificateDelegate)(PVOID pContext);

struct SNIAuthProviderInfoWrapper
{
    PVOID pDelegateContext;
    tSqlClientCertificateDelegate pSqlClientCertificateDelegate;
};

extern void SNISetLastError(
    ProviderNum Provider,
    DWORD       dwNativeError,
    DWORD       dwSNIError,
    WCHAR     * pszFileName,
    WCHAR     * pszFunction,
    DWORD       dwLineNumber);

extern void __cdecl SNIGetLastError(__out_opt SNI_ERROR * pSNIerror);

#undef Assert

SNI_Packet * __cdecl SNIPacketAllocateWrapper(__in SNI_ConnWrapper * pConn, SNI_Packet_IOType IOType)
{
    return SNIPacketAllocate(pConn->m_pConn, IOType);
}

DWORD __cdecl SNIWriteAsyncWrapper(__inout SNI_ConnWrapper * pConn, __in SNI_Packet * pPacket)
{
    pConn->m_fSyncOverAsyncWrite = false;
    return SNIWriteAsync(pConn->m_pConn, pPacket);
}

DWORD __cdecl SNIWriteSyncOverAsync(__inout SNI_ConnWrapper * pConn, __in SNI_Packet * pPacket)
{
    if (pConn->m_fSupportsSyncOverAsync)
    {
        return SNIWriteSync(pConn->m_pConn, pPacket, NULL);
    }

    pConn->m_fSyncOverAsyncWrite = true;
    DWORD dwError = SNIWriteAsync(pConn->m_pConn, pPacket);
    if (ERROR_IO_PENDING == dwError)
    {
        dwError = ::WaitForSingleObject(pConn->m_WriteResponseReady, INFINITE);
        if (ERROR_SUCCESS == dwError)
        {
            if (pConn->m_WriteError.dwNativeError != ERROR_SUCCESS)
            {
                SNISetLastError(pConn->m_WriteError.Provider, pConn->m_WriteError.dwNativeError, pConn->m_WriteError.dwSNIError, NULL, NULL, 0);
            }
            dwError = pConn->m_WriteError.dwNativeError;
        }
        else
        {
            SNISetLastError(INVALID_PROV, dwError, SNIE_SYSTEM, NULL, NULL, 0);
        }
    }

    assert(dwError != ERROR_IO_PENDING); // should never return pending
    return dwError;
}

DWORD __cdecl SNIReadAsyncWrapper(__inout SNI_ConnWrapper * pConn, __out SNI_Packet ** ppNewPacket)
{
    pConn->m_fSyncOverAsyncRead = false;
    return SNIReadAsync(pConn->m_pConn, ppNewPacket, NULL);
}

DWORD __cdecl SNIClientCertificateFallbackWrapper(
    __in PVOID pCallbackContext,
    __in BOOL fHash,
    __in_z LPCWSTR pszCertificate,
    __out PCCERT_CONTEXT * ppCertContext,
    __out DWORD *pdwFlags,
    __out ULONG cchKeyContainer,
    __out_ecount(cchKeyContainer) WCHAR *pwchKeyContainer
)
{
    *pwchKeyContainer = 0;
    *ppCertContext = NULL;

    SNIAuthProviderInfoWrapper *pWrapperContext = reinterpret_cast<SNIAuthProviderInfoWrapper *>(pCallbackContext);

    *ppCertContext = reinterpret_cast<PCCERT_CONTEXT>((*pWrapperContext->pSqlClientCertificateDelegate)(pWrapperContext->pDelegateContext));
    if (*ppCertContext != NULL)
    {
        *ppCertContext = CertDuplicateCertificateContext(*ppCertContext);
    }

    return *ppCertContext != NULL ? ERROR_SUCCESS : CRYPT_E_NOT_FOUND;
}
//DWORD __cdecl SNIReadSyncOverAsync(SafeHandle^  pConn,  System::IntPtr packet, Int32 timeout) {
//
//    SNI_Packet*  local_packet = NULL;
//    System::UInt32 ret;
//
//    // provides a guaranteed finally block – without this it isn’t guaranteed – non interruptable by fatal exceptions
//    bool mustRelease = false;
//    RuntimeHelpers::PrepareConstrainedRegions();
//    __try
//    {
//        pConn->DangerousAddRef(mustRelease);
//        Debug::Assert(mustRelease, "AddRef Failed!");
//        SNI_ConnWrapper*  local_pConn = static_cast<SNI_ConnWrapper*>(pConn->DangerousGetHandle().ToPointer());
//        // Need to call SyncOverAsync via PInvoke (instead of a pointer) such that the CLR notifies our hoster (e.g. SQLCLR) that we are doing a managed\native transition
//        ret = ::SNIReadSyncOverAsync(local_pConn, &local_packet, timeout);
//    }
//    __finally
//    {
//        if (mustRelease)
//        {
//            pConn->DangerousRelease();
//        }
//    }
//    packet = static_cast<System::IntPtr>(local_packet);
//    return ret;
//}

DWORD __cdecl SNIReadSyncOverAsync(__inout SNI_ConnWrapper * pConn, __out SNI_Packet ** ppNewPacket, int timeout)
{
    *ppNewPacket = NULL;

    if (pConn->m_fSupportsSyncOverAsync)
    {
        return SNIReadSync(pConn->m_pConn, ppNewPacket, timeout);
    }

    ::EnterCriticalSection(&pConn->m_ReadLock);
    DWORD dwError;
    if (!pConn->m_fPendingRead)
    {
        pConn->m_fSyncOverAsyncRead = true;
        pConn->m_fPendingRead = true;
        dwError = SNIReadAsync(pConn->m_pConn, ppNewPacket, NULL);
        assert((*ppNewPacket == NULL && dwError != ERROR_SUCCESS) || (*ppNewPacket != NULL && dwError == ERROR_SUCCESS));
    }
    else
    {
        assert(pConn->m_fSyncOverAsyncRead); // should be syncOverAsync from last call to SNIReadSyncOverAsync
        dwError = ERROR_IO_PENDING;
    }

    if (ERROR_IO_PENDING == dwError)
    {
        dwError = ::WaitForSingleObject(pConn->m_ReadResponseReady, timeout);

        if (dwError == ERROR_TIMEOUT)
        {
            // treat ERROR_TIMEOUT as WAIT_TIMEOUT as that is what is expected by callers
            dwError = WAIT_TIMEOUT;
        }
        else if (ERROR_SUCCESS == dwError)
        {
            pConn->m_fPendingRead = false;
            *ppNewPacket = pConn->m_pPacket;
            pConn->m_pPacket = NULL;
            if (pConn->m_Error.dwNativeError != ERROR_SUCCESS)
            {
                SNISetLastError(pConn->m_Error.Provider, pConn->m_Error.dwNativeError, pConn->m_Error.dwSNIError, NULL, NULL, 0);
            }
            dwError = pConn->m_Error.dwNativeError;
            assert((*ppNewPacket == NULL && dwError != ERROR_SUCCESS) || (*ppNewPacket != NULL && dwError == ERROR_SUCCESS));
        }
        else
        {
            SNISetLastError(INVALID_PROV, dwError, SNIE_SYSTEM, NULL, NULL, 0);
        }
    }
    else if (dwError == ERROR_SUCCESS)
    {
        pConn->m_fPendingRead = false;
    }

    ::LeaveCriticalSection(&pConn->m_ReadLock);

    assert((*ppNewPacket == NULL && dwError != ERROR_SUCCESS) || (*ppNewPacket != NULL && dwError == ERROR_SUCCESS));
    return dwError;
}

void __stdcall UnmanagedReadCallback(LPVOID ConsKey, SNI_Packet * pPacket, DWORD dwError)
{
    SNI_ConnWrapper* pConn = (SNI_ConnWrapper*)ConsKey;

    if (!pConn->m_fSyncOverAsyncRead)
    {
        pConn->m_fnReadComp(pConn->m_ConsumerKey, pPacket, dwError);
    }
    else
    {
        if (dwError == ERROR_SUCCESS)
        {
            SNIPacketAddRef(pPacket);
            pConn->m_pPacket = pPacket;
            pConn->m_Error.dwNativeError = ERROR_SUCCESS;
        }
        else
        {
            pConn->m_pPacket = NULL;
            SNIGetLastError(&pConn->m_Error);
            // SNIGetLastError strips SNI_STRING_ERROR_BASE out of the code
            pConn->m_Error.dwSNIError += SNI_STRING_ERROR_BASE;
            assert(pConn->m_Error.dwNativeError != ERROR_SUCCESS);
        }

        ::ReleaseSemaphore(pConn->m_ReadResponseReady, 1, NULL);
    }
}

void __stdcall UnmanagedWriteCallback(LPVOID ConsKey, SNI_Packet * pPacket, DWORD dwError)
{
    SNI_ConnWrapper* pConn = (SNI_ConnWrapper*)ConsKey;

    if (!pConn->m_fSyncOverAsyncWrite)
    {
        pConn->m_fnWriteComp(pConn->m_ConsumerKey, pPacket, dwError);
    }
    else
    {
        if (dwError == ERROR_SUCCESS)
        {
            pConn->m_WriteError.dwNativeError = ERROR_SUCCESS;
        }
        else
        {
            SNIGetLastError(&pConn->m_WriteError);
            // SNIGetLastError strips SNI_STRING_ERROR_BASE out of the code
            pConn->m_WriteError.dwSNIError += SNI_STRING_ERROR_BASE;
            assert(pConn->m_WriteError.dwNativeError != ERROR_SUCCESS);
        }

        ::ReleaseSemaphore(pConn->m_WriteResponseReady, 1, NULL);
    }
}


DWORD __cdecl SNIOpenWrapper(
    __in SNI_CONSUMER_INFO * pConsumerInfo,
    __inout_opt LPWSTR szConnect,
    __in SNI_ConnWrapper * pConn,
    __out SNI_ConnWrapper ** ppConn,
    __in BOOL fSync)
{
    SNI_ConnWrapper* pConnWrapper = NULL;
    SNI_Conn* pNewConn = NULL;
    DWORD dwError = ERROR_SUCCESS;

    pConnWrapper = new SNI_ConnWrapper(pConsumerInfo);
    if (pConnWrapper == NULL)
    {
        dwError = ERROR_OUTOFMEMORY;
        SNISetLastError(INVALID_PROV, dwError, SNIE_SYSTEM, NULL, NULL, 0);
        goto ErrorExit;
    }

    pConsumerInfo->fnReadComp = UnmanagedReadCallback;
    pConsumerInfo->fnWriteComp = UnmanagedWriteCallback;
    pConsumerInfo->ConsumerKey = pConnWrapper;

    dwError = SNIOpen(pConsumerInfo, szConnect, pConn->m_pConn, &pNewConn, fSync);
    if (dwError != ERROR_SUCCESS)
    {
        goto ErrorExit;
    }

    pConnWrapper->m_pConn = pNewConn;

    BOOL fSupportsSyncOverAsync;
    dwError = SNIGetInfo(pNewConn, SNI_QUERY_CONN_SUPPORTS_SYNC_OVER_ASYNC, &fSupportsSyncOverAsync);
    assert(dwError == ERROR_SUCCESS); // SNIGetInfo cannot fail with this QType

    // convert BOOL to bool
    pConnWrapper->m_fSupportsSyncOverAsync = !!fSupportsSyncOverAsync;

    *ppConn = pConnWrapper;
    return ERROR_SUCCESS;

ErrorExit:
    if (pConnWrapper)
    {
        delete pConnWrapper;
    }
    return dwError;
}

DWORD __cdecl SNIOpenSyncExWrapper(__inout SNI_CLIENT_CONSUMER_INFO * pClientConsumerInfo, __deref_out SNI_ConnWrapper ** ppConn)
{
    SNI_ConnWrapper* pConnWrapper = NULL;
    SNI_Conn* pConn = NULL;
    DWORD dwError = ERROR_SUCCESS;

    pConnWrapper = new SNI_ConnWrapper(&pClientConsumerInfo->ConsumerInfo);
    if (pConnWrapper == NULL)
    {
        dwError = ERROR_OUTOFMEMORY;
        SNISetLastError(INVALID_PROV, dwError, SNIE_SYSTEM, NULL, NULL, 0);
        goto ErrorExit;
    }

    pClientConsumerInfo->ConsumerInfo.fnReadComp = UnmanagedReadCallback;
    pClientConsumerInfo->ConsumerInfo.fnWriteComp = UnmanagedWriteCallback;
    pClientConsumerInfo->ConsumerInfo.ConsumerKey = pConnWrapper;

    dwError = SNIOpenSyncEx(pClientConsumerInfo, &pConn);
    if (dwError != ERROR_SUCCESS)
    {
        goto ErrorExit;
    }

    pConnWrapper->m_pConn = pConn;

    BOOL fSupportsSyncOverAsync;
    dwError = SNIGetInfo(pConn, SNI_QUERY_CONN_SUPPORTS_SYNC_OVER_ASYNC, &fSupportsSyncOverAsync);
    assert(dwError == ERROR_SUCCESS); // SNIGetInfo cannot fail with this QType

    // convert BOOL to bool
    pConnWrapper->m_fSupportsSyncOverAsync = !!fSupportsSyncOverAsync;

    *ppConn = pConnWrapper;
    return ERROR_SUCCESS;

ErrorExit:
    if (pConnWrapper)
    {
        delete pConnWrapper;
    }
    return dwError;
}

DWORD __cdecl SNICloseWrapper(__inout SNI_ConnWrapper * pConn)
{
    DWORD dwError = SNIClose(pConn->m_pConn);
    delete pConn;
    return dwError;
}

DWORD __cdecl SNIGetInfoWrapper(__in SNI_ConnWrapper * pConn, UINT QType, __out VOID * pbQInfo)
{
    return SNIGetInfo(pConn->m_pConn, QType, pbQInfo);
}

DWORD __cdecl SNISetInfoWrapper(__out SNI_ConnWrapper * pConn, UINT QType, __in VOID * pbQInfo)
{
    return SNISetInfo(pConn->m_pConn, QType, pbQInfo);
}

DWORD __cdecl SNIAddProviderWrapper(__inout SNI_ConnWrapper * pConn, ProviderNum ProvNum, __in LPVOID pInfo)
{
    return SNIAddProvider(pConn->m_pConn, ProvNum, pInfo);
}

DWORD __cdecl SNIRemoveProviderWrapper(__inout SNI_ConnWrapper * pConn, ProviderNum ProvNum)
{
    return SNIRemoveProvider(pConn->m_pConn, ProvNum);
}


DWORD __cdecl SNIWaitForSSLHandshakeToCompleteWrapper(__in SNI_ConnWrapper * pConn, DWORD dwMilliseconds)
{
    return SNIWaitForSSLHandshakeToComplete(pConn->m_pConn, dwMilliseconds);
}

DWORD __cdecl SNICheckConnectionWrapper(__in SNI_ConnWrapper* pConn)
{
    return SNICheckConnection(pConn->m_pConn);
}

DWORD __cdecl SNISecGenClientContextWrapper(
    __in SNI_ConnWrapper * pConn,
    BYTE    *pIn,
    DWORD   cbIn,
    BYTE    *pOut,
    __in DWORD  *pcbOut,
    BOOL    *pfDone,
    __in __nullterminated const WCHAR    *szServerInfo,
    DWORD    cbServerInfo,
    LPCWSTR pwszUserName,
    LPCWSTR pwszPassword)
{
    return SNISecGenClientContext(pConn->m_pConn, pIn, cbIn, pOut, pcbOut, pfDone, szServerInfo, cbServerInfo, pwszUserName, pwszPassword);
}

DWORD __cdecl UnmanagedIsTokenRestricted(__in HANDLE token, __out BOOL *isRestricted)
{
    ::SetLastError(0);

    *isRestricted = ::IsTokenRestricted(token); // calls into win32 API
    return ::GetLastError();
}

void __cdecl SNIPacketResetWrapper(SNI_ConnWrapper * pConn, SNI_Packet_IOType IOType, __out SNI_Packet * pPacket, ConsumerNum ConsNum)
{
    SNIPacketReset(pConn->m_pConn, IOType, pPacket, ConsNum);
}

DWORD __cdecl SNIPacketGetDataWrapper(__in SNI_Packet * packet, __out_bcount_part(*dataSize, readBufferLength) BYTE * readBuffer, __in DWORD readBufferLength, __out DWORD * dataSize)
{
    BYTE* byteData = NULL;
    SNIPacketGetData(packet, &byteData, dataSize);
    DWORD dwError = memcpy_s(readBuffer, readBufferLength, byteData, *dataSize);
    return dwError;
}