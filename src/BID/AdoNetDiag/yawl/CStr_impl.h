/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       CStr_impl.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Pseudo-Template implementation of CStr class.
//
//  Comments:   Expands to CStrA & CStrW.
//              Redesign of MFC' CString
//
//              File Created : 12-Apr-1996
//              Last Modified: 06-Mar-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
// NO standard guard symbol here! ///////////////////////////////////////////////////////////////

#ifndef __CSTR_IMPL_H__
#error Supplemental header file. DO NOT include directly!
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////
//                Unified names for string operations used in CStr implementation              //
/////////////////////////////////////////////////////////////////////////////////////////////////

#if defined( CStrT)
    #undef  StrDataT
    #undef  CStrT
    #undef  _CSTR_NAME
    #undef  _CSTR_X
    #undef  nullStr
    #undef  nullData
    #undef  _X
    #undef  _Tx
    #undef  _WHITESPACE_X

    #undef  CHAR_T
    #undef  PSTR_T
    #undef  PCSTR_T

    #undef  tGetStrLen
    #undef  tBidValidString
    #undef  tstrlen
    #undef  tstrcpy
    #undef  tstrcmp
    #undef  tstricmp
    #undef  tstrcoll
    #undef  tstricoll
    #undef  tstrspn
    #undef  tstrchr
    #undef  tstrrchr
    #undef  tstrstr
    #undef  tisspace
    #undef  tstrpbrk

    #undef  _CSTR_FMT

#endif

#if defined( _W_CSTR)
    // UNICODE version
    #define StrDataT        StrDataW
    #define CStrT           CStrW
    #define _CSTR_NAME      _T("CStrW")
    #define _CSTR_X         L"CStrW"
    #define nullStr         PWSTR(&CStr_EmptyString [3])
    #define nullData        ((StrDataW*)CStr_EmptyString)
    #define _X(token)       token##W
    #define _Tx(str)        L ## str
    #define _WHITESPACE_X   L" \t\n\v\f\r"

    #define CHAR_T          WCHAR
    #define PSTR_T          PWSTR
    #define PCSTR_T         PCWSTR

    #define tGetStrLen      GetStrLenW
    #define tBidValidString BidValidStringW
    #define tstrlen         (int)(INT_PTR)wcslen
    #define tstrcpy         wcscpy
    #define tstrcmp         wcscmp
    #define tstricmp        _wcsicmp
    #define tstrcoll        wcscoll
    #define tstricoll       _wcsicoll
    #define tstrspn         (int)(INT_PTR)wcsspn
    #define tstrchr         wcschr
    #define tstrrchr        wcsrchr
    #define tstrstr         wcsstr
    #define tisspace        iswspace
    #define tstrpbrk        wcspbrk

    #ifdef _UNICODE
      #define _CSTR_FMT     L"%s"
    #else
      #define _CSTR_FMT     "%ls"
    #endif

#elif defined( _A_CSTR)
    // ANSI version
    #define StrDataT        StrDataA
    #define CStrT           CStrA
    #define _CSTR_NAME      _T("CStrA")
    #define _CSTR_X         "CStrA"
    #define nullStr         PSTR(&CStr_EmptyString [3])
    #define nullData        ((StrDataA*)CStr_EmptyString)
    #define _X(token)       token##A
    #define _Tx(str)        str
    #define _WHITESPACE_X   " \t\n\v\f\r"

    #define CHAR_T          char
    #define PSTR_T          PSTR
    #define PCSTR_T         PCSTR

    #define tGetStrLen      GetStrLenA
    #define tBidValidString BidValidStringA
    #define tstrlen         (int)(INT_PTR)strlen
    #define tstrcmp         strcmp
    #define tstrcpy         strcpy
    #define tstricmp        _stricmp
    #define tstrcoll        strcoll
    #define tstricoll       _stricoll
    #define tstrspn         (int)(INT_PTR)strspn
    #define tstrchr         strchr
    #define tstrrchr        strrchr
    #define tstrstr         strstr
    #define tisspace        isspace
    #define tstrpbrk        strpbrk

    #ifdef _UNICODE
      #define _CSTR_FMT     L"%hs"
    #else
      #define _CSTR_FMT     "%s"
    #endif

#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                                                                             //
#if defined( __DECL_CSTR__) /////////////////////////////////////////////////////////////////////

#ifndef _NOLIST_HDRS
  #if defined( _A_CSTR)
    #pragma message("    CStrA-28")
  #elif defined( _W_CSTR)
    #pragma message("    CStrW-28")
  #else
    #error __DECL_CSTR__ defined but none of _A_CSTR or _W_CSTR!
  #endif
#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                          StrDataT                                           //
/////////////////////////////////////////////////////////////////////////////////////////////////

class StrDataT
{
 public:
    enum
    {
        fSTATIC = 0x80000000UL,
        fRDONLY = 0x40000000UL,

        fUTF8   = 0x20000000UL,
        fOEMCP  = 0x10000000UL,
        fCPMASK = 0x30000000UL,

        fMASK   = 0xF0000000UL,
        fSHIFT  = 28,
        cntMASK = 0x0FFFFFFFUL
    };

    PSTR_T  data()                                              { return PSTR_T(this + 1);      }
    static  StrDataT* ctlBlock( PSTR_T ptr)                     { return ((StrDataT*)ptr) - 1;  }

 private:
    UINT    m_RefsNFlags;
    CSTRSZ  m_nChars;
    CSTRSZ  m_nAlloc;
 // CHAR_T  data [m_nAlloc+1];

    enum    { MAX_REFCNT = UINT_MAX & ~fMASK  };

    static  StrDataT* allocate( CSTRSZ nAlloc, StrDataT* pOther = NULL);
    static  PSTR_T    setupStatic( BYTE* pRawData, int nBytes);

    inline  void addRef();
    long    getRefs()                                   { return long(m_RefsNFlags & cntMASK);  }
    void    release();

    bool    isGrowable() const        { return (m_RefsNFlags & (cntMASK|fSTATIC|fRDONLY)) == 1; }
    bool    isReadOnly() const                          { return (m_RefsNFlags & fRDONLY) != 0; }
    bool    isRefCountable() const      { return (m_RefsNFlags & (fSTATIC|fRDONLY)) != fSTATIC; }
    bool    isStatic() const                            { return (m_RefsNFlags & fSTATIC) != 0; }
    bool    isWriteable() const               { return (m_RefsNFlags & (cntMASK|fRDONLY)) == 1; }

    bool    isAnsiOrUni() const                         { return (m_RefsNFlags & fCPMASK) == 0; }
    bool    isOemCP() const                             { return (m_RefsNFlags & fOEMCP) != 0;  }
    bool    isUTF8() const                              { return (m_RefsNFlags & fUTF8) != 0;   }
    bool    isCompatibleCP(const StrDataT* pOther) const;

    UINT    getCodePage() const;
    UINT    getFlags() const                                { return UINT(m_RefsNFlags & fMASK);}
    void    setCodePageToAnsiOrUni()                                { m_RefsNFlags &= ~fCPMASK; }
    void    setCodePageFlags( UINT codePage);
    void    setFlags( UINT mask, UINT bits);

    UINT    getEncodingBits() const             { return UINT(m_RefsNFlags & fCPMASK) >> fSHIFT;}
    void    setEncodingBits(UINT bits)                      { setFlags(fCPMASK, bits << fSHIFT);}

    int     getCapacity() const                                         { return (int)m_nAlloc; }
    int     getNChars() const                                           { return (int)m_nChars; }
    inline  int setNChars( int nChars);

    friend  class CStrT;

    //
    //  This declares CStr BidExtensions as friends for StrDataA and StrDataW
    //
   #ifdef _A_CSTR
    BID_EXTENSION_DECLARE(CStrA);
   #else
    BID_EXTENSION_DECLARE(CStrW);
   #endif

    friend  void tryStrDataT();         // *???*

}; // StrDataT


inline void StrDataT::addRef()
{
    DASSERT( getRefs() < MAX_REFCNT );  // TODO: should this be retail check ?
    if( !isStatic() ) ++m_RefsNFlags;
}

inline int StrDataT::setNChars( int nChars)
{
    DASSERT( CSTRSZ(nChars) <= m_nAlloc );
    DASSERT( isWriteable() );
    m_nChars = CSTRSZ(nChars);
    data() [nChars] = '\0';
    return nChars;
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                            CStrT                                            //
/////////////////////////////////////////////////////////////////////////////////////////////////

class CStrT
{
 public:
    //
    //  Ctors & Dtor ============================================================================
    //
    CStrT()                                                                         { init();   }
    CStrT( const int p )                                    { p; DASSERT(p == NULL);  init();   }
    CStrT( const StrDataT& d) { m_pchData = ((StrDataT*)&d)->data(); ((StrDataT*)&d)->addRef(); }
    CStrT( const CStrT& stringSrc);
   #ifdef _W_CSTR
    CStrT( const CStrA& stringSrc);
   #else
    CStrT( const CStrW& stringSrc);
   #endif
    CStrT( LPCSTR lpch,  UINT srcCodePage = CP_ACP, int nLength = -1);
    CStrT( LPCWSTR lpch, UINT dstCodePage = CP_ACP, int nLength = -1);
    CStrT( const BYTE* psz)                                     { init(); *this = (LPCSTR)psz;  }
    ~CStrT()                                                                        { done();   }

    //
    //  Attributes & Operations =================================================================
    //
    CStrT&  Empty();                                        // Delete buffer
    CStrT&  Erase();                                        // Delete content, buffer remains

    int     GetAllocLength() const                          { return getData()->getCapacity();  }
    int     GetLength() const                               { return getData()->getNChars();    }
    CHAR_T  GetLastChar() const             { return IsEmpty() ? '\0' : GetAt( GetLength()-1 ); }
    int     GetNumOfBytes() const           { return getData()->getNChars() * sizeof(CHAR_T);   }
    bool    HasStaticBuffer() const                         { return getData()->isStatic();     }
    bool    IsEmpty() const                                 { return GetLength() == 0;          }

    CHAR_T  GetAt( int nIndex) const                                { return (*this)[nIndex];   }
    inline  CHAR_T operator[]( int nIndex) const;
    void    SetAt( int nIndex, CHAR_T ch);

    operator PCSTR_T() const                                                { return m_pchData; }
    PCSTR_T  GetStrPtr() const                                              { return m_pchData; }

    bool    operator!() const                                               { return IsEmpty(); }
    bool    operator==( const int n) const             { n; DASSERT( n == 0); return IsEmpty(); }

    //
    //  Overloaded assignment ===================================================================
    //
    const CStrT& operator=(const CStrT& stringSrc);     // Ref-counted copy from another CStrT
   #ifdef _W_CSTR
    const CStrT& operator=(const CStrA& string);
   #else
    const CStrT& operator=(const CStrW& string);
   #endif

    const CStrT& operator=(LPCSTR psz);
    const CStrT& operator=(LPCWSTR psz);

    //
    //  Explicit duplication ====================================================================
    //
    CStrT& CopyFrom( const CStrT& str)      { assignCopy( str.GetLength(), str); return *this;  }
    CStrT  MakeClone() const                    { return CStrT(m_pchData, _defCP, GetLength()); }

    //
    //  String concatenation ====================================================================
    //
    const CStrT& operator+=(const CStrT& string);
    const CStrT& operator+=(PCSTR_T psz);
    const CStrT& operator+=(CHAR_T ch);
   #ifdef _W_CSTR
    const CStrT& operator+=(char ch)                       { *this += (WCHAR)ch; return *this;  }
   #endif
    CStrT&  Add( const CStrT& string);
    CStrT&  Add( PCSTR_T psz);
    CStrT&  Add( PCSTR_T psz, int srcLen);
    CStrT&  Add( CHAR_T ch, int nRepeat = 1);

    CStrT&  operator << ( const CStrT& string)                          { return Add( string);  }
    CStrT&  operator << ( PCSTR_T psz)                                  { return Add( psz);     }
    CStrT&  operator << ( CHAR_T ch)                                    { return Add( ch);      }

    //
    //  Simple sub-string extraction ============================================================
    //
    void    Left( CStrT& dest, int nCount) const;
    void    Mid( CStrT& dest, int nFirst, int nCount = LONG_MAX - SHRT_MAX) const;
    void    Right( CStrT& dest, int nCount) const;

    CStrT   Left( int nCount) const;
    CStrT   Mid( int nFirst, int nCount = LONG_MAX - SHRT_MAX) const;
    CStrT   Right( int nCount) const;

    //
    //  Searching ===============================================================================
    //
    int     Find( CHAR_T ch, int nStart = 0) const;
    int     Find( PCSTR_T pSub, int nStart = 0) const;

    int     ReverseFind( CHAR_T ch, int nStart = INT_MAX) const;

    //
    //  CHAR <-> WCHAR and CodePage conversions =================================================
    //
    UINT    GetCodePage() const                             { return getData()->getCodePage();  }

    CStrT&  ToStrA( CStrA& strDst, UINT dstCodePage = CP_ACP) const;
    CStrT&  ToStrW( CStrW& strDst, UINT srcCodePage = UINT(-1)) const;

    CStrA   ToBytes( UINT dstCodePage) const;

   #ifdef _A_CSTR
    CStrA&  ConvertToAnsi();
    CStrA&  ConvertToOem();
   #endif

    //
    //  Access to string implementation buffer as "C" character array ===========================
    //
    PSTR_T  GetBuffer( int nMinBufLength);
    PSTR_T  GetBufferSetLength( int nNewLength);
    CStrT&  ReleaseBuffer( int nNewLength = -1);

    //
    //  Misc. helpers ===========================================================================
    //
    CStrT&  AllocBuffer( int nBufLen)           { GetBuffer( nBufLen); return ReleaseBuffer(0); }
    CStrT&  FreeExtra();

    static  const CStrT& GetEmptyString();
    static  bool  IsEmpty(PCSTR_T str);

   #ifdef _A_CSTR
    UINT    getEncodingBits() const                     { return getData()->getEncodingBits();  }
   #else
    UINT    getEncodingBits() const     { return (UINT)(StrDataT::fCPMASK >> StrDataT::fSHIFT); }
   #endif

    //
    //  Specials. Use with care! ================================================================
    //
    PVOID   _duplicateRef() const               { getData()->addRef(); return (PVOID)m_pchData; }
    static  void _removeRef( PVOID p)           { StrDataT::ctlBlock( (PSTR_T)p)->release();    }

    //
    //  Helper for CSTR_(staticStr, initialSize); should not be used directly.
    //
    CStrT(BYTE* pRawBuf, int nBytes)    { m_pchData = StrDataT::setupStatic( pRawBuf, nBytes);  }

 protected:
    //
    //  IMPLEMENTATION ==========================================================================
    //
    PSTR_T    m_pchData;
    StrDataT* getData() const                           { return StrDataT::ctlBlock(m_pchData); }

    void    done()                                                      { getData()->release(); }
    void    init()                                                      { m_pchData = nullStr;  }

    int     allocBeforeWrite( int nLen);
    int     allocBuffer( int nLen);
    void    allocCopy( CStrT& dest, int nCopyLen, int nCopyIndex) const;
    void    assignCopy( int nSrcLen, PCSTR_T pSrcData);
    void    changeCapacity( int newSize);
    void    concatCopy( int nSrc1Len, PCSTR_T pSrc1Data, int nSrc2Len, PCSTR_T pSrc2Data);
    CStrT&  concatInPlace( int nSrcLen, PCSTR_T pSrcData);
    void    copyBeforeWrite();

    void    setCodePageToAnsiOrUni()                { getData()->setCodePageToAnsiOrUni();      }
    void    setCodePageFlags( UINT codePage)        { getData()->setCodePageFlags( codePage);   }
    int     setLength(int nLen) { return (m_pchData == nullStr) ? 0: getData()->setNChars(nLen);}
    void    smartAssign( StrDataT* pOther);

    static  int safeStrlen( PCSTR_T psz)                            { return tGetStrLen(psz);   }

    bool isCompatibleCP(const CStrT& other) const
    {
        return getData()->isCompatibleCP(other.getData());
    }

   #ifdef _A_CSTR
    void    convertToStrA(PCSTR srcStr, int srcLen, UINT srcCP, CStrA& dstStr, UINT dstCP) const;
    void    convertCopy( PCWSTR psz, UINT dstCodePage, int srcLen );
    void    setEncodingBits( UINT bits)                     { getData()->setEncodingBits(bits); }
   #else
    void    convertCopy( PCSTR psz, UINT srcCodePage, int srcLen );
    void    setEncodingBits( UINT )                                 { DASSERT(BAD_CODE_PATH);   }
   #endif

   #ifdef _A_CSTR
    BID_EXTENSION_DECLARE(CStrA);
    friend class CStrW;
   #else
    BID_EXTENSION_DECLARE(CStrW);
    friend class CStrA;
   #endif

}; // CStrT

inline CHAR_T CStrT::operator[]( int nIndex) const   // same as GetAt
{
    DASSERT( nIndex >= 0);
    DASSERT( nIndex < getData()->getNChars());
    return m_pchData [nIndex];
}

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  HELPER FUNCTIONS
//
inline int  Capacity    (const CStrT& s)                        { return s.GetAllocLength();    }
inline int  Len         (const CStrT& s)                        { return s.GetLength();         }
inline int  Length      (const CStrT& s)                        { return s.GetLength();         }
inline bool IsStatic    (const CStrT& s)                        { return s.HasStaticBuffer();   }


#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                 End of file "CStr_impl.h"                                   //
/////////////////////////////////////////////////////////////////////////////////////////////////
