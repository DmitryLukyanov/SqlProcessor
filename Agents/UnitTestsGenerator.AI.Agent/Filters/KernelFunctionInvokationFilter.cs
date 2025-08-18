using Microsoft.SemanticKernel;

namespace UnitTestsGenerator.AI.Agent.Filters
{
    /*
    https://learn.microsoft.com/en-us/semantic-kernel/concepts/enterprise-readiness/filters?utm_source=chatgpt.com&pivots=programming-language-csharp
 
    Function Invocation Filter - this filter is executed each time a KernelFunction is invoked. It allows:

    - Access to information about the function being executed and its arguments
    - Handling of exceptions during function execution
    - Overriding of the function result, either before (for instance for caching scenario's) or after execution (for instance for responsible AI scenarios)
    - Retrying of the function in case of failure (e.g., switching to an alternative AI model)
    */
    public sealed class KernelFunctionInvokationFilter : IFunctionInvocationFilter
    {
        public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            Console.WriteLine("***************************************FunctionInvokation is started*****************************************");
            Console.WriteLine($"FunctionName:{context.Function.Name}");
            await next(context);
            Console.WriteLine("***************************************FunctionInvokation is complited***************************************");
        }
    }
}
