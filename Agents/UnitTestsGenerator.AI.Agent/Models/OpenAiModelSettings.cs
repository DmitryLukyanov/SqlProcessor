namespace UnitTestsGenerator.AI.Agent.OpenAI.Models
{
    public class OpenAiModelSettings
    {
        public const string ConfigurationKey = "OpenAi";

        public required string ModelName { get; init; }
        public required string ApiKey { get; init; }
    }
}
