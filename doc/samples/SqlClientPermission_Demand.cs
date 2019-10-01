using System;
using System.Data;
// <Snippet1>
using Microsoft.Data.SqlClient;
using System.Security.Permissions;

class Program
{
    static void Main()
    {
    }

    // Code requires directives to
    // System.Security.Permissions and
    // Microsoft.Data.SqlClient

    private bool CanRequestNotifications()
    {
        SqlClientPermission permission =
            new SqlClientPermission(
            PermissionState.Unrestricted);
        try
        {
            permission.Demand();
            return true;
        }
        catch (System.Exception)
        {
            return false;
        }
    }
}
// </Snippet1>
