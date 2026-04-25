// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Data.SqlClient;

internal static class LocalesHelper
{
    private const int LocaleMappingCount = 207;

    /// <summary>
    /// Array copied directly from tdssort.h from luxor.
    /// Use the sort ID as an index into this array to retrieve the code page.
    /// If the value is zero, the index is not a valid sort ID.
    /// </summary>
    private static ReadOnlySpan<ushort> SortIdToCodePageMappings => [
        // 0-29: reserved
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        // 30-35
        437, 437, 437, 437, 437, 437,
        // 36-39: reserved
        0, 0, 0, 0,
        // 40-45
        850, 850, 850, 850, 850, 850,
        // 46-48: reserved
        0, 0, 0,
        // 49-61
        850, 1252, 1252, 1252, 1252, 1252, 850, 850, 850, 850, 850, 850, 850,
        // 62-70: reserved
        0, 0, 0, 0, 0, 0, 0, 0, 0,
        // 71-75
        1252, 1252, 1252, 1252, 1252,
        // 76-79: reserved
        0, 0, 0, 0,
        // 80-98
        1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250, 1250,
        // 99-103: reserved
        0, 0, 0, 0, 0,
        // 104-108
        1251, 1251, 1251, 1251, 1251,
        // 109-111: reserved
        0, 0, 0,
        // 112-114
        1253, 1253, 1253,
        // 115-119: reserved
        0, 0, 0, 0, 0,
        // 120-122
        1253, 1253, 1253,
        // 123: reserved
        0,
        // 124
        1253,
        // 125-127: reserved
        0, 0, 0,
        // 128-130
        1254, 1254, 1254,
        // 131-135: reserved
        0, 0, 0, 0, 0,
        // 136-138
        1255, 1255, 1255,
        // 139-143: reserved
        0, 0, 0, 0, 0,
        // 144-146
        1256, 1256, 1256,
        // 147-151: reserved
        0, 0, 0, 0, 0,
        // 152-160
        1257, 1257, 1257, 1257, 1257, 1257, 1257, 1257, 1257,
        // 161-182: reserved
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        // 183-186
        1252, 1252, 1252, 1252,
        // 187-191: reserved
        0, 0, 0, 0, 0,
        // 192-206
        932, 932, 949, 949, 950, 950, 936, 936, 932, 949, 950, 936, 874, 874, 874,
        // 207-209: reserved
        0, 0, 0,
        // 210-217
        1252, 1252, 1252, 1252, 1252, 1252, 1252, 1252,
        // 218-255
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    /// <summary>
    /// Maps LCIDs to code pages. Each pair of values in the array represents an LCID and its corresponding code page.
    /// This means that even indexes in the array are LCIDs, and the following odd index is the code page for that LCID.
    /// </summary>
    /// <remarks>
    /// This is aligned with the mssql-jdbc driver. See the following file for the original data:
    /// https://github.com/microsoft/mssql-jdbc/blob/23e935b846af5f112f445289e89d54a379e8b8ed/src/main/java/com/microsoft/sqlserver/jdbc/SQLCollation.java#L141-L361
    /// </remarks>
    private static ReadOnlySpan<int> LcidToCodePageMappings => [
        // ar_SA         bg_BG            ca_ES            zh_TW            cs_CZ            da_DK            de_DE            el_GR
        0x0401, 1256,    0x0402, 1251,    0x0403, 1252,    0x0404, 950,     0x0405, 1250,    0x0406, 1252,    0x0407, 1252,    0x0408, 1253,

        // en_US         es_ES_tradnl     fi_FI            fr_FR            he_IL            hu_HU            is_IS            it_IT
        0x0409, 1252,    0x040a, 1252,    0x040b, 1252,    0x040c, 1252,    0x040d, 1255,    0x040e, 1250,    0x040f, 1252,    0x0410, 1252,

        // ja_JP         ko_KR            nl_NL            nb_NO            pl_PL            pt_BR            rm_CH            ro_RO
        0x0411, 932,     0x0412, 949,     0x0413, 1252,    0x0414, 1252,    0x0415, 1250,    0x0416, 1252,    0x0417, 1252,    0x0418, 1250,

        // ru_RU         hr_HR            sk_SK            sq_AL            sv_SE            th_TH            tr_TR            ur_PK
        0x0419, 1251,    0x041a, 1250,    0x041b, 1250,    0x041c, 1250,    0x041d, 1252,    0x041e, 874,     0x041f, 1254,    0x0420, 1256,

        // id_ID         uk_UA            be_BY            sl_SI            et_EE            lv_LV            lt_LT            tg_Cyrl_TJ
        0x0421, 1252,    0x0422, 1251,    0x0423, 1251,    0x0424, 1250,    0x0425, 1257,    0x0426, 1257,    0x0427, 1257,    0x0428, 1251,

        // fa_IR         vi_VN            hy_AM            az_Latn_AZ       eu_ES            wen_DE           mk_MK            tn_ZA
        0x0429, 1256,    0x042a, 1258,    0x042b, 1252,    0x042c, 1254,    0x042d, 1252,    0x042e, 1252,    0x042f, 1251,    0x0432, 1252,

        // xh_ZA         zu_ZA            af_ZA            ka_GE            fo_FO            hi_IN            mt_MT            se_NO
        0x0434, 1252,    0x0435, 1252,    0x0436, 1252,    0x0437, 1252,    0x0438, 1252,    0x0439, 1200,    0x043a, 1200,    0x043b, 1252,

        // ms_MY         kk_KZ            ky_KG            sw_KE            tk_TM            uz_Latn_UZ       tt_RU            bn_IN
        0x043e, 1252,    0x043f, 1251,    0x0440, 1251,    0x0441, 1252,    0x0442, 1250,    0x0443, 1254,    0x0444, 1251,    0x0445, 1200,

        // pa_IN         gu_IN            or_IN            ta_IN            te_IN            kn_IN            ml_IN            as_IN
        0x0446, 1200,    0x0447, 1200,    0x0448, 1200,    0x0449, 1200,    0x044a, 1200,    0x044b, 1200,    0x044c, 1200,    0x044d, 1200,

        // mr_IN         sa_IN            mn_MN            bo_CN            cy_GB            km_KH            lo_LA            gl_ES
        0x044e, 1200,    0x044f, 1200,    0x0450, 1251,    0x0451, 1200,    0x0452, 1252,    0x0453, 1200,    0x0454, 1200,    0x0456, 1252,

        // kok_IN        syr_SY           si_LK            iu_Cans_CA       am_ET            ne_NP            fy_NL            ps_AF
        0x0457, 1200,    0x045a, 1200,    0x045b, 1200,    0x045d, 1252,    0x045e, 1252,    0x0461, 1200,    0x0462, 1252,    0x0463, 1200,

        // fil_PH        dv_MV            ha_Latn_NG       yo_NG            quz_BO           nso_ZA           ba_RU            lb_LU
        0x0464, 1252,    0x0465, 1200,    0x0468, 1252,    0x046a, 1252,    0x046b, 1252,    0x046c, 1252,    0x046d, 1251,    0x046e, 1252,

        // kl_GL         ig_NG            ii_CN            arn_CL           moh_CA           br_FR            ug_CN            mi_NZ
        0x046f, 1252,    0x0470, 1252,    0x0478, 1252,    0x047a, 1252,    0x047c, 1252,    0x047e, 1252,    0x0480, 1256,    0x0481, 1200,

        // oc_FR         co_FR            gsw_FR           sah_RU           qut_GT           rw_RW            wo_SN            prs_AF
        0x0482, 1252,    0x0483, 1252,    0x0484, 1252,    0x0485, 1251,    0x0486, 1252,    0x0487, 1252,    0x0488, 1252,    0x048c, 1256,

        // ar_IQ         zh_CN            de_CH            en_GB            es_MX            fr_BE            it_CH            nl_BE
        0x0801, 1256,    0x0804, 936,     0x0807, 1252,    0x0809, 1252,    0x080a, 1252,    0x080c, 1252,    0x0810, 1252,    0x0813, 1252,

        // nn_NO         pt_PT            sr_Latn_CS       sv_FI            Lithuanian_Classic  az_Cyrl_AZ    dsb_DE           se_SE
        0x0814, 1252,    0x0816, 1252,    0x081a, 1250,    0x081d, 1252,    0x0827, 1257,    0x082c, 1251,    0x082e, 1252,    0x083b, 1252,

        // ga_IE         ms_BN            uz_Cyrl_UZ       bn_BD            mn_Mong_CN       iu_Latn_CA       tzm_Latn_DZ      quz_EC
        0x083c, 1252,    0x083e, 1252,    0x0843, 1251,    0x0845, 1200,    0x0850, 1251,    0x085d, 1252,    0x085f, 1252,    0x086b, 1252,

        // ar_EG         zh_HK            de_AT            en_AU            es_ES            fr_CA            sr_Cyrl_CS       se_FI
        0x0c01, 1256,    0x0c04, 950,     0x0c07, 1252,    0x0c09, 1252,    0x0c0a, 1252,    0x0c0c, 1252,    0x0c1a, 1251,    0x0c3b, 1252,

        // quz_PE        ar_LY            zh_SG            de_LU            en_CA            es_GT            fr_CH            hr_BA
        0x0c6b, 1252,    0x1001, 1256,    0x1004, 936,     0x1007, 1252,    0x1009, 1252,    0x100a, 1252,    0x100c, 1252,    0x101a, 1250,

        // smj_NO        ar_DZ            zh_MO            de_LI            en_NZ            es_CR            fr_LU            bs_Latn_BA
        0x103b, 1252,    0x1401, 1256,    0x1404, 950,     0x1407, 1252,    0x1409, 1252,    0x140a, 1252,    0x140c, 1252,    0x141a, 1250,

        // smj_SE        ar_MA            en_IE            es_PA            fr_MC            sr_Latn_BA       sma_NO           ar_TN
        0x143b, 1252,    0x1801, 1256,    0x1809, 1252,    0x180a, 1252,    0x180c, 1252,    0x181a, 1250,    0x183b, 1252,    0x1c01, 1256,

        // en_ZA         es_DO            sr_Cyrl_BA       sma_SB           ar_OM            en_JM            es_VE            bs_Cyrl_BA
        0x1c09, 1252,    0x1c0a, 1252,    0x1c1a, 1251,    0x1c3b, 1252,    0x2001, 1256,    0x2009, 1252,    0x200a, 1252,    0x201a, 1251,

        // sms_FI        ar_YE            en_CB            es_CO            smn_FI           ar_SY            en_BZ            es_PE
        0x203b, 1252,    0x2401, 1256,    0x2409, 1252,    0x240a, 1252,    0x243b, 1252,    0x2801, 1256,    0x2809, 1252,    0x280a, 1252,

        // ar_JO         en_TT            es_AR            ar_LB            en_ZW            es_EC            ar_KW            en_PH
        0x2c01, 1256,    0x2c09, 1252,    0x2c0a, 1252,    0x3001, 1256,    0x3009, 1252,    0x300a, 1252,    0x3401, 1256,    0x3409, 1252,

        // es_CL         ar_AE            es_UY            ar_BH            es_PY            ar_QA            en_IN            es_BO
        0x340a, 1252,    0x3801, 1256,    0x380a, 1252,    0x3c01, 1256,    0x3c0a, 1252,    0x4001, 1256,    0x4009, 1252,    0x400a, 1252,

        // en_MY         es_SV            en_SG            es_HN            es_NI            es_PR            es_US
        0x4409, 1252,    0x440a, 1252,    0x4809, 1252,    0x480a, 1252,    0x4c0a, 1252,    0x500a, 1252,    0x540a, 1252,
    ];

    public static bool TryGetCodePage(int lcid, int sortId, out int codePage)
    {
        if (sortId != 0)
        {
            codePage = (uint)sortId < SortIdToCodePageMappings.Length
                ? SortIdToCodePageMappings[sortId]
                : 0;

            Debug.Assert(codePage >= 0, $"TryGetCodePage accessed codepage data and producted 0! sortId = {sortId}");
        }
        else
        {
            codePage = GetCodePageByLcid(lcid & 0xFFFF);
        }
        return codePage != 0;
    }

    private static int GetCodePageByLcid(int lcid)
    {
        Debug.Assert(LcidToCodePageMappings.Length == LocaleMappingCount * 2);

        ReadOnlySpan<int> lcidMappings = LcidToCodePageMappings;
        int mappingIndex = lcidMappings.IndexOf(lcid);

        // If LCID is not found, or if it's found at an odd index (which would be a code page, not an LCID), return zero
        // to indicate that the code page could not be found.
        // Also include an explicit bounds check to ensure that the method doesn't contain any exception paths.
        if (mappingIndex == -1 || (mappingIndex % 2) != 0 || ((uint)mappingIndex + 1) >= lcidMappings.Length)
        {
            return 0;
        }

        return lcidMappings[mappingIndex + 1];
    }
}
