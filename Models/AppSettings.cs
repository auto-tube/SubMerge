using System.Text.Json.Serialization; // Required for JsonPropertyName

namespace AutoTubeWpf.Models
{
    /// <summary>
    /// Represents the application settings that are persisted to a configuration file.
    /// </summary>
    public class AppSettings
    {
        // Use JsonPropertyName to match the keys in the existing Python config file if desired,
        // otherwise, C# naming conventions are fine for a new config file.
        // Let's match the Python keys for potential compatibility during transition.

        [JsonPropertyName("google_api_key")]
        public string? GoogleApiKey { get; set; }

        [JsonPropertyName("aws_access_key_id")]
        public string? AwsAccessKeyId { get; set; }

        [JsonPropertyName("aws_secret_access_key")]
        public string? AwsSecretAccessKey { get; set; }

        [JsonPropertyName("aws_region_name")]
        public string? AwsRegionName { get; set; } = "us-east-1"; // Default from Python code

        [JsonPropertyName("default_output_path")]
        public string? DefaultOutputPath { get; set; }

        [JsonPropertyName("organize_output")]
        public bool OrganizeOutput { get; set; } = true; // Default from Python code

        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "dark"; // Default from Python code

        // Add any other settings that need persistence here
    }
}