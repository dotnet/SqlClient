/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       Hashing.cpp
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Common helpers for text hashing             (Reduced version for Bid2Etw28)
//
//  Comments:
//              File Created : 10-Apr-1996
//              Last Modified: 14-Sep-2003
//
//              <owner current="true" primary="true">kisakov</owner>
//
/////////////////////////////////////////////////////////////////////////////////////////////////
#include  "stdafx.h"
#include  "yawl/Hashing.h"


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                MD5 REFERENCE IMPLEMENTATION                                 //
/////////////////////////////////////////////////////////////////////////////////////////////////

/*
 ***********************************************************************
 ** md5.h -- Header file for implementation of MD5                    **
 ** RSA Data Security, Inc. MD5 Message-Digest Algorithm              **
 ** Created: 2/17/90 RLR                                              **
 ** Revised: 12/27/90 SRD,AJ,BSK,JT Reference C version               **
 ** Revised (for MD5): RLR 4/27/91                                    **
 **   -- G modified to have y&~z instead of y&z                       **
 **   -- FF, GG, HH modified to add in last register done             **
 **   -- Access pattern: round 2 works mod 5, round 3 works mod 3     **
 **   -- distinct additive constant for each step                     **
 **   -- round 4 added, working mod 7                                 **
 ***********************************************************************
 */

/*
 ***********************************************************************
 ** Copyright (C) 1990, RSA Data Security, Inc. All rights reserved.  **
 **                                                                   **
 ** License to copy and use this software is granted provided that    **
 ** it is identified as the "RSA Data Security, Inc. MD5 Message-     **
 ** Digest Algorithm" in all material mentioning or referencing this  **
 ** software or this function.                                        **
 **                                                                   **
 ** License is also granted to make and use derivative works          **
 ** provided that such works are identified as "derived from the RSA  **
 ** Data Security, Inc. MD5 Message-Digest Algorithm" in all          **
 ** material mentioning or referencing the derived work.              **
 **                                                                   **
 ** RSA Data Security, Inc. makes no representations concerning       **
 ** either the merchantability of this software or the suitability    **
 ** of this software for any particular purpose.  It is provided "as  **
 ** is" without express or implied warranty of any kind.              **
 **                                                                   **
 ** These notices must be retained in any copies of any part of this  **
 ** documentation and/or software.                                    **
 ***********************************************************************
 */

// Data structure for MD5 (Message-Digest) computation

#define MD5_LEN             16

typedef struct
{
  ULONG i[ 2 ];             // number of _bits_ handled mod 2^64
  ULONG buf[ 4 ];           // scratch buffer
  UCHAR in[ 64 ];           // input buffer
  UCHAR digest[ MD5_LEN ];  // actual digest after MD5Final call
}
MD5_CTX;

// Constants for Transform routine.

#define S11 7
#define S12 12
#define S13 17
#define S14 22
#define S21 5
#define S22 9
#define S23 14
#define S24 20
#define S31 4
#define S32 11
#define S33 16
#define S34 23
#define S41 6
#define S42 10
#define S43 15
#define S44 21

static VOID TransformMD5 (ULONG *, ULONG *);

static const UCHAR PADDING[64] =
{
  0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
};

// F, G and H are basic MD5 functions

#define F(x, y, z) (((x) & (y)) | ((~x) & (z)))
#define G(x, y, z) (((x) & (z)) | ((y) & (~z)))
#define H(x, y, z) ((x) ^ (y) ^ (z))
#define I(x, y, z) ((y) ^ ((x) | (~z)))

// ROTATE_LEFT rotates x left n bits.

#define ROTATE_LEFT(x, n) (((x) << (n)) | ((x) >> (32-(n))))

// FF, GG, HH, and II transformations for rounds 1, 2, 3, and 4
// Rotation is separate from addition to prevent recomputation

#define FF(a, b, c, d, x, s, ac) \
  {(a) += F ((b), (c), (d)) + (x) + (ULONG)(ac); \
   (a) = ROTATE_LEFT ((a), (s)); \
   (a) += (b); \
  }
#define GG(a, b, c, d, x, s, ac) \
  {(a) += G ((b), (c), (d)) + (x) + (ULONG)(ac); \
   (a) = ROTATE_LEFT ((a), (s)); \
   (a) += (b); \
  }
#define HH(a, b, c, d, x, s, ac) \
  {(a) += H ((b), (c), (d)) + (x) + (ULONG)(ac); \
   (a) = ROTATE_LEFT ((a), (s)); \
   (a) += (b); \
  }
#define II(a, b, c, d, x, s, ac) \
  {(a) += I ((b), (c), (d)) + (x) + (ULONG)(ac); \
   (a) = ROTATE_LEFT ((a), (s)); \
   (a) += (b); \
  }

VOID MD5Init( MD5_CTX *mdContext )
{
  mdContext->i[0] = mdContext->i[1] = (ULONG)0;

  // Load magic initialization constants.

  mdContext->buf[0] = (ULONG)0x67452301;
  mdContext->buf[1] = (ULONG)0xefcdab89;
  mdContext->buf[2] = (ULONG)0x98badcfe;
  mdContext->buf[3] = (ULONG)0x10325476;
}

VOID MD5Update( MD5_CTX *mdContext, UCHAR const *inBuf, ULONG inLen )
{
ULONG   in[16];
INT     mdi;
ULONG   i, ii;

  /* compute number of bytes mod 64 */

  mdi = (int)((mdContext->i[0] >> 3) & 0x3f);

  /* update number of bits */

  if ((mdContext->i[0] + ((ULONG)inLen << 3)) < mdContext->i[0])
    mdContext->i[1]++;
  mdContext->i[0] += ((ULONG)inLen << 3);
  mdContext->i[1] += ((ULONG)inLen >> 29);

  while (inLen--) {
    /* add new character to buffer, increment mdi */
    mdContext->in[mdi++] = *inBuf++;

    /* transform if necessary */
    if (mdi == 0x40) {
      for (i = 0, ii = 0; i < 16; i++, ii += 4)
        in[i] = (((ULONG)mdContext->in[ii+3]) << 24) |
                (((ULONG)mdContext->in[ii+2]) << 16) |
                (((ULONG)mdContext->in[ii+1]) << 8) |
                ((ULONG)mdContext->in[ii]);
      TransformMD5 (mdContext->buf, in);
      mdi = 0;
    }
  }
}

VOID MD5Final (MD5_CTX *mdContext)
{
  ULONG in[16];
  INT mdi;
  ULONG i, ii;
  ULONG padLen;

  /* save number of bits */
  in[14] = mdContext->i[0];
  in[15] = mdContext->i[1];

  /* compute number of bytes mod 64 */
  mdi = (int)((mdContext->i[0] >> 3) & 0x3f);

  /* pad out to 56 mod 64 */
  padLen = (mdi < 56) ? (56 - mdi) : (120 - mdi);
  MD5Update (mdContext, PADDING, padLen);

  /* append length in bits and transform */
  for (i = 0, ii = 0; i < 14; i++, ii += 4)
    in[i] = (((ULONG)mdContext->in[ii+3]) << 24) |
            (((ULONG)mdContext->in[ii+2]) << 16) |
            (((ULONG)mdContext->in[ii+1]) << 8) |
            ((ULONG)mdContext->in[ii]);
  TransformMD5 (mdContext->buf, in);

  /* store buffer in digest */
  for (i = 0, ii = 0; i < 4; i++, ii += 4) {
    mdContext->digest[ii] = (UCHAR)(mdContext->buf[i] & 0xff);
    mdContext->digest[ii+1] =
      (UCHAR)((mdContext->buf[i] >> 8) & 0xff);
    mdContext->digest[ii+2] =
      (UCHAR)((mdContext->buf[i] >> 16) & 0xff);
    mdContext->digest[ii+3] =
      (UCHAR)((mdContext->buf[i] >> 24) & 0xff);
  }
}

/* Basic MD5 step. Transforms buf based on in.
 */
static VOID TransformMD5 (ULONG *buf, ULONG *in)
{
  ULONG a = buf[0], b = buf[1], c = buf[2], d = buf[3];

  /* Round 1 */
  FF ( a, b, c, d, in[ 0], S11, 0xd76aa478); /* 1 */
  FF ( d, a, b, c, in[ 1], S12, 0xe8c7b756); /* 2 */
  FF ( c, d, a, b, in[ 2], S13, 0x242070db); /* 3 */
  FF ( b, c, d, a, in[ 3], S14, 0xc1bdceee); /* 4 */
  FF ( a, b, c, d, in[ 4], S11, 0xf57c0faf); /* 5 */
  FF ( d, a, b, c, in[ 5], S12, 0x4787c62a); /* 6 */
  FF ( c, d, a, b, in[ 6], S13, 0xa8304613); /* 7 */
  FF ( b, c, d, a, in[ 7], S14, 0xfd469501); /* 8 */
  FF ( a, b, c, d, in[ 8], S11, 0x698098d8); /* 9 */
  FF ( d, a, b, c, in[ 9], S12, 0x8b44f7af); /* 10 */
  FF ( c, d, a, b, in[10], S13, 0xffff5bb1); /* 11 */
  FF ( b, c, d, a, in[11], S14, 0x895cd7be); /* 12 */
  FF ( a, b, c, d, in[12], S11, 0x6b901122); /* 13 */
  FF ( d, a, b, c, in[13], S12, 0xfd987193); /* 14 */
  FF ( c, d, a, b, in[14], S13, 0xa679438e); /* 15 */
  FF ( b, c, d, a, in[15], S14, 0x49b40821); /* 16 */

  /* Round 2 */
  GG ( a, b, c, d, in[ 1], S21, 0xf61e2562); /* 17 */
  GG ( d, a, b, c, in[ 6], S22, 0xc040b340); /* 18 */
  GG ( c, d, a, b, in[11], S23, 0x265e5a51); /* 19 */
  GG ( b, c, d, a, in[ 0], S24, 0xe9b6c7aa); /* 20 */
  GG ( a, b, c, d, in[ 5], S21, 0xd62f105d); /* 21 */
  GG ( d, a, b, c, in[10], S22,  0x2441453); /* 22 */
  GG ( c, d, a, b, in[15], S23, 0xd8a1e681); /* 23 */
  GG ( b, c, d, a, in[ 4], S24, 0xe7d3fbc8); /* 24 */
  GG ( a, b, c, d, in[ 9], S21, 0x21e1cde6); /* 25 */
  GG ( d, a, b, c, in[14], S22, 0xc33707d6); /* 26 */
  GG ( c, d, a, b, in[ 3], S23, 0xf4d50d87); /* 27 */
  GG ( b, c, d, a, in[ 8], S24, 0x455a14ed); /* 28 */
  GG ( a, b, c, d, in[13], S21, 0xa9e3e905); /* 29 */
  GG ( d, a, b, c, in[ 2], S22, 0xfcefa3f8); /* 30 */
  GG ( c, d, a, b, in[ 7], S23, 0x676f02d9); /* 31 */
  GG ( b, c, d, a, in[12], S24, 0x8d2a4c8a); /* 32 */

  /* Round 3 */
  HH ( a, b, c, d, in[ 5], S31, 0xfffa3942); /* 33 */
  HH ( d, a, b, c, in[ 8], S32, 0x8771f681); /* 34 */
  HH ( c, d, a, b, in[11], S33, 0x6d9d6122); /* 35 */
  HH ( b, c, d, a, in[14], S34, 0xfde5380c); /* 36 */
  HH ( a, b, c, d, in[ 1], S31, 0xa4beea44); /* 37 */
  HH ( d, a, b, c, in[ 4], S32, 0x4bdecfa9); /* 38 */
  HH ( c, d, a, b, in[ 7], S33, 0xf6bb4b60); /* 39 */
  HH ( b, c, d, a, in[10], S34, 0xbebfbc70); /* 40 */
  HH ( a, b, c, d, in[13], S31, 0x289b7ec6); /* 41 */
  HH ( d, a, b, c, in[ 0], S32, 0xeaa127fa); /* 42 */
  HH ( c, d, a, b, in[ 3], S33, 0xd4ef3085); /* 43 */
  HH ( b, c, d, a, in[ 6], S34,  0x4881d05); /* 44 */
  HH ( a, b, c, d, in[ 9], S31, 0xd9d4d039); /* 45 */
  HH ( d, a, b, c, in[12], S32, 0xe6db99e5); /* 46 */
  HH ( c, d, a, b, in[15], S33, 0x1fa27cf8); /* 47 */
  HH ( b, c, d, a, in[ 2], S34, 0xc4ac5665); /* 48 */

  /* Round 4 */
  II ( a, b, c, d, in[ 0], S41, 0xf4292244); /* 49 */
  II ( d, a, b, c, in[ 7], S42, 0x432aff97); /* 50 */
  II ( c, d, a, b, in[14], S43, 0xab9423a7); /* 51 */
  II ( b, c, d, a, in[ 5], S44, 0xfc93a039); /* 52 */
  II ( a, b, c, d, in[12], S41, 0x655b59c3); /* 53 */
  II ( d, a, b, c, in[ 3], S42, 0x8f0ccc92); /* 54 */
  II ( c, d, a, b, in[10], S43, 0xffeff47d); /* 55 */
  II ( b, c, d, a, in[ 1], S44, 0x85845dd1); /* 56 */
  II ( a, b, c, d, in[ 8], S41, 0x6fa87e4f); /* 57 */
  II ( d, a, b, c, in[15], S42, 0xfe2ce6e0); /* 58 */
  II ( c, d, a, b, in[ 6], S43, 0xa3014314); /* 59 */
  II ( b, c, d, a, in[13], S44, 0x4e0811a1); /* 60 */
  II ( a, b, c, d, in[ 4], S41, 0xf7537e82); /* 61 */
  II ( d, a, b, c, in[11], S42, 0xbd3af235); /* 62 */
  II ( c, d, a, b, in[ 2], S43, 0x2ad7d2bb); /* 63 */
  II ( b, c, d, a, in[ 9], S44, 0xeb86d391); /* 64 */

  buf[0] += a;
  buf[1] += b;
  buf[2] += c;
  buf[3] += d;
}

//
//  END OF MD5 REFERENCE IMPLEMENTATION
//


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  FakeGuidFromText{A|W}
//
static DWORD s_NullGuid[4] = { 0x0075004E, 0x006C006C, 0x00550047, 0x00440049 };

DASSERT_COMPILER( sizeof(GUID) == sizeof(s_NullGuid) );
DASSERT_COMPILER( sizeof(GUID) == MD5_LEN );


void FakeGuidFromTextA( GUID& rGuid, PCSTR str, int nLen )
{
    if( str == NULL || nLen == 0 || 0 == (nLen = GetStrLenA(str, nLen)) )
    {
        YAWL_CopyMemory( &rGuid, s_NullGuid, sizeof(GUID) );
    }
    else
    {
        MD5_CTX md5Ctx;

        MD5Init  ( &md5Ctx );
        MD5Update( &md5Ctx, (UCHAR const*)str, (ULONG)(nLen * sizeof(CHAR)) );
        MD5Final ( &md5Ctx );

        YAWL_CopyMemory( &rGuid, md5Ctx.digest, sizeof(GUID) );
    }

} // FakeGuidFromTextA


void FakeGuidFromTextW(GUID& rGuid, PCWSTR str, int nLen)
{
    if( str == NULL || nLen == 0 || 0 == (nLen = GetStrLenW(str, nLen)) )
    {
        YAWL_CopyMemory( &rGuid, s_NullGuid, sizeof(GUID) );
    }
    else
    {
        MD5_CTX md5Ctx;

        MD5Init  ( &md5Ctx );
        MD5Update( &md5Ctx, (UCHAR const*)str, (ULONG)(nLen * sizeof(WCHAR)) );
        MD5Final ( &md5Ctx );

        YAWL_CopyMemory( &rGuid, md5Ctx.digest, sizeof(GUID) );
    }

} // FakeGuidFromTextW


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                 End of file "Hashing.cpp"                                   //
/////////////////////////////////////////////////////////////////////////////////////////////////
