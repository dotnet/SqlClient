// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#ifndef _SNI_WRAPPER_HPP_
#define _SNI_WRAPPER_HPP_

#include <windows.h>
#include "sni.hpp"
#include <assert.h>
#include <string.h>

#define DLLEXPORT __declspec(dllexport)

extern "C" DLLEXPORT DWORD __cdecl GetSniMaxComposedSpnLength()
{
    return SNI_MAX_COMPOSED_SPN;
}

struct SNI_ConnWrapper
{
    SNI_ConnWrapper(__in SNI_CONSUMER_INFO * pConsumerInfo) :
        m_pConn(NULL),
        m_fnReadComp(pConsumerInfo->fnReadComp),
        m_fnWriteComp(pConsumerInfo->fnWriteComp),
        m_ConsumerKey(pConsumerInfo->ConsumerKey),
        m_pPacket(NULL),
        m_fSyncOverAsyncRead(false),
        m_fSyncOverAsyncWrite(false),
        m_fSupportsSyncOverAsync(false),
        m_fPendingRead(false)
    {
        m_ReadResponseReady = ::CreateSemaphore(NULL, 0, 1, NULL);
        m_WriteResponseReady = ::CreateSemaphore(NULL, 0, 1, NULL);
        ::InitializeCriticalSection(&m_ReadLock);
    }

    ~SNI_ConnWrapper()
    {
        CloseHandle(m_ReadResponseReady);
        CloseHandle(m_WriteResponseReady);
        DeleteCriticalSection(&m_ReadLock);
    }

    SNI_Conn* m_pConn;
    PIOCOMP_FN m_fnReadComp;
    PIOCOMP_FN m_fnWriteComp;
    LPVOID m_ConsumerKey;

    CRITICAL_SECTION m_ReadLock;
    HANDLE m_ReadResponseReady;
    HANDLE m_WriteResponseReady;
    bool m_fPendingRead;

    SNI_Packet *m_pPacket;
    SNI_ERROR m_WriteError;
    SNI_ERROR m_Error;
    bool m_fSyncOverAsyncRead;
    bool m_fSyncOverAsyncWrite;
    bool m_fSupportsSyncOverAsync;
};

extern "C" DLLEXPORT DWORD __cdecl SNIOpenWrapper(__in SNI_CONSUMER_INFO * pConsumerInfo,
    __inout_opt LPWSTR szConnect,
    __in SNI_ConnWrapper * pConn,
    __out SNI_ConnWrapper ** ppConn,
    __in BOOL fSync);
extern "C" DLLEXPORT DWORD __cdecl SNIOpenSyncExWrapper(__inout SNI_CLIENT_CONSUMER_INFO * pClientConsumerInfo, __deref_out SNI_ConnWrapper ** ppConn);
extern "C" DLLEXPORT DWORD __cdecl SNICloseWrapper(__inout SNI_ConnWrapper * pConn);

extern "C" DLLEXPORT DWORD __cdecl SNIGetInfoWrapper(__in SNI_ConnWrapper * pConn, UINT QType, __out VOID * pbQInfo);
extern "C" DLLEXPORT DWORD __cdecl SNISetInfoWrapper(__out SNI_ConnWrapper * pConn, UINT QType, __in VOID * pbQInfo);

extern "C" DLLEXPORT DWORD __cdecl SNIAddProviderWrapper(__inout SNI_ConnWrapper * pConn, ProviderNum ProvNum, __in LPVOID pInfo);
extern "C" DLLEXPORT DWORD __cdecl SNIRemoveProviderWrapper(__inout SNI_ConnWrapper * pConn, ProviderNum ProvNum);

extern "C" DLLEXPORT DWORD __cdecl SNIWaitForSSLHandshakeToCompleteWrapper(__in SNI_ConnWrapper * pConn, DWORD dwMilliseconds);

extern "C" DLLEXPORT DWORD __cdecl SNICheckConnectionWrapper(__in SNI_ConnWrapper* pConn);
extern "C" DLLEXPORT DWORD __cdecl SNISecGenClientContextWrapper(__in SNI_ConnWrapper * pConn,
    BYTE    *pIn,
    DWORD   cbIn,
    BYTE    *pOut,
    __in DWORD  *pcbOut,
    BOOL    *pfDone,
    __in __nullterminated const WCHAR    *szServerInfo,
    DWORD    cbServerInfo,
    LPCWSTR pwszUserName,
    LPCWSTR pwszPassword);

extern "C" DLLEXPORT SNI_Packet * __cdecl SNIPacketAllocateWrapper(__in SNI_ConnWrapper * pConn, SNI_Packet_IOType IOType);
extern "C" DLLEXPORT DWORD __cdecl SNIWriteAsyncWrapper(__inout SNI_ConnWrapper * pConn, __in SNI_Packet * pPacket);
extern "C" DLLEXPORT DWORD __cdecl SNIWriteSyncOverAsync(__inout SNI_ConnWrapper * pConn, __in SNI_Packet * pPacket);
extern "C" DLLEXPORT DWORD __cdecl SNIReadAsyncWrapper(__inout SNI_ConnWrapper * pConn, __out SNI_Packet ** ppNewPacket);
extern "C" DLLEXPORT DWORD __cdecl SNIReadSyncOverAsync(__inout SNI_ConnWrapper * pConn, __out SNI_Packet ** ppNewPacket, int timeout);

extern "C" DLLEXPORT DWORD __cdecl UnmanagedIsTokenRestricted(__in HANDLE token, __out BOOL *isRestricted);

extern "C" DLLEXPORT void __cdecl SNIPacketResetWrapper(SNI_ConnWrapper * pConn, SNI_Packet_IOType IOType, __out SNI_Packet * pPacket, ConsumerNum ConsNum);
extern "C" DLLEXPORT DWORD __cdecl SNIPacketGetDataWrapper(__in SNI_Packet * packet, __out_bcount_part(*dataSize, readBufferLength) BYTE * readBuffer, __in DWORD readBufferLength, __out DWORD * dataSize);

#endif