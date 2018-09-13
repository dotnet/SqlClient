//****************************************************************************
//              Copyright (c) 1988-2003 Microsoft Corporation.
//
// @File: sqlhdr.h
// @Owner: sharads, petergv
// @test: 
//
// <owner current="true" primary="true">sharads</owner>
// <owner current="true" primary="false">petergv</owner>
//
// Purpose: SQL Header
//
// Notes:
//	
// @EndHeader@
//****************************************************************************
#ifndef _SQLHDR_H_
#define _SQLHDR_H_

#define SNIX 1

#ifndef SNIX

#ifdef DEBUG
#define SOSHOST_USE_MALLOCSPY
#endif

#else 	// ifndef SNIX

#ifndef CPL_ASSERT
#define CPL_ASSERT(exp) typedef char __CPL_ASSERT__[(exp)?1:-1]
#endif

#define STRSAFE_LOCALE_FUNCTIONS 1

#define OACR_WARNING_PUSH __pragma ( prefast( push ) )
#define OACR_WARNING_POP __pragma ( prefast( pop ) )
#define OACR_WARNING_ENABLE( cWarning, comment ) __pragma ( prefast( enable: __WARNING_##cWarning, comment ) )
#define OACR_WARNING_DISABLE( cWarning, comment ) __pragma ( prefast( disable: __WARNING_##cWarning, comment ) )
#define OACR_WARNING_SUPPRESS( cWarning, comment ) __pragma ( prefast( suppress: __WARNING_##cWarning, comment) )

#endif 	// ifndef SNIX

#define SNI_BASED_CLIENT 1
#endif	// ifndef _SQLHDR_H_
