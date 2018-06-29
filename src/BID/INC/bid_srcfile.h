/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       BID_SRCFILE.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   SourceFile Info optimizer.
//
//  Comments:   Include this file at the beginning of each compilation unit (*.c/*.cpp)
//              in order to make all BID tracepoints in this unit use single pointer to "__FILE__".
//              This can help reduce the number of pointers allocated in .sdbid section.
//
//              #include  "BidApi.h"
//              #include  "BID_SRCFILE.h"
//                         BID_SRCFILE  // This macro allocates actual pointer to "__FILE__"
//
//              However, for source modules that textually include other source files, this
//              technique may produce false information about source file/line number.
//
//              Last Modified: 15-May-2005
//
//              <owner current="true" primary="true">kisakov</owner>
//
/////////////////////////////////////////////////////////////////////////////////////////////////
#ifndef _BID_DECLARED
  #error BID declaration must be included before BID_SRCFILE.h
#endif
#ifdef __BID_SRCFILE_H__
  #error BID_SRCFILE.h can be included only once per compilation unit (source file)
#endif
#define __BID_SRCFILE_H__

#undef  _bidSF
#undef  _bid_SRCINFO

#define _bidSF      (UINT_PTR)_bidSrcFile2A
#define _bid_SRCINFO

/////////////////////////////////////////////////////////////////////////////////////////////////
//                                 End of file "BID_SRCFILE.h"                                 //
/////////////////////////////////////////////////////////////////////////////////////////////////
