// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.Data
{
    internal partial class StringsHelper : Strings
    {
        private static StringsHelper s_loader = null;
        private readonly ResourceManager _resources;

        internal StringsHelper()
        {
            _resources = new ResourceManager("Microsoft.Data.SqlClient.Resources.Strings", GetType().Assembly);
        }

        private static StringsHelper GetLoader()
        {
            if (s_loader is null)
            {
                StringsHelper sr = new();
                Interlocked.CompareExchange(ref s_loader, sr, null);
            }
            return s_loader;
        }

        public static string GetResourceString(string res)
        {
            StringsHelper sys = GetLoader();
            if (sys is null)
                return null;

            // If "res" is a resource id, temp will not be null, "res" will contain the retrieved resource string.
            // If "res" is not a resource id, temp will be null.
            string temp = sys._resources.GetString(res, StringsHelper.Culture);
            return temp ?? res;
        }

        public static string GetString(string res, params object[] args)
        {
            res = GetResourceString(res);
            if (args?.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string value = args[i] as string;
                    if (value?.Length > 1024)
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

#if NET
        // This method is used to decide if we need to append the exception message parameters to the message when calling Strings.Format. 
        // by default it returns false.
        // Native code generators can replace the value this returns based on user input at the time of native code generation.
        // Marked as NoInlining because if this is used in an AoT compiled app that is not compiled into a single file, the user
        // could compile each module with a different setting for this. We want to make sure there's a consistent behavior
        // that doesn't depend on which native module this method got inlined into.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool UsingResourceKeys()
        {
            return false;
        }

        public static string Format(string resourceFormat, params object[] args)
        {
            if (args is not null)
            {
                if (UsingResourceKeys())
                {
                    return resourceFormat + string.Join(", ", args);
                }

                return string.Format(resourceFormat, args);
            }

            return resourceFormat;
        }

        public static string Format(string resourceFormat, object p1)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1);
            }

            return string.Format(resourceFormat, p1);
        }

        public static string Format(string resourceFormat, object p1, object p2)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1, p2);
            }

            return string.Format(resourceFormat, p1, p2);
        }

        public static string Format(string resourceFormat, object p1, object p2, object p3)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1, p2, p3);
            }

            return string.Format(resourceFormat, p1, p2, p3);
        }
#endif
    }
}
