using Microsoft.SemanticKernel;
using UnitTestsGenerator.AI.Agent.Agents;
using UnitTestsGenerator.AI.Agent.Tools;
using UnitTestsGenerator.AI.Agent.Utils;

namespace UnitTestsGenerator.AI.Agent.Filters
{
    /*
     https://learn.microsoft.com/en-us/semantic-kernel/concepts/enterprise-readiness/filters?utm_source=chatgpt.com&pivots=programming-language-csharp

    Auto Function Invocation Filter - similar to the function invocation filter, 
    this filter operates within the scope of automatic function calling, providing additional context, 
    including chat history, a list of all functions to be executed, and iteration counters. 
    It also allows termination of the auto function calling process (e.g., if a desired result is obtained from the second of three planned functions).
     */
    public sealed class AutoFunctionInvocationFilter(KernelArgumentsPoolConfiguration kernelArgumentsPoolConfiguration) : IAutoFunctionInvocationFilter
    {
        public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
        {
            Console.WriteLine("---------------------------------------AutoFunctionInvokation is started-----------------------------------------");
            Console.WriteLine($"AutoFunctionName:{context.Function.Name}");
            await next(context);
            Console.WriteLine("---------------------------------------AutoFunctionInvokation is complited---------------------------------------");

            // TODO: investigate the below
            //// update history
            //var agentName = context.Function.Name switch
            //{
            //    TesterTools.ReviewCreatedUnitTestsPluginName => "Tester",
            //    DeveloperTools.LogDevelopmentPluginName => "Developer",
            //    _ => throw new NotSupportedException("TODO: find a better approach")
            //};
            //kernelArgumentsPoolConfiguration.UpdateAgentConfigurationIfPossible(agentName, context.Result!.GetValue<string>()!);

            // Stop any further auto tool calls in THIS turn
            context.Terminate = true;
        }
    }
}
