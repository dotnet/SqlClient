using System.Text.Json;
using System.Text.Json.Serialization;
namespace Microsoft.Data.SqlClient.UserAgent
{
    internal class UserAgentInfoDto
    {
        // Note: JSON key names are defined as constants to avoid reflection during serialization.
        // This allows us to calculate their UTF-8 encoded byte sizes efficiently without instantiating
        // the DTO or relying on JsonProperty attribute resolution at runtime. The small overhead of 
        // maintaining constants is justified by the performance and allocation savings.
        public const string DriverJsonKey = "driver";
        public const string VersionJsonKey = "version";
        public const string OsJsonKey = "os";
        public const string ArchJsonKey = "arch";
        public const string RuntimeJsonKey = "runtime";

        [JsonPropertyName(DriverJsonKey)]
        public string Driver { get; set; }

        [JsonPropertyName(VersionJsonKey)]
        public string Version { get; set; }

        [JsonPropertyName(OsJsonKey)]
        public OsInfo OS { get; set; }

        [JsonPropertyName(ArchJsonKey)]
        public string Arch { get; set; }

        [JsonPropertyName(RuntimeJsonKey)]
        public string Runtime { get; set; }

        public class OsInfo
        {
            public const string TypeJsonKey = "type";
            public const string DetailsJsonKey = "details";

            [JsonPropertyName(TypeJsonKey)]
            public string Type { get; set; }

            [JsonPropertyName(DetailsJsonKey)]
            public string Details { get; set; }
        }
    }
}
