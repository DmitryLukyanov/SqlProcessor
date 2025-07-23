using Microsoft.SemanticKernel;
using System.Collections.Concurrent;

namespace UnitTestsGenerator.AI.Agent.Utils
{
    public sealed class KernelArgumentsPoolConfiguration(
        Dictionary<string, string> agentToFieldMapping)
    {
        public Dictionary<string, string> Arguments = agentToFieldMapping;
        public ConcurrentDictionary<string, KernelArguments> Pool = new(StringComparer.OrdinalIgnoreCase);

        public void UpdateAgentConfigurationIfPossible(string agentName, string newValue)
        {
            if (Arguments.TryGetValue(agentName, out var field))
            {
                if (Pool.TryGetValue(agentName, out var kernelArgument))
                {
                    kernelArgument[field] = newValue;
                }
            }
        }
    }
}
