/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       CPConversion.cpp
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   CodePage and CHAR/WCHAR conversion wrappers
//
//  Comments:                                               (Reduced version for Bid2Etw28)
//              File Created : 10-Apr-1996
//              Last Modified: 06-Mar-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
/////////////////////////////////////////////////////////////////////////////////////////////////
#include  "stdafx.h"
#include  "yawl/CPConversion.h"

//
//  String pointer validation
//
BOOL _emptyStr(PCVOID pStr)
{
    PCSTR   ptr = (PCSTR)(UINT_PTR)0xBAADF00D;
    BOOL    bEmpty = TRUE;
    __try
    {
        ptr = (PCSTR)pStr;
        bEmpty = (ptr == NULL || *ptr == '\0');
    }
    __except( EXCEPTION_EXECUTE_HANDLER )
    {
        BidTraceU1A( BID_ADV, "<_emptyStr|ADV|AV> %p R/O AV\n", ptr );
    }
    return bEmpty;
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Unicode -> Ansi/MultiByte. Code page mapping.
//
int WINAPI _mbLen(PCWSTR src, UINT dstCP, int srcCnt)
{
    int len = WideCharToMultiByte(dstCP, 0, src, srcCnt, NULL, 0, NULL, NULL);

    //
    //  Now len includes null terminator, but we wanna behavior similar to strlen()
    //
    if( len > 0 ) len--;
    return len;
}

int WINAPI _toMB(PSTR dst, PCWSTR src, int dstCnt, UINT dstCP, int srcCnt)
{
    int len = WideCharToMultiByte(dstCP, 0, src, srcCnt, dst, dstCnt, NULL, NULL);
    if( len > 0 ) len--;
    return len;
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Ansi -> Unicode. Code page mapping.
//
int WINAPI _uniLen(PCSTR src, UINT srcCP, int srcCnt)
{
    int len = MultiByteToWideChar(srcCP, 0, src, srcCnt, NULL, 0);

    //
    //  Now len includes null terminator, but we wanna behavior similar to strlen()
    //
    if( len > 0 ) len--;
    return len;
}


int WINAPI _toUni(PWSTR dst, PCSTR src, int dstCnt, UINT srcCP, int srcCnt)
{
    int len = MultiByteToWideChar(srcCP, 0, src, srcCnt, dst, dstCnt);
    if( len > 0 ) len--;
    return len;
}

/////////////////////////////////////////////////////////////////////////////////////////////////
//                               End of file "CPConversion.cpp"                                //
/////////////////////////////////////////////////////////////////////////////////////////////////
