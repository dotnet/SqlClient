/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       Guid.cpp
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Simple wrapper class for GUID               (Reduced version for Bid2Etw28)
//
//  Comments:
//              File Created : 17-Sep-2003
//              Last Modified: 06-Mar-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
/////////////////////////////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "Guid.h"


namespace Chicago { /////////////////////////////////////////////////////////////////////////////
//
//  History:    31-Dec-1993     ErikGav     Chicago port
//              14-Sep-2003     KIsakov     copy-paste with few adjustments

static const BYTE GuidMap[] =
        { 3, 2, 1, 0, '-', 5, 4, '-', 7, 6, '-', 8, 9, '-', 10, 11, 12, 13, 14, 15 };

static const TCHAR tszDigits[] = _T("0123456789ABCDEF");

// >> kisakov
static const int UUIDConvertBufLen = sizeof("{00000000-0000-0000-0000-000000000000}") + 2;


static void tStringFromUUID(const GUID* pGuid, PTSTR lpsz)
{
    PTSTR       p       = lpsz;
    const BYTE* pBytes  = (const BYTE*) pGuid;

    *p++ = _T('{');

    for (int i = 0; i < sizeof(GuidMap); i++)
    {
        if (GuidMap[i] == _T('-'))
        {
            *p++ = _T('-');
        }
        else
        {
            *p++ = tszDigits[ (pBytes[GuidMap[i]] & 0xF0) >> 4 ];
            *p++ = tszDigits[ (pBytes[GuidMap[i]] & 0x0F) ];
        }
    }

    *p++ = _T('}');
    *p   = _T('\0');

    // >> kisakov
    DASSERT( (int)(p - lpsz) < UUIDConvertBufLen );

} // tStringFromUUID


static BOOL HexStringToDword(PCTSTR& lpsz, DWORD& Value, int cDigits, TCHAR chDelim)
{
    int Count;

    Value = 0;
    for (Count = 0; Count < cDigits; Count++, lpsz++)
    {
        if (*lpsz >= '0' && *lpsz <= '9')
            Value = (Value << 4) + *lpsz - '0';
        else if (*lpsz >= 'A' && *lpsz <= 'F')
            Value = (Value << 4) + *lpsz - 'A' + 10;
        else if (*lpsz >= 'a' && *lpsz <= 'f')
            Value = (Value << 4) + *lpsz - 'a' + 10;
        else
            return(FALSE);
    }

    if (chDelim != 0)
        return *lpsz++ == chDelim;
    else
        return TRUE;

} // HexStringToDword


static BOOL tUUIDFromString(PCTSTR lpsz, LPGUID pguid)
{
    DWORD dw;

    if (!HexStringToDword(lpsz, pguid->Data1, sizeof(DWORD)*2, '-'))
        return FALSE;

    if (!HexStringToDword(lpsz, dw, sizeof(WORD)*2, '-'))
        return FALSE;

    pguid->Data2 = (WORD)dw;

    if (!HexStringToDword(lpsz, dw, sizeof(WORD)*2, '-'))
        return FALSE;

    pguid->Data3 = (WORD)dw;

    if (!HexStringToDword(lpsz, dw, sizeof(BYTE)*2, 0))
        return FALSE;

    pguid->Data4[0] = (BYTE)dw;

    if (!HexStringToDword(lpsz, dw, sizeof(BYTE)*2, '-'))
        return FALSE;

    pguid->Data4[1] = (BYTE)dw;

    if (!HexStringToDword(lpsz, dw, sizeof(BYTE)*2, 0))
        return FALSE;

    pguid->Data4[2] = (BYTE)dw;

    if (!HexStringToDword(lpsz, dw, sizeof(BYTE)*2, 0))
        return FALSE;

    pguid->Data4[3] = (BYTE)dw;

    if (!HexStringToDword(lpsz, dw, sizeof(BYTE)*2, 0))
        return FALSE;

    pguid->Data4[4] = (BYTE)dw;

    if (!HexStringToDword(lpsz, dw, sizeof(BYTE)*2, 0))
        return FALSE;

    pguid->Data4[5] = (BYTE)dw;

    if (!HexStringToDword(lpsz, dw, sizeof(BYTE)*2, 0))
        return FALSE;

    pguid->Data4[6] = (BYTE)dw;
    if (!HexStringToDword(lpsz, dw, sizeof(BYTE)*2, 0))
        return FALSE;

    pguid->Data4[7] = (BYTE)dw;
    return TRUE;

} // tUUIDFromString

} // namespace Chicago


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Guid
//
void Guid::Init(PCTSTR textStr)
{
    convert(textStr);
    BidTraceU3(BID_ADV, BID_TAG1("ADV") _T("%p{.}  \"%s\"  input: \"%s\"\n"),
                this, (PCTSTR)ToString(),
                (textStr == NULL || BidValidString(textStr)) ? textStr : _T("<BadPtr>") );
}

Guid& Guid::SeriesFrom(const Guid& other)
{
    _value = other._value;
    _value.Data1++;
    return *this;
}

CStr& Guid::ToStr(CStr& dstBuf, bool bAdd) const
{
    TCHAR tmpBuf [Chicago::UUIDConvertBufLen];
    Chicago::tStringFromUUID(&_value, tmpBuf);
    if( !bAdd ){
        dstBuf.Erase();
    }
    dstBuf += tmpBuf;
    return dstBuf;
}

CStr Guid::ToString() const
{
    CStr tmpBuf;
    return ToStr(tmpBuf, false);
}

void Guid::convert(PCTSTR textStr)
{
    BID_SRCFILE;

    PCTSTR tmp = textStr;
    __try {
        if( textStr == NULL || *textStr == _T('\0') ){
            FakeGuidFromText( _value, textStr );
        }
        else {
            if( tmp[0] == _T('{') ) tmp++;
            if( !looksLikeGuid(tmp) ){
                FakeGuidFromText( _value, textStr );
            }
            else if( !Chicago::tUUIDFromString(tmp, &_value) ){
                BidTrace2(BID_TAG1("ERR|ARGS") _T("%p{.}  Bad input: \"%s\"\n"), this, textStr);
                cleanup();
            }
        }
    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER ){
        cleanup();
    }
}

bool Guid::looksLikeGuid(PCTSTR s)
{
    //          1         2         3     6
    //0123456789.123456789.123456789.12345
    //00000000-0000-0000-0000-000000000000
    //
    return s[8]  == _T('-') && s[13] == _T('-')
        && s[18] == _T('-') && s[23] == _T('-');
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                   End of file "Guid.cpp"                                    //
/////////////////////////////////////////////////////////////////////////////////////////////////
