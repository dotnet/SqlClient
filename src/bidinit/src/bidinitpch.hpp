//****************************************************************************
//              Copyright (c) Microsoft Corporation.
//
// @File: bidinitpch.hpp
// @Owner: mattn
// @Test: zlin
//
// <owner current="true" primary="true">adoprov</owner>
//
// Purpose: bidinit precompiled header.
//
// Notes: 
//          
// @EndHeader@
//****************************************************************************

// keep this header file - bidinit.lib is built as UNICODE, while SNIX as ASCII

#pragma once

#ifndef UNICODE
#define  UNICODE    // Enable UNICODE.
#endif

#ifndef _UNICODE
#define _UNICODE    // Enable UNICODE runtime library routines.
#endif

#include <windows.h>
#include <assert.h>		// assert

#define _BID_UNICODE_LOADER
#define _BID_EXPLICIT_EXPORT

#include "BidApi.h"
#include "BidApiEx.h"
