using System;

namespace Microsoft.Data.SqlClient
{
    public sealed partial class ActiveDirectoryAuthenticationProvider : SqlAuthenticationProvider
    {
        private Func<object> _parentActivityOrWindowFunc = null;

        private Func<object> ParentActivityOrWindow
        {
            get
            {
                return _parentActivityOrWindowFunc != null ? _parentActivityOrWindowFunc : GetConsoleOrTerminalWindow;
            }

            set 
            { 
                _parentActivityOrWindowFunc = value;
            }
        }

#if NETFRAMEWORK
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/SetIWin32WindowFunc/*'/>
        public void SetIWin32WindowFunc(Func<System.Windows.Forms.IWin32Window> iWin32WindowFunc) => SetParentActivityOrWindow(iWin32WindowFunc);
#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parentActivityOrWindowFunc"></param>
        public void SetParentActivityOrWindow(Func<object> parentActivityOrWindowFunc) => this.ParentActivityOrWindow = parentActivityOrWindowFunc;

        private object GetConsoleOrTerminalWindow()
        {
            IntPtr consoleHandle = Interop.Kernel32.GetConsoleWindow();
            IntPtr handle = Interop.User32.GetAncestor(consoleHandle, Interop.User32.GetAncestorFlags.GetRootOwner);

            return handle;
        }
    }
}
