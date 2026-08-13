namespace Microsoft.Data.SqlClient.Samples.AzureSqlConnector
{
    /// <summary>
    /// Shared SQL text used by both <see cref="MainForm"/> (UI-thread variant) and
    /// <see cref="MainFormWorker"/> (worker-thread variant). Keeping the literal in one place
    /// avoids drift when one variant gains a new column.
    /// </summary>
    internal static class IdentityQuery
    {
        public const string CommandText =
            "SELECT " +
            "    SUSER_SNAME()        AS LoggedInUser, " +
            "    ORIGINAL_LOGIN()     AS OriginalLogin, " +
            "    USER_NAME()          AS DatabaseUser, " +
            "    SUSER_ID()           AS LoginSid, " +
            "    DB_NAME()            AS DatabaseName, " +
            "    @@SERVERNAME         AS ServerName, " +
            "    HOST_NAME()          AS ClientHost, " +
            "    APP_NAME()           AS AppName, " +
            "    SESSION_USER         AS SessionUser, " +
            "    CURRENT_USER         AS CurrentUser, " +
            "    @@SPID               AS SessionId, " +
            "    @@VERSION            AS ServerVersion;";
    }
}
