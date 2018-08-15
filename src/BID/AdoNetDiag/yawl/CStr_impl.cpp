/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       CStr_impl.cpp
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Pseudo-Template implementation of CStr class.
//
//  Comments:   Expands to CStrA & CStrW.
//              Redesign of MFC' CString
//
//              File Created : 12-Apr-1996
//              Last Modified: 10-Jun-2005
//
//              <owner current="true" primary="true">kisakov</owner>
//
/////////////////////////////////////////////////////////////////////////////////////////////////
#ifndef __IMPL_CSTR__
#error  Supplement file for CStr class. DO NOT compile it directlry!
#endif

//
//  Define internal datatypes (CHAR_T, PSTR_T etc.)
//
#define     __CSTR_IMPL_H__
#include    "yawl/CStr_impl.h"
#undef      __CSTR_IMPL_H__


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                          StrDataT                                           //
/////////////////////////////////////////////////////////////////////////////////////////////////

StrDataT* StrDataT::allocate( CSTRSZ nAlloc, StrDataT* pOther )
{
    if( nAlloc == 0 )
    {
        DASSERT( pOther == nullData || pOther == NULL );
        return nullData;
    }

    StrDataT* p = NULL;

    YAWL_MALLOC( p, StrDataT, sizeof(StrDataT) + (nAlloc + 1) * sizeof(CHAR_T) );
    DASSERT( p != NULL );


    if( pOther == NULL || pOther == nullData )
    {
        p->m_RefsNFlags = 0;    // nRefs = 0; all flags = false
        p->m_nChars = 0;
        p->data()[0] = '\0';
    }
    else
    {
        CSTRSZ bytes2Copy;

        DASSERT( BidValidAddress( pOther, sizeof(StrDataT), FALSE/*RO*/) );

        bytes2Copy = (CSTRSZ) min( nAlloc, pOther->m_nChars); // so far just number of characters
        bytes2Copy = sizeof(StrDataT) + (bytes2Copy + 1) * sizeof(CHAR_T);

        DASSERT( BidValidAddress( pOther, bytes2Copy, FALSE/*RO*/) );
        DASSERT( bytes2Copy <= sizeof(StrDataT) + (nAlloc + 1) * sizeof(CHAR_T) );
        YAWL_CopyMemory( p, pOther, bytes2Copy );

        p->m_RefsNFlags = (pOther->m_RefsNFlags & fCPMASK); // nRefs = 0; copy CodePage flags.

        BidTraceU4A(BID_ADV,
                    BID_TAG1A("PERF|ADV") "dup %p{.} %u  from %p{StrDataT} %u\n",
                    p, nAlloc, pOther, pOther->m_nAlloc);
    }

    p->m_nAlloc = nAlloc;
    if( p->m_nChars > nAlloc ) p->m_nChars = nAlloc;
    p->data()[nAlloc] = '\0';

    return p;

} // StrDataT::allocate


void StrDataT::release()
{
    if( isStatic() ) return;

    DASSERT( getRefs() > 0 );
    --m_RefsNFlags;

    if( getRefs() == 0 )
    {
        register void* p = (void*)this;
        YAWL_FREE( p );
    }
}

bool StrDataT::isCompatibleCP(const StrDataT* pOther) const
{
    return (m_RefsNFlags & fCPMASK) == (pOther->m_RefsNFlags & fCPMASK);
}

UINT StrDataT::getCodePage() const
{
    if( isOemCP() ){
        return CP_OEMCP;
    }
    else if( isUTF8() ){
        return CP_UTF8;
    }
    else {
        DASSERT( isAnsiOrUni() );
        return CP_ACP;
    }
}


#ifdef _W_CSTR

    void StrDataW::setCodePageFlags( UINT codePage)
    {
        UNUSED_ALWAYS( codePage );
        DASSERT( BAD_CODE_PATH );   // Should not be called for WCHAR strings
    }

#endif
#ifdef _A_CSTR

    void StrDataA::setCodePageFlags( UINT codePage)
    {
        if( codePage == CP_OEMCP ){
            setFlags(fCPMASK, fOEMCP);
        }
        else if( codePage == CP_UTF8 ){
            setFlags(fCPMASK, fUTF8);
        }
        else{
            BidTraceU2( (BID_ADV && codePage != CP_ACP),
                        BID_TAG1("WARN|CVTCP|ADV") _T("%p{.}  %d{CODE_PAGE} ignored, set CP_ACP\n"),
                        this, codePage );
            setFlags(fCPMASK, 0);
        }
    }

#endif


void StrDataT::setFlags( UINT mask, UINT bits)
{
    DASSERT( (mask & ~fMASK) == 0 );    // Must stay within flags zone and not touch RefCounter

    m_RefsNFlags ^= (bits ^ m_RefsNFlags) & mask;
}


PSTR_T StrDataT::setupStatic( BYTE* pRawData, int nBytes)
{
    StrDataT*   pData = (StrDataT*)pRawData;
    int         nAlloc;

    DASSERT( BidValidAddress( pRawData, nBytes, TRUE) );

    nAlloc = nBytes - sizeof(StrDataT);     // actual num. of bytes for buffer
    DASSERT( nAlloc > sizeof(CHAR_T) );     // must be at least one extra character (terminator)
    nAlloc = (nAlloc - 1) / sizeof(CHAR_T); // convert to capacity (in characters, not bytes)

    pData->m_RefsNFlags   = UINT( fSTATIC | 1);
    pData->m_nAlloc       = (CSTRSZ)nAlloc;
    pData->m_nChars       = 0;
    pData->data()[0]      = '\0';
    pData->data()[nAlloc] = '\0';

    return pData->data();
}



/////////////////////////////////////////////////////////////////////////////////////////////////
//                                            CStrT                                            //
/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Ctors & Dtor
//
CStrT::CStrT( const CStrT& stringSrc)
{
    init();
    operator=( stringSrc);
}

#ifdef _W_CSTR
CStrW::CStrW( const CStrA& stringSrc)
#else
CStrA::CStrA( const CStrW& stringSrc)
#endif
{
    init();
    convertCopy(stringSrc.GetStrPtr(), stringSrc.GetCodePage(), stringSrc.GetLength());
}


CStrT::CStrT( PCSTR_T psz, UINT codePage, int nLength)
{
    init();

    if( nLength < 0 )
    {
        nLength = safeStrlen( psz);
    }
    if( nLength != 0 )
    {
        DASSERT( BidValidAddress( psz, nLength * sizeof(CHAR_T), FALSE));
        allocBuffer( nLength);
        YAWL_CopyMemory( m_pchData, psz, nLength * sizeof(CHAR_T));
    }

   #ifdef _W_CSTR
    UNUSED_ALWAYS(codePage);
    DASSERT( getData()->isAnsiOrUni() );
   #else
    setCodePageFlags(codePage);
   #endif
}


#ifdef _W_CSTR

    CStrW::CStrW( PCSTR psz, UINT srcCodePage, int nLength)
    {
        init();
        convertCopy(psz, srcCodePage, nLength);
    }

#else

    CStrA::CStrA( PCWSTR psz, UINT dstCodePage, int nLength)
    {
        init();
        convertCopy(psz, dstCodePage, nLength);
    }

#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Attributes & Operations
//
CStrT& CStrT::Empty()
{
    smartAssign( nullData);
    return *this;
}


CStrT& CStrT::Erase()
{
    if( GetLength() > 0 ){
        ReleaseBuffer(0);
    }
    return *this;
}


void CStrT::SetAt( int nIndex, CHAR_T ch)
{
    DASSERT(nIndex >= 0);
    DASSERT(nIndex < GetLength());

    copyBeforeWrite();
    m_pchData [nIndex] = ch;
}



/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Overloaded assignment
//
const CStrT& CStrT::operator=( const CStrT& other)
{
    StrDataT* pOther = other.getData();

    if( pOther->isRefCountable() )
    {
        smartAssign( pOther);
    }
    else
    {
        int len = pOther->getNChars();
        assignCopy( len, other.GetStrPtr());

        BidTraceU3( (BID_ADV && len > 0),
                    BID_TAG1("PERF|ADV") _T("%p{.} Copy %d chars from %p{.}\n"),
                    this, len, pOther );
    }
    return *this;
}

#ifdef _W_CSTR
const CStrT& CStrT::operator=( const CStrA& stringSrc)
#else
const CStrT& CStrT::operator=( const CStrW& stringSrc)
#endif
{
    convertCopy(stringSrc.GetStrPtr(), stringSrc.GetCodePage(), stringSrc.GetLength());
    return *this;
}


const CStrT& CStrT::operator=( PCSTR_T psz)
{
    if( psz == NULL || *psz == _T('\0') ){
        Empty();
    }else{
        DASSERT( tBidValidString(psz));
        assignCopy( safeStrlen(psz), psz);
    }
    return *this;
}

#ifdef _W_CSTR

    const CStrW& CStrW::operator=( PCSTR asciiStr)
    {
        convertCopy(asciiStr, CP_ACP, -1);
        return *this;
    }

#else

    const CStrA& CStrA::operator=( PCWSTR psz)
    {
        convertCopy(psz, CP_ACP, -1);
        return *this;
    }

#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  String Concatenation
//

const CStrT& CStrT::operator+=( const CStrT& string)
{
    DASSERT( isCompatibleCP(string) );
    return concatInPlace( string.GetLength(), string.m_pchData);
}

const CStrT& CStrT::operator+=( PCSTR_T psz)
{
    DASSERT( psz == NULL || tBidValidString(psz));
    return concatInPlace( safeStrlen(psz), psz);
}

const CStrT& CStrT::operator+=( CHAR_T ch)
{
    return concatInPlace(1, &ch);
}


CStrT& CStrT::Add( const CStrT& string)
{
    DASSERT( isCompatibleCP(string) );
    return concatInPlace( string.GetLength(), string.m_pchData);
}

CStrT& CStrT::Add( PCSTR_T psz)
{
    DASSERT( psz == NULL || tBidValidString(psz));
    return concatInPlace( safeStrlen(psz), psz);
}

CStrT& CStrT::Add( PCSTR_T psz, int srcLen)
{
    DASSERT( srcLen <= safeStrlen(psz) );
    return concatInPlace( srcLen, psz);
}

CStrT& CStrT::Add( CHAR_T ch, int nRepeat /* = 1 */)
{
    if( nRepeat > 0 )
    {
        int curLen = GetLength();
        int newLen = curLen + nRepeat;

        GetBufferSetLength( newLen);

       #ifdef _W_CSTR
        PWSTR   pp = m_pchData + curLen;
        while( --nRepeat >= 0 ) *pp++ = ch;
       #else
        YAWL_FillMemory( m_pchData + curLen, nRepeat, ch);
       #endif
    }
    return *this;

} // CStrT::Add



/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Simple sub-string extraction
//
void CStrT::Left(CStrT& dest, int nCount) const
{
    if( nCount < 0 ){
        nCount = 0;
    }

    if( nCount >= GetLength() ){
        dest = *this;
    }
    else {
        allocCopy(dest, nCount, 0);
    }
}


void CStrT::Mid(CStrT& dest, int nFirst, int nCount) const
{
    if (nFirst < 0)
        nFirst = 0;
    if (nCount < 0)
        nCount = 0;

    if (nFirst + nCount > GetLength())
        nCount = GetLength() - nFirst;
    if (nFirst > GetLength())
        nCount = 0;

    DASSERT(nFirst >= 0);
    DASSERT(nFirst + nCount <= GetLength());

    if( nFirst == 0 && nFirst + nCount == GetLength() ){
        dest = *this;
    }
    else {
        allocCopy( dest, nCount, nFirst);
    }
}


void CStrT::Right(CStrT& dest, int nCount) const
{
    if (nCount < 0)
        nCount = 0;

    if( nCount >= GetLength() ){
        dest = *this;
    }
    else {
        allocCopy( dest, nCount, GetLength()-nCount);
    }
}


CStrT CStrT::Left(int nCount) const
{
    CStrT dest;
    Left(dest, nCount);
    return dest;
}

CStrT CStrT::Mid(int nFirst, int nCount) const
{
    CStrT dest;
    Mid( dest, nFirst, nCount );
    return dest;
}

CStrT CStrT::Right(int nCount) const
{
    CStrT dest;
    Right( dest, nCount );
    return dest;
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Searching
//

int CStrT::Find( CHAR_T ch, int nStart) const
{
    int nLength = GetLength();
    if( nStart < 0 || nStart >= nLength )
    {
        return (-1);
    }

    // find first single character
    PSTR_T psz = tstrchr( m_pchData + nStart, ch);

    // return -1 if not found and index otherwise
    return (psz == NULL) ? -1 : (int)(psz - m_pchData);
}


int CStrT::Find( PCSTR_T pSub, int nStart) const
{
    DASSERT( tBidValidString(pSub));

    int nLength = GetLength();
    if( nStart < 0) nStart = 0;
    if (nStart > nLength) return -1;

    // find first matching substring
    PSTR_T psz = tstrstr(m_pchData + nStart, pSub);

    // return -1 for not found, distance from beginning otherwise
    return (psz == NULL) ? -1 : (int)(psz - m_pchData);
}


int CStrT::ReverseFind( CHAR_T ch, int nStart) const
{
    int     len = getData()->getNChars();
    PSTR_T  psz = m_pchData + ((nStart < len) ? nStart : len);

    while( --psz >= m_pchData && *psz != ch ) { ; }

    // return -1 if not found, distance from beginning otherwise
    return (int)(psz - m_pchData);
}



/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  CHAR <-> WCHAR conversion
//


CStrA CStrT::ToBytes( UINT dstCodePage ) const
{
    CStrA  buf;
    ToStrA( buf, dstCodePage );
    return buf;
}


#ifdef _A_CSTR

    void CStrA::convertToStrA(PCSTR srcStr, int srcLen, UINT srcCP, CStrA& dstStr, UINT dstCP) const
    {
        BID_SRCFILE;

        int srcCapacity;
        int dstCapacity;

        MemBlkRaw<WCHAR> memBuf;
        PWSTR            tmpBuf = NULL;

        dstStr.Empty();

        srcCapacity = srcLen + 1;
        dstCapacity = _uniLen(srcStr, srcCP, srcCapacity);
        if( dstCapacity == 0 ){
            return;                                                     // << === EARLY EXIT
        }
        dstCapacity++;   // convert Length to Capacity (include null terminator)

        if( dstCapacity < STACKBUF_THRESHOLD ){
            tmpBuf = (PWSTR) _alloca( dstCapacity * sizeof(WCHAR) );
        }
        else {
            MemBlkRaw_ALLOC( memBuf, dstCapacity * sizeof(WCHAR) );
            tmpBuf = memBuf.Ptr();
            BidTraceU2( BID_ADV,
                        BID_TAG1("IO|PERF|ADV") _T("tmpBuf: %p in heap: %d bytes\n"),
                        tmpBuf, dstCapacity * sizeof(WCHAR) );
        }

        int nRet = _toUni( tmpBuf, srcStr, dstCapacity, srcCP, srcCapacity );
        BidCHK( (dstCapacity - 1) == nRet );

        CStrA tmpStr( tmpBuf, dstCP, dstCapacity - 1 );

        dstStr = tmpStr;

        BidTraceU5( BID_ADV,
                    _T("<CStrA::ToStrA|PERF|CVTCP|ADV> ")
                    _T("%p{.}  srcLen: %d  srcCP: %d{CODE_PAGE}  dstCP: %d{CODE_PAGE}  dstLen: %d\n"),
                    this, srcLen, srcCP, dstCP, dstStr.GetLength() );

    } // CStrA::convertToStrA


    enum CStr_CPType { cptACP, cptOEM, cptUTF8, cptCPXXX, cptRange  };

    static CStr_CPType CStr_getCPType(UINT codePage)
    {
        return codePage == CP_ACP   ? cptACP
             : codePage == CP_OEMCP ? cptOEM
             : codePage == CP_UTF8  ? cptUTF8
             : cptCPXXX;
    }

    CStrA& CStrA::ToStrA( CStrA& dstStr, UINT dstCP ) const
    {
        BID_SRCFILE;

        enum HelperFunction
        {
            hfCopy, hfOem2Ansi, hfAnsi2Oem, hfConvert
        };

        static HelperFunction hfTable [cptCPXXX][cptRange] =
        {
            /*  src \ dst    ACP         OEM        UTF8       CPXXX   */
            /*  ACP  */ { hfCopy    , hfAnsi2Oem, hfConvert, hfConvert },
            /*  OEM  */ { hfOem2Ansi, hfCopy    , hfConvert, hfConvert },
            /*  UTF8 */ { hfConvert , hfConvert , hfCopy   , hfConvert }
        };

        int  dstLen;
        UINT srcCP = getData()->getCodePage();

        CStr_CPType srcCPType = CStr_getCPType( srcCP );
        CStr_CPType dstCPType = CStr_getCPType( dstCP );

        DASSERT( srcCPType < cptCPXXX );

        HelperFunction selector = hfTable [srcCPType][dstCPType];
        switch( selector )
        {
         case hfCopy:
            dstStr = *this;
            break;

         case hfOem2Ansi:
            dstLen = GetLength();
            if( 0 == dstLen ){
                dstStr.Erase();
            }
            else {
                BidCHK(OemToCharBuffA(GetStrPtr(), dstStr.GetBuffer(dstLen), dstLen));
                dstStr.ReleaseBuffer(dstLen);
            }
            dstStr.setCodePageFlags(CP_ACP);
            break;

         case hfAnsi2Oem:
            dstLen = GetLength();
            if( 0 == dstLen ){
                dstStr.Erase();
            }
            else {
                BidCHK(CharToOemBuffA(GetStrPtr(), dstStr.GetBuffer(dstLen), dstLen));
                dstStr.ReleaseBuffer(dstLen);
            }
            dstStr.setCodePageFlags(CP_OEMCP);
            break;

         case hfConvert:
            convertToStrA(GetStrPtr(), GetLength(), srcCP, dstStr, dstCP);
            break;

         default:
            DASSERT(BAD_ENUM);
            dstStr.Empty();

        } // switch

        return const_cast<CStrA&>(*this);

    } // CStrA::ToStrA


    CStrA& CStrA::ToStrW( CStrW& dstStr, UINT srcCP ) const
    {
        BID_SRCFILE;

        int srcCapacity;
        int dstLen;

        if( (srcCapacity = GetLength()) == 0 ){
            dstStr.Erase();
            return const_cast<CStrA&>(*this);   // << === EARLY EXIT
        }
        srcCapacity++;

        if( srcCP == UINT(-1) ){
            srcCP = GetCodePage();
        }

        if( (dstLen = _uniLen( GetStrPtr(), srcCP, srcCapacity)) == 0 ){
            dstStr.Erase();
            return const_cast<CStrA&>(*this);   // << === EARLY EXIT
        }

        int nRet = _toUni( dstStr.GetBuffer(dstLen), GetStrPtr(), dstLen + 1,
                           srcCP, srcCapacity);
        BidCHK( dstLen == nRet );
        dstStr.ReleaseBuffer(nRet);

        BidTraceU4( BID_ADV,
                    BID_TAG1("PERF|CVTCP|ADV")
                    _T("%p{.}  srcLen: %d  srcCP: %d{CODE_PAGE}  dstLen: %d\n"),
                    this, srcCapacity-1, srcCP, dstStr.GetLength() );

        return const_cast<CStrA&>(*this);

    } // CStrA::ToStrW


    //
    //  ANSI <-> OEM support (in-place conversion)
    //
    CStrA& CStrA::ConvertToAnsi()
    {
        return ToStrA( *this, CP_ACP );
    }

    CStrA& CStrA::ConvertToOem()
    {
        return ToStrA( *this, CP_OEMCP );
    }

#endif
#ifdef _W_CSTR

    CStrW& CStrW::ToStrA( CStrA& dstStr, UINT dstCP ) const
    {
        BID_SRCFILE;

        int srcCapacity;
        int dstLen;

        if( (srcCapacity = GetLength()) == 0 ){
            dstStr.Erase();
            return const_cast<CStrW&>(*this);                   // << === EARLY EXIT
        }
        srcCapacity++;

        if( (dstLen = _mbLen( GetStrPtr(), dstCP, srcCapacity)) == 0 ){
            dstStr.Erase();
            return const_cast<CStrW&>(*this);                   // << === EARLY EXIT
        }

                   //_toMB(LPSTR dst, LPCWSTR src, int dstCnt, UINT dstCP, int srcCnt = -1);
        int nRet = _toMB( dstStr.GetBuffer(dstLen), GetStrPtr(), dstLen + 1, dstCP, srcCapacity);
        BidCHK( dstLen == nRet );
        dstStr.ReleaseBuffer(nRet);

        BidTraceU4( BID_ADV,
                    BID_TAG1("PERF|CVTCP|ADV")
                    _T("%p{.}  srcLen: %d  dstCP: %d{CODE_PAGE}  dstLen: %d\n"),
                    this, srcCapacity-1, dstCP, dstStr.GetLength() );

        dstStr.setCodePageFlags(dstCP);
        return const_cast<CStrW&>(*this);

    } // CStrW::ToStrA


    CStrW& CStrW::ToStrW( CStrW& dstStr, UINT srcCP ) const
    {
        UNUSED_ALWAYS(srcCP);
        dstStr = *this;
        DASSERT( dstStr.getData()->isAnsiOrUni() );
        return const_cast<CStrW&>(*this);
    }

#endif


/////////////////////////////////////////////////////////////////////////////////////////////////

#ifdef _A_CSTR

    void CStrA::convertCopy(PCWSTR psz, UINT dstCodePage, int srcLen)
    {
        int srcCount = (srcLen <= 0) ? -1 : srcLen + 1;
        int dstLen = (psz != NULL) ? _mbLen(psz, dstCodePage, srcCount) : 0;

        if( dstLen > 0 )
        {
            allocBeforeWrite( dstLen);
            int n = _toMB( m_pchData, psz, dstLen + 1, dstCodePage, srcCount );
            ReleaseBuffer( n >= 0 ? n : -1 );

            BidTraceU4( BID_ADV,
                        BID_TAG1("PERF|CVTCP|ADV")
                        _T("%p{.}  srcLen: %d  dstCP: %d{CODE_PAGE}  dstLen: %d\n"),
                        this, srcLen, dstCodePage, dstLen );
        }
        else
        {
            Erase();
        }
        setCodePageFlags(dstCodePage);
    }

#endif
#ifdef _W_CSTR

    void CStrW::convertCopy(PCSTR psz, UINT srcCP, int srcLen)
    {
        int srcCount = (srcLen <= 0) ? -1 : srcLen + 1;
        int dstLen = (psz != NULL) ? _uniLen(psz, srcCP, srcCount) : 0;

        if( dstLen > 0 )
        {
            allocBeforeWrite( dstLen );
            int n = _toUni( m_pchData, psz, dstLen + 1, srcCP, srcCount );
            ReleaseBuffer( n >= 0 ? n : -1 );

            BidTraceU4( BID_ADV,
                        BID_TAG1("PERF|CVTCP|ADV")
                        _T("%p{.}  srcLen: %d  srcCP: %d{CODE_PAGE}  dstLen: %d\n"),
                        this, srcLen, srcCP, dstLen );
        }
        else
        {
            Erase();
        }
        DASSERT( getData()->isAnsiOrUni() );
    }

#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Access to string implementation buffer as "C" character array
//

PSTR_T CStrT::GetBuffer( int nMinBufLength)
{
    DASSERT( nMinBufLength >= 0 );

    StrDataT* pData = getData();
    if( !pData->isWriteable() || nMinBufLength > pData->getCapacity() )
    {
        changeCapacity( nMinBufLength);
    }
    return m_pchData;
}


PSTR_T CStrT::GetBufferSetLength( int nNewLength)
{
    DASSERT(nNewLength >= 0);

    GetBuffer( nNewLength);
    setLength( nNewLength);
    return m_pchData;
}


CStrT& CStrT::ReleaseBuffer( int nNewLength)
{
    copyBeforeWrite();              // just in case GetBuffer was not called
    if( nNewLength < 0 ){
        nNewLength = tGetStrLen( m_pchData);
    }
    setLength( nNewLength);
    return *this;
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Misc. helpers
//
const CStrT& CStrT::GetEmptyString()
{
    static PSTR_T pNil = nullStr;
    return *(CStrT*) &pNil;
}


bool CStrT::IsEmpty(PCSTR_T str)
{
    BID_SRCFILE;

    bool bEmpty = true;

    __try
    {
        bEmpty = (str == NULL) || (*str == _Tx('\0'));
    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
    {
        bEmpty = true;
    }
    return bEmpty;
}


#define  CStr_EXTENSION_BUFF_MAX    512

#ifdef _A_CSTR

    BID_EXTENSIONA( CStrA )
    {
        BID_EXTENSION_REF(CStrA, obj);

        UINT sizeInChars = CStr_DumpStrLen;  

        if( sizeInChars > CStr_EXTENSION_BUFF_MAX )
        {
            sizeInChars = CStr_EXTENSION_BUFF_MAX;
        }
        
        PSTR        buf = (PSTR)_alloca( sizeInChars * sizeof(char) );
        StrDataA*   p = obj.getData();

        if( obj.m_pchData == nullStr ){
            buf = "<nullStr>";
        } else {
            buf = (PSTR)BidStrDigestA(buf, sizeInChars, obj.m_pchData);
        }

        BidWrite9A( "StrDataA %p %c%c%c%c Ref %d  Len %3d  Alloc %3d  \"%s\"\n",
                    p,
                    p->isStatic()   ? 's' : '-',
                    p->isReadOnly() ? 'r' : '-',
                    p->isOemCP()    ? 'o' : '-',
                    p->isUTF8()     ? 'f' : '-',
                    p->getRefs(),
                    p->getNChars(),
                    p->getCapacity(),
                    buf );
    }
    BID_EXTENSION_ALIASA( "CStr", CStrA );

#endif
#ifdef _W_CSTR

    BID_EXTENSIONW( CStrW )
    {
        BID_EXTENSION_REF(CStrW, obj);

        UINT sizeInChars = CStr_DumpStrLen;  

        if( sizeInChars > CStr_EXTENSION_BUFF_MAX )
        {
            sizeInChars = CStr_EXTENSION_BUFF_MAX;
        }
        
        PWSTR       buf = (PWSTR)_alloca( sizeInChars * sizeof(WCHAR) );
        StrDataW*   p = obj.getData();

        if( obj.m_pchData == nullStr ){
            buf = L"<nullStr>";
        } else {
            buf = (PWSTR)BidStrDigestW(buf, sizeInChars, obj.m_pchData);
        }

        BidWrite9W( L"StrDataW %p %c%c%c%c Ref %d  Len %3d  Alloc %3d  L\"%ls\"\n",
                    p,
                    p->isStatic()   ? L's' : L'-',
                    p->isReadOnly() ? L'r' : L'-',
                    p->isOemCP()    ? L'o' : L'-',
                    p->isUTF8()     ? L'f' : L'-',
                    p->getRefs(),
                    p->getNChars(),
                    p->getCapacity(),
                    buf );
    }
    BID_EXTENSION_ALIASW( L"CStr", CStrW );

#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Implementation
//
int CStrT::allocBeforeWrite( int nLen)
{
    StrDataT* pData = getData();
    if( !pData->isWriteable() || nLen > pData->getCapacity() )
    {
        allocBuffer( nLen);
    }
    return nLen;
}

int CStrT::allocBuffer( int nLen)
{
    DASSERT( nLen >= 0 );
    smartAssign( StrDataT::allocate( CSTRSZ(nLen)) );
    return setLength( nLen);
}

void CStrT::allocCopy( CStrT& dest, int nCopyLen, int nCopyIndex) const
{
    if (nCopyLen <= 0) {
        dest.Erase();
    } else {
        dest.allocBuffer( nCopyLen);
        YAWL_CopyMemory( dest.m_pchData, m_pchData + nCopyIndex, nCopyLen * sizeof(CHAR_T));
    }
}

void CStrT::assignCopy( int nSrcLen, PCSTR_T pSrcData)
{
    if( allocBeforeWrite( nSrcLen) > 0 )
    {
        DASSERT( tBidValidString( pSrcData, nSrcLen) );
        YAWL_CopyMemory( m_pchData, pSrcData, nSrcLen * sizeof(CHAR_T));
    }
    setLength( nSrcLen);
}

void CStrT::changeCapacity( int newSize)
{
    StrDataT* pData  = getData();
    int       nChars = pData->getNChars();

    //
    //  Re-allocation must occur only if buffer size has to be changed
    //  OR if current buffer is not writeable (ReadOnly OR RefCount > 1)
    //
    DASSERT( newSize != pData->getCapacity() || !pData->isWriteable() );
    DASSERT( newSize >= 0 );

    smartAssign( StrDataT::allocate( (CSTRSZ)newSize, pData) );
    setLength( nChars);
}

void CStrT::concatCopy( int nSrc1Len, PCSTR_T pSrc1Data, int nSrc2Len, PCSTR_T pSrc2Data)
{
    //
    //  Master concatenation routine. Concatenate two sources
    //
    DASSERT( nSrc1Len >= 0 );
    DASSERT( nSrc2Len >= 0 );

    INT64   size64 = INT64(nSrc1Len) + nSrc2Len;
    int     nNewLen = (int)size64; 

    YAWL_THROW1_FROM_METHOD_IF( size64 != (INT64)nNewLen, XC_ABORT, nNewLen, "total chars, overflow" );

    if( allocBuffer( nNewLen) > 0 )
    {
        YAWL_CopyMemory( m_pchData, pSrc1Data, nSrc1Len * sizeof(CHAR_T));
        YAWL_CopyMemory( m_pchData + nSrc1Len, pSrc2Data, nSrc2Len * sizeof(CHAR_T));
    }
}

CStrT& CStrT::concatInPlace( int nSrcLen, PCSTR_T pSrcData)
{
    //
    //  The main routine for += operators
    //
    if( nSrcLen > 0 )
    {
        StrDataT* pData   = getData();
        int       nChars  = pData->getNChars();
        int       nDstLen = nSrcLen + nChars;

        // DASSERT( tBidValidString( pSrcData, nSrcLen) ); -- caller must verify pSrcData
        YAWL_CopyMemory( GetBuffer( nDstLen) + nChars, pSrcData, nSrcLen * sizeof(CHAR_T));
        setLength( nDstLen);
    }
    return *this;
}


void CStrT::copyBeforeWrite()
{
    register StrDataT* pData = getData();
    if( !pData->isWriteable() )
    {
        smartAssign( StrDataT::allocate( pData->m_nAlloc, pData) );
    }
}


void CStrT::smartAssign( StrDataT* pOther)
{
    register StrDataT* pData = getData();
    if( pData == pOther ) return;

    //
    //  The most common cause of this assert is returning read-only/stack-allocated string.
    //  For functions returning CStr object, always use CSTR_RET wrapper:
    //  CStr Foo()
    //  {   CSTR_(tmpBuf, BUFSIZE);
    //      ...
    //      return CSTR_RET(tmpBuf);
    //  }
    //
    DASSERT( !pOther->isStatic() || pOther->isReadOnly());

    pOther->addRef();
    pData->release();
    m_pchData = pOther->data();
}

/////////////////////////////////////////////////////////////////////////////////////////////////
//                                End of file "CStr_impl.cpp"                                  //
/////////////////////////////////////////////////////////////////////////////////////////////////
