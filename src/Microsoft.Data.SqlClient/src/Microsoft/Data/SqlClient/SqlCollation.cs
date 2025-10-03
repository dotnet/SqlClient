// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.SqlTypes;
using System.Diagnostics;
using Microsoft.Data.SqlTypes;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlCollation
    {
        // First 20 bits of info field represent the lcid, bits 21-25 are compare options
        private const uint IgnoreCase = 1 << 20; // bit 21 - IgnoreCase
        private const uint IgnoreNonSpace = 1 << 21; // bit 22 - IgnoreNonSpace / IgnoreAccent
        private const uint IgnoreWidth = 1 << 22; // bit 23 - IgnoreWidth
        private const uint IgnoreKanaType = 1 << 23; // bit 24 - IgnoreKanaType
        private const uint BinarySort = 1 << 24; // bit 25 - BinarySort

        internal const uint MaskLcid = 0xfffff;
        private const int LcidVersionBitOffset = 28;
        private const uint MaskLcidVersion = unchecked((uint)(0xf << LcidVersionBitOffset));
        private const uint MaskCompareOpt = IgnoreCase | IgnoreNonSpace | IgnoreWidth | IgnoreKanaType | BinarySort;

        internal readonly uint _info;
        internal readonly byte _sortId;

        public SqlCollation(uint info, byte sortId)
        {
            _info = info;
            _sortId = sortId;
        }

        internal int LCID
        {
            // First 20 bits of info field represent the lcid
            get
            {
                return unchecked((int)(_info & MaskLcid));
            }
        }

        internal SqlCompareOptions SqlCompareOptions
        {
            get
            {
                SqlCompareOptions options = SqlCompareOptions.None;
                if (0 != (_info & IgnoreCase))
                {
                    options |= SqlCompareOptions.IgnoreCase;
                }

                if (0 != (_info & IgnoreNonSpace))
                {
                    options |= SqlCompareOptions.IgnoreNonSpace;
                }

                if (0 != (_info & IgnoreWidth))
                {
                    options |= SqlCompareOptions.IgnoreWidth;
                }

                if (0 != (_info & IgnoreKanaType))
                {
                    options |= SqlCompareOptions.IgnoreKanaType;
                }

                if (0 != (_info & BinarySort))
                {
                    options |= SqlCompareOptions.BinarySort;
                }

                return options;
            }
        }

        internal bool IsUTF8 => (_info & TdsEnums.UTF8_IN_TDSCOLLATION) == TdsEnums.UTF8_IN_TDSCOLLATION;

        internal string TraceString()
        {
            return string.Format(/*IFormatProvider*/ null, "(LCID={0}, Opts={1})", LCID, (int)SqlCompareOptions);
        }

        private static int FirstSupportedCollationVersion(int lcid)
        {
            // NOTE: switch-case works ~3 times faster in this case than search with Dictionary
            switch (lcid)
            {
                case 1044:
                    return 2; // Norwegian_100_BIN
                case 1047:
                    return 2; // Romansh_100_BIN
                case 1056:
                    return 2; // Urdu_100_BIN
                case 1065:
                    return 2; // Persian_100_BIN
                case 1068:
                    return 2; // Azeri_Latin_100_BIN
                case 1070:
                    return 2; // Upper_Sorbian_100_BIN
                case 1071:
                    return 1; // Macedonian_FYROM_90_BIN
                case 1081:
                    return 1; // Indic_General_90_BIN
                case 1082:
                    return 2; // Maltese_100_BIN
                case 1083:
                    return 2; // Sami_Norway_100_BIN
                case 1087:
                    return 1; // Kazakh_90_BIN
                case 1090:
                    return 2; // Turkmen_100_BIN
                case 1091:
                    return 1; // Uzbek_Latin_90_BIN
                case 1092:
                    return 1; // Tatar_90_BIN
                case 1093:
                    return 2; // Bengali_100_BIN
                case 1101:
                    return 2; // Assamese_100_BIN
                case 1105:
                    return 2; // Tibetan_100_BIN
                case 1106:
                    return 2; // Welsh_100_BIN
                case 1107:
                    return 2; // Khmer_100_BIN
                case 1108:
                    return 2; // Lao_100_BIN
                case 1114:
                    return 1; // Syriac_90_BIN
                case 1121:
                    return 2; // Nepali_100_BIN
                case 1122:
                    return 2; // Frisian_100_BIN
                case 1123:
                    return 2; // Pashto_100_BIN
                case 1125:
                    return 1; // Divehi_90_BIN
                case 1133:
                    return 2; // Bashkir_100_BIN
                case 1146:
                    return 2; // Mapudungan_100_BIN
                case 1148:
                    return 2; // Mohawk_100_BIN
                case 1150:
                    return 2; // Breton_100_BIN
                case 1152:
                    return 2; // Uighur_100_BIN
                case 1153:
                    return 2; // Maori_100_BIN
                case 1155:
                    return 2; // Corsican_100_BIN
                case 1157:
                    return 2; // Yakut_100_BIN
                case 1164:
                    return 2; // Dari_100_BIN
                case 2074:
                    return 2; // Serbian_Latin_100_BIN
                case 2092:
                    return 2; // Azeri_Cyrillic_100_BIN
                case 2107:
                    return 2; // Sami_Sweden_Finland_100_BIN
                case 2143:
                    return 2; // Tamazight_100_BIN
                case 3076:
                    return 1; // Chinese_Hong_Kong_Stroke_90_BIN
                case 3098:
                    return 2; // Serbian_Cyrillic_100_BIN
                case 5124:
                    return 2; // Chinese_Traditional_Pinyin_100_BIN
                case 5146:
                    return 2; // Bosnian_Latin_100_BIN
                case 8218:
                    return 2; // Bosnian_Cyrillic_100_BIN

                default:
                    return 0;   // other LCIDs have collation with version 0
            }
        }

        internal static bool Equals(SqlCollation a, SqlCollation b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }
            else
            {
                return a._info == b._info && a._sortId == b._sortId;
            }
        }

        internal static bool Equals(SqlCollation collation, uint info, byte sortId)
        {
            if (collation is not null)
            {
                return collation._info == info && collation._sortId == sortId;
            }
            return false;
        }

        public static SqlCollation FromLCIDAndSort(int lcid, SqlCompareOptions sqlCompareOptions)
        {
            uint info = 0;
            byte sortId = 0;
            
            Debug.Assert((sqlCompareOptions & SqlTypeWorkarounds.SqlStringValidSqlCompareOptionMask) == sqlCompareOptions, "invalid set_SqlCompareOptions value");
            uint compare = 0;
            if ((sqlCompareOptions & SqlCompareOptions.IgnoreCase) == SqlCompareOptions.IgnoreCase)
            {
                compare |= IgnoreCase;
            }
            if ((sqlCompareOptions & SqlCompareOptions.IgnoreNonSpace) == SqlCompareOptions.IgnoreNonSpace)
            {
                compare |= IgnoreNonSpace;
            }
            if ((sqlCompareOptions & SqlCompareOptions.IgnoreWidth) == SqlCompareOptions.IgnoreWidth)
            {
                compare |= IgnoreWidth;
            }
            if ((sqlCompareOptions & SqlCompareOptions.IgnoreKanaType) == SqlCompareOptions.IgnoreKanaType)
            {
                compare |= IgnoreKanaType;
            }
            if ((sqlCompareOptions & SqlCompareOptions.BinarySort) == SqlCompareOptions.BinarySort)
            {
                compare |= BinarySort;
            }
            info = (info & MaskLcid) | compare;
            
            int lcidValue = lcid & (int)MaskLcid;
            Debug.Assert(lcidValue == lcid, "invalid set_LCID value");

            // Some 2008 LCIDs do not have collation with version = 0
            // since user has no way to specify collation version, we set the first (minimal) supported version for these collations
            int versionBits = FirstSupportedCollationVersion(lcidValue) << LcidVersionBitOffset;
            Debug.Assert((versionBits & MaskLcidVersion) == versionBits, "invalid version returned by FirstSupportedCollationVersion");

            // combine the current compare options with the new locale ID and its first supported version
            info = (info & MaskCompareOpt) | unchecked((uint)lcidValue) | unchecked((uint)versionBits);
            
            return new SqlCollation(info, sortId);
        }
    }
}
