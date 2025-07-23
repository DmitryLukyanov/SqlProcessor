using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace UnitTestsGenerator.AI.Agent.Tools
{
    public sealed class DeveloperTools
    {
        public const string LogDevelopmentPluginName = nameof(LogDevelopment);

        [KernelFunction(LogDevelopmentPluginName)]
        [Description(@"Log all the work that was done in development including generated unit tests")]
        public void LogDevelopment(
            [Description("The original method code")] string originalCode,
            [Description("The main assistant output")] string output, 
            [Description("Full listing of the created unit tests in C# code block. Ensure that the code is complited")] string unitTest,
            [Description("Reasoning")] string reasoning/*,
            [Description("Verbatim tester review including tool output")] string testerReview*/)
        { 
            // follow up changes after development agent
        }
    }
}
