// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Microsoft.Data.Common.ConnectionString;

namespace Microsoft.Data.Common
{
    internal partial class DbConnectionOptions
    {
        internal string ExpandAttachDbFileName(string replacementValue)
        {
            int copyPosition = 0;

            StringBuilder builder = new(_usersConnectionString.Length);
            for (NameValuePair current = _keyChain; current != null; current = current.Next)
            {
                if (string.Equals(current.Name, DbConnectionStringKeywords.AttachDBFilename, StringComparison.InvariantCultureIgnoreCase))
                {
                    builder.Append($"{current.Name}={replacementValue};");
                }
                else
                {
                    builder.Append(_usersConnectionString, copyPosition, current.Length);
                }
                copyPosition += current.Length;
            }

            return builder.ToString();
        }

        // SxS notes:
        // * this method queries "DataDirectory" value from the current AppDomain.
        //   This string is used for to replace "!DataDirectory!" values in the connection string, it is not considered as an "exposed resource".
        // * This method uses GetFullPath to validate that root path is valid, the result is not exposed out.
        internal static string ExpandDataDirectory(string keyword, string value)
        {
            string fullPath = null;
            if (value != null && value.StartsWith(DataDirectory, StringComparison.OrdinalIgnoreCase))
            {
                // find the replacement path
                object rootFolderObject = AppDomain.CurrentDomain.GetData("DataDirectory");
                var rootFolderPath = (rootFolderObject as string);
                if (rootFolderObject != null && rootFolderPath == null)
                {
                    throw ADP.InvalidDataDirectory();
                }
                else if (string.IsNullOrEmpty(rootFolderPath))
                {
                    rootFolderPath = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
                }

                var fileName = value.Substring(DataDirectory.Length);

                if (Path.IsPathRooted(fileName))
                {
                    fileName = fileName.TrimStart(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                }

                fullPath = Path.Combine(rootFolderPath, fileName);

                // verify root folder path is a real path without unexpected "..\"
                if (!Path.GetFullPath(fullPath).StartsWith(rootFolderPath, StringComparison.Ordinal))
                {
                    throw ADP.InvalidConnectionOptionValue(keyword);
                }
            }
            return fullPath;
        }

    }
}
