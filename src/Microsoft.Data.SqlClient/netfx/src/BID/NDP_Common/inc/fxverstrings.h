#ifndef VER_PRODUCTNAME_STR
    #if defined(CSC_INVOKED)
        #if defined(PPTARGET_VB)
            #define VER_PRODUCTNAME_STR      ("Microsoft" + Microsoft.VisualBasic.ChrW(174) + " .NET Framework")
        #elif defined(PPTARGET_CS) || defined(PPTARGET_JS) || defined(FEATURE_PAL)
            #define VER_PRODUCTNAME_STR      "Microsoft\u00AE .NET Framework"
        #else
            #error Unknown language when defining VER_PRODUCTNAME_STR!
        #endif
    #elif defined(PLIST_INVOKED)
        #define VER_PRODUCTNAME Microsoft® .NET Framework
    #else
        #define VER_PRODUCTNAME_STR      L"Microsoft\256 .NET Framework"
    #endif
#endif

// The following copyright is intended for display in the Windows Explorer property box for a DLL or EXE
// See \\lca\pdm\TMGUIDE\Copyright\Crt_Tmk_Notices.xls for copyright guidelines
//
#ifndef VER_LEGALCOPYRIGHT_STR
    #if defined(CSC_INVOKED)
        #if defined(PPTARGET_VB)
            #define VER_LEGALCOPYRIGHT_STR      Microsoft.VisualBasic.ChrW(169) + " Microsoft Corporation.  All rights reserved."
        #elif defined(PPTARGET_CS) || defined(PPTARGET_JS) || defined(FEATURE_PAL)
            #define VER_LEGALCOPYRIGHT_STR      "\u00A9 Microsoft Corporation.  All rights reserved."
        #else
            #error Unknown language when defining VER_LEGALCOPYRIGHT_STR!
        #endif
    #elif defined(PLIST_INVOKED)
        // EMPTY is to force multiple spaces when the preprocessor would combine them otherwise.
        // It gets editted out post-processing.
        #define VER_LEGALCOPYRIGHT © Microsoft Corporation. EMPTY All rights reserved.
    #else
        #define VER_LEGALCOPYRIGHT_STR      "\251 Microsoft Corporation.  All rights reserved."
        #define VER_LEGALCOPYRIGHT_STR_L   L"\251 Microsoft Corporation.  All rights reserved."
    #endif
#endif

// VSWhidbey #495749
// Note: The following legal copyright is intended for situations where the copyright symbol doesn't display 
//       properly.  For example, the following copyright should be displayed as part of the logo for DOS command-line 
//       applications.  If you change the format or wording of the following copyright, you should apply the same
//       change to fxresstrings.txt (for managed applications).
#ifndef VER_LEGALCOPYRIGHT_LOGO_STR
    #define VER_LEGALCOPYRIGHT_LOGO_STR    "Copyright (c) Microsoft Corporation.  All rights reserved."
    #define VER_LEGALCOPYRIGHT_LOGO_STR_L L"Copyright (c) Microsoft Corporation.  All rights reserved."
#endif
