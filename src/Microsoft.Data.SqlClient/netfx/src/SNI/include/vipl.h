//*********************************************************************
//		Copyright (c) Microsoft Corporation.
//
// @File: vipl.h
// @Owner: nantu, petergv
// @Test: milu
//
// <owner current="true" primary="true">nantu</owner>
// <owner current="true" primary="false">petergv</owner>
//
// Purpose: 
//		Header for VIA provider specific structures 
//
// Notes:
//	
// @EndHeader@
//****************************************************************************	

//
// Copyright (C) 1998 Giganet Incorporated
// All rights reserved
//
// This file represents information contained the Virtual Interface Specification Revision 1.0
// as ported to Windows NT 4.0
//
#ifndef _VIPL_H
#define _VIPL_H

#ifndef IN
#define IN
#endif
#ifndef OUT
#define OUT
#endif

//
// VIA types
//
typedef unsigned __int64 VIP_UINT64;
typedef unsigned __int32 VIP_UINT32;
typedef unsigned __int16 VIP_UINT16;
typedef unsigned __int8 VIP_UINT8;

typedef unsigned char VIP_UCHAR;
typedef char VIP_CHAR;
typedef WCHAR VIP_WCHAR;

typedef unsigned short VIP_USHORT;
typedef short VIP_SHORT;

typedef unsigned long VIP_ULONG;
typedef long VIP_LONG;

typedef int VIP_BOOLEAN;
typedef void *VIP_PVOID;
typedef void *VIP_EVENT_HANDLE;

#define VIP_TRUE	1
#define VIP_FALSE 0

//
// handle types
//
typedef VIP_PVOID VIP_QOS;
typedef VIP_PVOID VIP_NIC_HANDLE;
typedef VIP_PVOID VIP_VI_HANDLE;
typedef VIP_PVOID VIP_CQ_HANDLE;
typedef VIP_PVOID VIP_PROTECTION_HANDLE;
typedef VIP_UINT32 VIP_MEM_HANDLE;
typedef VIP_PVOID VIP_CONN_HANDLE;

//
// infinite timeout
//
#define VIP_INFINITE INFINITE

#ifdef USE_FALCON_DESCRIPTOR_FORMAT

typedef FALCON_DESCRIPTOR VIP_DESCRIPTOR;

#else
 
//
// VIA descriptors
//
struct _VIP_DESCRIPTOR;

//
// VIA 64 bit address format
//
typedef volatile union {
    VIP_UINT64 AddressBits;
    VIP_PVOID Address;
    struct _VIP_DESCRIPTOR *Descriptor;
} VIP_PVOID64;

//
// control segment format
//
typedef volatile struct {
    VIP_PVOID64 Next;
    VIP_MEM_HANDLE NextHandle;
    VIP_UINT16 SegCount;
    VIP_UINT16 Control;

    VIP_UINT32 Reserved;
    VIP_UINT32 ImmediateData;
    VIP_UINT32 Length;
    VIP_UINT32 Status;
} VIP_CONTROL_SEGMENT;

//
// control field
//
#define VIP_CONTROL_OP_SENDRECV         0x0
#define VIP_CONTROL_OP_RDMAWRITE        0x1
#define VIP_CONTROL_OP_RDMAREAD         0x2
#define VIP_CONTROL_OP_RESERVED         0x3
#define VIP_CONTROL_OP_MASK             0x3
#define VIP_CONTROL_IMMEDIATE           0x4
#define VIP_CONTROL_QFENCE              0x8
#define VIP_CONTROL_RESERVED            0xfff0

//
// status field
//
#define VIP_STATUS_DONE                 0x1
#define VIP_STATUS_FORMAT_ERROR         0x2
#define VIP_STATUS_PROTECTION_ERROR     0x4
#define VIP_STATUS_LENGTH_ERROR         0x8
#define VIP_STATUS_PARTIAL_ERROR        0x10
#define VIP_STATUS_DESC_FLUSHED_ERROR   0x20
#define VIP_STATUS_TRANSPORT_ERROR      0x40
#define VIP_STATUS_RDMA_PROT_ERROR      0x80
#define VIP_STATUS_REMOTE_DESC_ERROR    0x100
#define VIP_STATUS_ERROR_MASK           0x1fe

#define VIP_STATUS_OP_SEND              0x00000
#define VIP_STATUS_OP_RECEIVE           0x10000
#define VIP_STATUS_OP_RDMA_WRITE        0x20000
#define VIP_STATUS_OP_REMOTE_RDMA_WRITE 0x30000
#define VIP_STATUS_OP_RDMA_READ         0x40000
#define VIP_STATUS_OP_MASK              0x70000
#define VIP_STATUS_IMMEDIATE            0x80000

#define VIP_STATUS_RESERVED             0xFFF0FE00

//
// address segment format
//
typedef volatile struct {
    VIP_PVOID64 Data;
    VIP_MEM_HANDLE Handle;
    VIP_UINT32 Reserved;
} VIP_ADDRESS_SEGMENT;

//
// data segment format
//
typedef volatile struct {
    VIP_PVOID64 Data;
    VIP_MEM_HANDLE Handle;
    VIP_UINT32 Length;
} VIP_DATA_SEGMENT;

#ifdef VIPL095

typedef union {
    VIP_ADDRESS_SEGMENT Remote;
    VIP_DATA_SEGMENT Local;
} VIP_DESCRIPTOR_SEGMENT;

//
// VIA descriptor format
//
typedef struct _VIP_DESCRIPTOR {
    VIP_CONTROL_SEGMENT CS;
    VIP_DESCRIPTOR_SEGMENT DS[2];
} VIP_DESCRIPTOR;

#else

//
// VIA descriptor format
//
typedef struct _VIP_DESCRIPTOR {
    VIP_CONTROL_SEGMENT Control;
    VIP_DATA_SEGMENT Data[1];
} VIP_DESCRIPTOR;

#endif
#endif

//
// descriptor alignment
//
#define VIP_DESCRIPTOR_ALIGNMENT 64

#ifndef SNI_BASED_CLIENT
#ifndef DLLEXPORT
#define DLLEXPORT __declspec(dllexport)
#endif
#else
#ifdef SNIX
#define DLLEXPORT __declspec(dllexport)
#else
#define DLLEXPORT
#endif
#endif

//
// API return codes
//
typedef enum {
    VIP_SUCCESS,
    VIP_NOT_DONE,
    VIP_INVALID_PARAMETER,
    VIP_ERROR_RESOURCE,

    VIP_TIMEOUT,
    VIP_REJECT,
    VIP_INVALID_RELIABILITY_LEVEL,
    VIP_INVALID_MTU,

    VIP_INVALID_QOS,
    VIP_INVALID_PTAG,
    VIP_INVALID_RDMAREAD,
    VIP_DESCRIPTOR_ERROR,

    VIP_INVALID_STATE,
    VIP_ERROR_NAMESERVICE,
    VIP_NO_MATCH,
    VIP_NOT_REACHABLE,

    VIP_ERROR_NOT_SUPPORTED,

    VIP_ERROR
} VIP_RETURN;

typedef VIP_USHORT VIP_RELIABILITY_LEVEL;

//
// VI reliability levels
//
#define VIP_SERVICE_UNRELIABLE 1
#define VIP_SERVICE_RELIABLE_DELIVERY 2
#define VIP_SERVICE_RELIABLE_RECEPTION 4
#define VIP_SERVICE_DFC (1<<15)

//
// Network address formats
//
typedef struct _VIP_NET_ADDRESS {
    VIP_UINT16 HostAddressLen;
    VIP_UINT16 DiscriminatorLen;
    VIP_UINT8 HostAddress[1];
} VIP_NET_ADDRESS;

//
// NIC attributes
//
typedef struct _VIP_NIC_ATTRIBUTES {
    VIP_CHAR Name[64];
    VIP_ULONG HardwareVersion;
    VIP_ULONG ProviderVersion;
    VIP_UINT16 NicAddressLen;
    const VIP_UINT8 *LocalNicAddress;
    VIP_BOOLEAN ThreadSafe;
    VIP_UINT16 MaxDiscriminatorLen;
    VIP_ULONG MaxRegisterBytes;
    VIP_ULONG MaxRegisterRegions;
    VIP_ULONG MaxRegisterBlockBytes;
    VIP_ULONG MaxVI;
    VIP_ULONG MaxDescriptorsPerQueue;
    VIP_ULONG MaxSegmentsPerDesc;
    VIP_ULONG MaxCQ;
    VIP_ULONG MaxCQEntries;
    VIP_ULONG MaxTransferSize;
    VIP_ULONG NativeMTU;
    VIP_ULONG MaxPtags;
    VIP_RELIABILITY_LEVEL ReliabilityLevelSupport;
    VIP_RELIABILITY_LEVEL RDMAReadSupport;
} VIP_NIC_ATTRIBUTES;

//
// Memory attributes
//
typedef struct _VIP_MEM_ATTRIBUTES {
    VIP_PROTECTION_HANDLE Ptag;
    VIP_BOOLEAN EnableRdmaWrite;
    VIP_BOOLEAN EnableRdmaRead;
} VIP_MEM_ATTRIBUTES;

typedef enum _VIP_RESOURCE_CODE {
    VIP_RESOURCE_NIC,
    VIP_RESOURCE_VI,
    VIP_RESOURCE_CQ,
    VIP_RESOURCE_DESCRIPTOR,
} VIP_RESOURCE_CODE;

typedef enum _VIP_ERROR_CODE {
    VIP_ERROR_POST_DESC,
    VIP_ERROR_CONN_LOST,
    VIP_ERROR_RECVQ_EMPTY,
    VIP_ERROR_VI_OVERRUN,
    VIP_ERROR_RDMAW_PROT,
    VIP_ERROR_RDMAW_DATA,
    VIP_ERROR_RDMAW_ABORT,
    VIP_ERROR_RDMAR_PROT,
    VIP_ERROR_COMP_PROT,
    VIP_ERROR_RDMA_TRANSPORT,
    VIP_ERROR_CATASTROPHIC,
} VIP_ERROR_CODE;

typedef struct _VIP_ERROR_DESCRIPTOR {
    VIP_NIC_HANDLE NicHandle;
    VIP_VI_HANDLE ViHandle;
    VIP_CQ_HANDLE CQHandle;
    VIP_DESCRIPTOR *DescriptorPtr;
    VIP_ULONG OpCode;
    VIP_RESOURCE_CODE ResourceCode;
    VIP_ERROR_CODE ErrorCode;
} VIP_ERROR_DESCRIPTOR;

//
// VI states
//
typedef enum {
    VIP_STATE_IDLE,
    VIP_STATE_CONNECTED,
    VIP_STATE_CONNECT_PENDING,
    VIP_STATE_ERROR,
} VIP_VI_STATE;

//
// VI attributes
//
typedef struct _VIP_VI_ATTRIBUTES {
    VIP_RELIABILITY_LEVEL ReliabilityLevel;
    VIP_ULONG MaxTransferSize;
    VIP_QOS QoS;
    VIP_PROTECTION_HANDLE Ptag;
    VIP_BOOLEAN EnableRdmaWrite;
    VIP_BOOLEAN EnableRdmaRead;
} VIP_VI_ATTRIBUTES;

#define VIP_SMI_AUTODISCOVERY ((VIP_ULONG) 1)

typedef struct {
    VIP_ULONG NumberOfHops;
    VIP_NET_ADDRESS *ADAddrArray;
    VIP_ULONG NumAdAddrs;
} VIP_AUTODISCOVERY_LIST;


#if __cplusplus
extern "C" {
#endif

//
// NIC primitives
//
DLLEXPORT
VIP_RETURN __cdecl VipOpenNic(
    IN const VIP_CHAR *DeviceName,
    OUT VIP_NIC_HANDLE *NicHandle);

DLLEXPORT
VIP_RETURN __cdecl VipCloseNic(
    IN VIP_NIC_HANDLE NicHandle);

DLLEXPORT
VIP_RETURN __cdecl VipQueryNic(
    IN VIP_NIC_HANDLE NicHandle,
    OUT VIP_NIC_ATTRIBUTES *Attributes);

DLLEXPORT
VIP_RETURN __cdecl VipRegisterMem(
    IN VIP_NIC_HANDLE NicHandle,
    IN VIP_PVOID VirtualAddress,
    IN VIP_ULONG Length,
    IN VIP_MEM_ATTRIBUTES *MemAttributes,
    OUT VIP_MEM_HANDLE *MemHandle);

DLLEXPORT
VIP_RETURN __cdecl VipDeregisterMem(
    IN VIP_NIC_HANDLE NicHandle,
    IN VIP_PVOID VirtualAddress,
    IN VIP_MEM_HANDLE MemHandle);

DLLEXPORT
VIP_RETURN __cdecl VipQueryMem(
    IN VIP_NIC_HANDLE NicHandle,
    IN VIP_PVOID VirtualAddress,
    IN VIP_MEM_HANDLE MemHandle,
    OUT VIP_MEM_ATTRIBUTES *MemAttributes);

DLLEXPORT
VIP_RETURN __cdecl VipSetMemAttributes(
    IN VIP_NIC_HANDLE NicHandle,
    IN VIP_PVOID VirtualAddress,
    IN VIP_MEM_HANDLE MemHandle,
    IN VIP_MEM_ATTRIBUTES *MemAttributes);

typedef void (*VIP_ERROR_HANDLER)(VIP_PVOID, VIP_ERROR_DESCRIPTOR *);

DLLEXPORT
VIP_RETURN __cdecl VipErrorCallback(
    IN VIP_NIC_HANDLE NicHandle,
    IN VIP_PVOID Context,
    IN VIP_ERROR_HANDLER ErrorHandler);

//
// management
//
DLLEXPORT
VIP_RETURN __cdecl VipQuerySystemManagementInfo(
    IN VIP_NIC_HANDLE NicHandle,
    IN VIP_ULONG InfoType,
    OUT VIP_PVOID SysManInfo);

//
// Protection tags
//
DLLEXPORT
VIP_RETURN __cdecl VipCreatePtag(
    IN VIP_NIC_HANDLE NicHandle,
    OUT VIP_PROTECTION_HANDLE *ProtectionTag);

DLLEXPORT
VIP_RETURN __cdecl VipDestroyPtag(
    IN VIP_NIC_HANDLE NicHandle,
    IN VIP_PROTECTION_HANDLE ProtectionTag);

//
// VI primitives
//
DLLEXPORT
VIP_RETURN __cdecl VipCreateVi(
    IN VIP_NIC_HANDLE NicHandle,
    IN VIP_VI_ATTRIBUTES *ViAttributes,
    IN VIP_CQ_HANDLE SendCQHandle,
    IN VIP_CQ_HANDLE RecvCQHandle,
    OUT VIP_VI_HANDLE *ViHandle);

DLLEXPORT
VIP_RETURN __cdecl VipDestroyVi(
    IN VIP_VI_HANDLE ViHandle);

DLLEXPORT
VIP_RETURN __cdecl VipQueryVi(
    IN VIP_VI_HANDLE ViHandle,
    OUT VIP_VI_STATE *State,
    OUT VIP_VI_ATTRIBUTES *Attributes,
    OUT VIP_BOOLEAN *SendQueueEmpty,
    OUT VIP_BOOLEAN *RecvQueueEmpty);

DLLEXPORT
VIP_RETURN __cdecl VipSetViAttributes(
    IN VIP_VI_HANDLE ViHandle,
    IN VIP_VI_ATTRIBUTES *Attributes);

DLLEXPORT
VIP_RETURN __cdecl VipPostSend(
    IN VIP_VI_HANDLE ViHandle,
    IN VIP_DESCRIPTOR *DescriptorPtr,
    IN VIP_MEM_HANDLE MemoryHandle);

DLLEXPORT
VIP_RETURN __cdecl VipSendDone(
    IN VIP_VI_HANDLE ViHandle,
    OUT VIP_DESCRIPTOR **DescriptorPtr);

DLLEXPORT
VIP_RETURN __cdecl VipSendWait(
    IN VIP_VI_HANDLE ViHandle,
    IN VIP_ULONG TimeOut,
    OUT VIP_DESCRIPTOR **DescriptorPtr);

typedef void (*VIP_VI_CALLBACK)(
    VIP_PVOID Context, VIP_NIC_HANDLE NicHandle, VIP_VI_HANDLE ViHandle, VIP_DESCRIPTOR *Descriptor);

DLLEXPORT
VIP_RETURN __cdecl VipSendNotify(
    IN VIP_VI_HANDLE ViHandle,
    IN VIP_PVOID Context,
    IN VIP_VI_CALLBACK Callback);

DLLEXPORT
VIP_RETURN __cdecl VipPostRecv(
    IN VIP_VI_HANDLE ViHandle,
    IN VIP_DESCRIPTOR *DescriptorPtr,
    IN VIP_MEM_HANDLE MemoryHandle);

DLLEXPORT
VIP_RETURN __cdecl VipRecvDone(
    IN VIP_VI_HANDLE ViHandle,
    OUT VIP_DESCRIPTOR **DescriptorPtr);

DLLEXPORT
VIP_RETURN __cdecl VipRecvWait(
    IN VIP_VI_HANDLE ViHandle,
    IN VIP_ULONG TimeOut,
    OUT VIP_DESCRIPTOR **DescriptorPtr);

DLLEXPORT
VIP_RETURN __cdecl VipRecvNotify(
    IN VIP_VI_HANDLE ViHandle,
    IN VIP_PVOID Context,
    IN VIP_VI_CALLBACK Callback);

DLLEXPORT
VIP_RETURN __cdecl VipConnectWait(
    IN VIP_NIC_HANDLE NicHandle,
    IN VIP_NET_ADDRESS *LocalAddr,
    IN VIP_ULONG Timeout,
    OUT VIP_NET_ADDRESS *RemoteAddr,
    OUT VIP_VI_ATTRIBUTES *RemoteViAttributes,
    OUT VIP_CONN_HANDLE *ConnHandle);

DLLEXPORT
VIP_RETURN __cdecl VipConnectAccept(
    IN VIP_CONN_HANDLE ConnHandle,
    IN VIP_VI_HANDLE ViHandle);

DLLEXPORT
VIP_RETURN __cdecl VipConnectReject(
    IN VIP_CONN_HANDLE ConnHandle);

DLLEXPORT
VIP_RETURN __cdecl VipConnectRequest(
    IN VIP_VI_HANDLE ViHandle,
    IN VIP_NET_ADDRESS *LocalAddr,
    IN VIP_NET_ADDRESS *RemoteAddr,
    IN VIP_ULONG Timeout,
    OUT VIP_VI_ATTRIBUTES *RemoteViAttributes);

DLLEXPORT
VIP_RETURN __cdecl VipDisconnect(
    IN VIP_VI_HANDLE ViHandle);

// 
// Completion Queue primitives
//
DLLEXPORT
VIP_RETURN __cdecl VipCreateCQ(
    IN VIP_NIC_HANDLE NicHandle,
    IN VIP_ULONG EntryCount,
    OUT VIP_CQ_HANDLE *CQHandle);

DLLEXPORT
VIP_RETURN __cdecl VipDestroyCQ(
    IN VIP_CQ_HANDLE CQHandle);

DLLEXPORT
VIP_RETURN __cdecl VipResizeCQ(
    IN VIP_CQ_HANDLE CQHandle,
    IN VIP_ULONG EntryCount);

DLLEXPORT
VIP_RETURN __cdecl VipCQDone(
    IN VIP_CQ_HANDLE CQHandle,
    OUT VIP_VI_HANDLE *ViHandle,
    OUT VIP_BOOLEAN *RecvQueue);

DLLEXPORT
VIP_RETURN __cdecl VipCQWait(
    IN VIP_CQ_HANDLE CQHandle,
    IN VIP_ULONG Timeout,
    OUT VIP_VI_HANDLE *ViHandle,
    OUT VIP_BOOLEAN *RecvQueue);

typedef void (*VIP_CQ_CALLBACK)(
    VIP_PVOID Context, VIP_NIC_HANDLE NicHandle, VIP_VI_HANDLE ViHandle, VIP_BOOLEAN RecvQueue);

DLLEXPORT
VIP_RETURN __cdecl VipCQNotify(
    IN VIP_CQ_HANDLE CqHandle,
    IN VIP_PVOID Context,
    IN VIP_CQ_CALLBACK Callback);

//
// name service API
//
DLLEXPORT
VIP_RETURN __cdecl VipNSInit(
    IN VIP_NIC_HANDLE NicHandle,
    IN VIP_PVOID NSInitInfo);

DLLEXPORT
VIP_RETURN __cdecl VipNSGetHostByName(
    IN VIP_NIC_HANDLE NicHandle,
    IN VIP_CHAR *Name,
    OUT VIP_NET_ADDRESS *Address,
    IN VIP_ULONG NameIndex);

DLLEXPORT
VIP_RETURN __cdecl VipNSGetHostByAddr(
    IN VIP_NIC_HANDLE NicHandle,
    IN VIP_NET_ADDRESS *Address,
    OUT VIP_CHAR *Name,
    IN OUT VIP_ULONG *NameLen);

DLLEXPORT
VIP_RETURN __cdecl VipNSShutdown(
    IN VIP_NIC_HANDLE NicHandle);

//
// peer connection API
//
DLLEXPORT
VIP_RETURN __cdecl VipConnectPeerRequest(
    IN VIP_VI_HANDLE ViHandle,
    IN VIP_NET_ADDRESS *LocalAddr,
    IN VIP_NET_ADDRESS *RemoteAddr,
    IN VIP_ULONG Timeout);

DLLEXPORT
VIP_RETURN __cdecl VipConnectPeerDone(
    IN VIP_VI_HANDLE ViHandle,
    OUT VIP_VI_ATTRIBUTES *RemoteAttributes);

DLLEXPORT
VIP_RETURN __cdecl VipConnectPeerWait(
    IN VIP_VI_HANDLE ViHandle,
    OUT VIP_VI_ATTRIBUTES *RemoteViAttributes);

//
// Tag demultiplexing
//
DLLEXPORT
VIP_RETURN __cdecl VipAddTagCQ(
	IN VIP_CQ_HANDLE CQHandle,
	IN OUT VIP_EVENT_HANDLE *Event,
	IN VIP_ULONG Tag,
	IN VIP_ULONG Priority);

DLLEXPORT
VIP_RETURN __cdecl VipRemoveTagCQ(
	IN VIP_CQ_HANDLE CQHandle,
	IN VIP_EVENT_HANDLE Event,
	IN VIP_ULONG Tag);

//
// deferred function call
//
DLLEXPORT
VIP_RETURN __cdecl VipPostDeferredSends(
	IN VIP_VI_HANDLE vihandle, 
	IN VIP_BOOLEAN enableinterrupt,
	IN OUT VIP_BOOLEAN *sendsdeferred);


#if __cplusplus
};
#endif

#endif
