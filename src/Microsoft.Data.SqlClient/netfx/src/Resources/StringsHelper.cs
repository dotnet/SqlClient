// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace Microsoft.Data
{
    internal partial class StringsHelper : Strings
    {
        private static StringsHelper s_loader = null;
        private readonly ResourceManager _resources;

        internal StringsHelper()
        {
            _resources = new ResourceManager("SqlClient.Resources.Strings", GetType().Assembly);
        }

        private static StringsHelper GetLoader()
        {
            if (s_loader == null)
            {
                StringsHelper sr = new();
                Interlocked.CompareExchange(ref s_loader, sr, null);
            }
            return s_loader;
        }

        public static string GetResourceString(string res)
        {
            StringsHelper sys = GetLoader();
            if (sys == null)
                return null;

            // If "res" is a resource id, temp will not be null, "res" will contain the retrieved resource string.
            // If "res" is not a resource id, temp will be null.
            string temp = sys._resources.GetString(res, StringsHelper.Culture);
            if (temp != null)
                res = temp;

            return res;
        }

        public static string GetString(string res, params object[] args)
        {
            res = GetResourceString(res);
            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string value = args[i] as string;
                    if (value != null && value.Length > 1024)
                    {
                        args[i] = value.Substring(0, 1024 - 3) + "...";
                    }
                }
                return string.Format(CultureInfo.CurrentCulture, res, args);
            }
            else
            {
                return res;
            }
        }
    }
}
