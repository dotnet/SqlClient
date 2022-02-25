namespace Microsoft.Data.Sql
{
    /// <summary>
    /// const values for SqlDataSourceEnumerator
    /// </summary>
    internal class SqlDataSourceEnumeratorUtil
    {
        internal const string ServerName = "ServerName";
        internal const string InstanceName = "InstanceName";
        internal const string IsClustered = "IsClustered";
        internal const string Version = "Version";
        internal static readonly string s_version = "Version:";
        internal static readonly string s_cluster = "Clustered:";
        internal static readonly int s_clusterLength = s_cluster.Length;
        internal static readonly int s_versionLength = s_version.Length;
    }
}
