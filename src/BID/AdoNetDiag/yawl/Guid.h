/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       Guid.h
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
#ifndef __GUID_H__ //////////////////////////////////////////////////////////////////////////////
#define __GUID_H__
#ifndef _NOLIST_HDRS
#pragma message("  Guid-28.h")
#endif

#include  "yawl/BaseRTL.h"
#include  "yawl/Hashing.h"
#include  "yawl/CStr.h"

#ifndef PCGUID
#define PCGUID  const GUID*
#endif

class Guid
{
 public:
    Guid()                                          { cleanup(); }
    void    Init(PCTSTR textStr);
    void    Done()                                  { cleanup(); }
    Guid&   SeriesFrom(const Guid& other);
    CStr&   ToStr(CStr& dstBuf, bool bAdd = false) const;
    CStr    ToString() const;

    PCGUID  GetPtr() const                          { return &_value;   }

    operator GUID&()                                { return _value;    }
    operator const GUID&() const                    { return _value;    }

    const   Guid& operator=(const Guid& other)      { _value = other._value; return *this;      }

 private:
    GUID    _value;

    void    cleanup()                               { YAWL_ZeroMemory(&_value, sizeof(_value)); }
    void    convert(PCTSTR textStr);
    bool    looksLikeGuid(PCTSTR s);

    Guid(const Guid&);

}; // Guid


#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                    End of file "Guid.h"                                     //
/////////////////////////////////////////////////////////////////////////////////////////////////
