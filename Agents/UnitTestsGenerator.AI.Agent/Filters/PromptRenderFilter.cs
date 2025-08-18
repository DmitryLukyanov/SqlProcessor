using Microsoft.SemanticKernel;

namespace UnitTestsGenerator.AI.Agent.Filters
{
    /*
    https://learn.microsoft.com/en-us/semantic-kernel/concepts/enterprise-readiness/filters?utm_source=chatgpt.com&pivots=programming-language-csharp
    Prompt Render Filter - this filter is triggered before the prompt rendering operation, enabling:
    - Viewing and modifying the prompt that will be sent to the AI (e.g., for RAG or PII redaction)
    - Preventing prompt submission to the AI by overriding the function result (e.g., for Semantic Caching)
     */
    public sealed class PromptRenderFilter : IPromptRenderFilter
    {
        public async Task OnPromptRenderAsync(PromptRenderContext context, Func<PromptRenderContext, Task> next)
        {
            Console.WriteLine("==============================Prompt rendering==============================");
            Console.WriteLine($"\n\nFunction name: {context.Function.Name}. Plugin name: {context.Function.PluginName}.");
            await next(context);
            Console.WriteLine($"\nPrompt: {context.RenderedPrompt}");
            Console.WriteLine($"Function result: {context.Result}\n\n");
            Console.WriteLine("==============================Prompt rendered===============================");
        }
    }
}
