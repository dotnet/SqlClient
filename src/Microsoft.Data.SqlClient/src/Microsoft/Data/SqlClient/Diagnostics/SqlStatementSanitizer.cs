// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.Data.SqlClient.Diagnostics;

/// <summary>
/// Bounded lexical masking for opt-in db.query.text, ported from the JDBC OTel module.
/// Literals and numbers become '?', comments are removed, and whitespace is collapsed.
/// Text that cannot be masked safely (unterminated literal/comment, EXEC arguments, or
/// oversize input/output) yields null so that no partially masked text is exported.
/// </summary>
internal static class SqlStatementSanitizer
{
    internal const int MaximumInputLength = 16384;
    internal const int MaximumOutputLength = 4096;

    internal static string Mask(string sql)
    {
        if (string.IsNullOrEmpty(sql) || sql.Length > MaximumInputLength)
        {
            return null;
        }

        string trimmed = sql.Trim();
        if (StartsWithKeyword(trimmed, "exec") || StartsWithKeyword(trimmed, "execute"))
        {
            return null;
        }

        StringBuilder output = new(Math.Min(sql.Length, MaximumOutputLength));
        int length = sql.Length;
        int i = 0;
        while (i < length)
        {
            if (output.Length >= MaximumOutputLength)
            {
                return null;
            }

            char current = sql[i];
            if (current == '-' && i + 1 < length && sql[i + 1] == '-')
            {
                i += 2;
                while (i < length && sql[i] != '\n' && sql[i] != '\r')
                {
                    i++;
                }
                AppendSpace(output);
            }
            else if (current == '/' && i + 1 < length && sql[i + 1] == '*')
            {
                i = SkipBlockComment(sql, i + 2);
                if (i < 0)
                {
                    return null;
                }
                AppendSpace(output);
            }
            else if ((current == 'N' || current == 'n') && i + 1 < length && sql[i + 1] == '\'' &&
                (i == 0 || !IsIdentifierPart(sql[i - 1])))
            {
                AppendMask(output);
                i = SkipDelimited(sql, i + 1, '\'');
                if (i < 0)
                {
                    return null;
                }
            }
            else if (current == '\'' || current == '"')
            {
                // Double-quoted text may be a literal when QUOTED_IDENTIFIER is off.
                AppendMask(output);
                i = SkipDelimited(sql, i, current);
                if (i < 0)
                {
                    return null;
                }
            }
            else if (current == '[')
            {
                int end = SkipDelimited(sql, i, ']');
                if (end < 0 || output.Length + end - i > MaximumOutputLength)
                {
                    return null;
                }
                output.Append(sql, i, end - i);
                i = end;
            }
            else if (IsNumberStart(sql, i))
            {
                AppendMask(output);
                i = SkipNumber(sql, i);
            }
            else if (char.IsWhiteSpace(current))
            {
                AppendSpace(output);
                i++;
            }
            else
            {
                output.Append(current);
                i++;
            }
        }

        string masked = output.ToString().Trim();
        return masked.Length == 0 ? null : masked;
    }

    private static bool StartsWithKeyword(string text, string keyword) =>
        text.StartsWith(keyword, StringComparison.OrdinalIgnoreCase) &&
        (text.Length == keyword.Length || char.IsWhiteSpace(text[keyword.Length]));

    private static int SkipBlockComment(string sql, int index)
    {
        int depth = 1;
        while (index < sql.Length && depth > 0)
        {
            if (index + 1 < sql.Length && sql[index] == '/' && sql[index + 1] == '*')
            {
                depth++;
                index += 2;
            }
            else if (index + 1 < sql.Length && sql[index] == '*' && sql[index + 1] == '/')
            {
                depth--;
                index += 2;
            }
            else
            {
                index++;
            }
        }
        return depth == 0 ? index : -1;
    }

    // Returns the index after the closing delimiter; a doubled delimiter is an escape.
    private static int SkipDelimited(string sql, int start, char close)
    {
        for (int i = start + 1; i < sql.Length; i++)
        {
            if (sql[i] == close)
            {
                if (i + 1 < sql.Length && sql[i + 1] == close)
                {
                    i++;
                }
                else
                {
                    return i + 1;
                }
            }
        }
        return -1;
    }

    private static bool IsNumberStart(string sql, int index)
    {
        char value = sql[index];
        if (!IsDigit(value) &&
            !(value == '.' && index + 1 < sql.Length && IsDigit(sql[index + 1])))
        {
            return false;
        }
        return index == 0 || !IsIdentifierPart(sql[index - 1]);
    }

    private static int SkipNumber(string sql, int index)
    {
        if (index + 1 < sql.Length && sql[index] == '0' &&
            (sql[index + 1] == 'x' || sql[index + 1] == 'X'))
        {
            index += 2;
            while (index < sql.Length && Uri.IsHexDigit(sql[index]))
            {
                index++;
            }
            return index;
        }

        bool exponent = false;
        while (index < sql.Length)
        {
            char value = sql[index];
            if (IsDigit(value) || value == '.')
            {
                index++;
            }
            else if (!exponent && (value == 'e' || value == 'E'))
            {
                exponent = true;
                index++;
                if (index < sql.Length && (sql[index] == '+' || sql[index] == '-'))
                {
                    index++;
                }
            }
            else
            {
                break;
            }
        }
        return index;
    }

    private static bool IsDigit(char value) => value >= '0' && value <= '9';

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(value) || value == '_' || value == '$' || value == '#' ||
        value == '@';

    private static void AppendMask(StringBuilder output)
    {
        if (output.Length == 0 || output[output.Length - 1] != '?')
        {
            output.Append('?');
        }
    }

    private static void AppendSpace(StringBuilder output)
    {
        if (output.Length > 0 && output[output.Length - 1] != ' ')
        {
            output.Append(' ');
        }
    }
}
