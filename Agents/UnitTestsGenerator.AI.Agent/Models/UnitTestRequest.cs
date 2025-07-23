using System.Text.Json.Serialization;

namespace UnitTestsGenerator.AI.Agent.Models
{
    public record UnitTestRequest
    {
        [JsonPropertyName("classCode")]
        public string ClassCode { get; init; } = string.Empty;
    }
}
