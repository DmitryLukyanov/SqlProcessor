using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using UnitTestsGenerator.AI.Agent.Agents;
using UnitTestsGenerator.AI.Agent.Extensions;
using UnitTestsGenerator.AI.Agent.Filters;
using UnitTestsGenerator.AI.Agent.Tools;
using UnitTestsGenerator.AI.Agent.Utils;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0130 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal class Program
{
    private static string __methodDefinition = $@"public sealed class WebCallService
    {{
        public bool Call3rdParty(string endpoint)
        {{
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(endpoint);
            try
            {{
                _ = httpClient.Send(new HttpRequestMessage());
                return true;
            }}
            catch (Exception ex)
            {{
                return false;
            }}
        }}

        public async Task<bool> Call3rdPartyAsync(string endpoint)
        {{
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(endpoint);
            try
            {{
                _ = await httpClient.SendAsync(new HttpRequestMessage());
                return true;
            }}
            catch (Exception ex)
            {{
                return false;
            }}
        }}

        public decimal GetCommitment(decimal input)
        {{
            var taxes = GetTaxes(input, ""us"");
            return input - taxes;
        }}

        public decimal GetTaxes(decimal input, string county)
        {{
            if (county == ""us"")
            {{
                return (decimal)(input * (decimal)0.4);
            }}
            return input * (decimal) 0.2;
        }}
    }}";

    private static async Task Main(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new InvalidOperationException("OPENAI_API_KEY must be configured");

        const string DeveloperAgent = "Developer";
        const string TesterAgent = "Tester";

        var serviceCollection = new ServiceCollection();
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOpenAIChatCompletion("gpt-4o-mini", apiKey);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        serviceCollection.AddLogging(builder => builder.AddConsole());
        serviceCollection.AddSingleton<AgentsFlowsFactory>();
        serviceCollection.AddSingleton<KernelPromptTemplateFactory>();
        serviceCollection.AddSingleton<KernelArgumentsPoolConfiguration>(
            new KernelArgumentsPoolConfiguration(new Dictionary<string, string>()
            // TODO: investigate the below
            /*{
                { DeveloperAgent, "lastTesterReview" }, // initialize empty value
                { TesterAgent, "lastTesterReview" }
            }*/));
        kernelBuilder.Services.AddSingleton<IFunctionInvocationFilter, KernelFunctionInvokationFilter>();
        kernelBuilder.Services.AddSingleton<IAutoFunctionInvocationFilter, AutoFunctionInvocationFilter>();
        kernelBuilder.Services.AddSingleton<IPromptRenderFilter, PromptRenderFilter>();

        kernelBuilder.Services.AddRangeSingleton(serviceCollection);
        serviceCollection.AddSingleton<Kernel>((sp) => kernelBuilder.Build());
        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(serviceCollection);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var aiFlowFactory = provider.GetRequiredService<AgentsFlowsFactory>();

        // creating agents
        var testerAgent = await aiFlowFactory.CreateAgent(
            template: $$$"""
                    You're tester who can review and build xunit unit tests.
                    Use available tool for this purpose.

                    If after review, the created tests are approved, then tests should be saved into the provided tests repository
                    otherwise, if the review has been failed, the output must be error details.
                    """,
            agentName: TesterAgent,
            toolsConfigurator: (kernel, sp) =>
            {
                kernel.Plugins.AddFromType<TesterTools>(pluginName: TesterTools.ReviewCreatedUnitTestsPluginName, serviceProvider: sp)
                ;
            });

        var developerAgent = await aiFlowFactory.CreateAgent(
            template: $$$"""
            You're a skilled c# developer who can create unit tests with xunit framework for the provided as input classes or methods.

            Create a unit test for the following content: {{{__methodDefinition}}}.

            Use the following rules in testing:
            - Always follow Arrange, Act and Assert (AAA) Pattern
            - Use the following test name templates. For positive scenarious: "Given-Wen-Then" technic to name unit tests.
            - If a method has input arguments with basic c# type like C# enum or int, decimal, double, float, string, bool, short,
            create a single C# test method and configure test cases via InlineData attribute. 
            As InlineData arguments, use edge cases values. Foe example, if there is a method "public int CalculateTax(int gross, string note)",
            the InlineData attributes for a test method should be: InlineData(0, "") InlineData(10, null) InlineData(100, "Some normal note") InlineData(-10, " ") 
            - If method has only an System.Net.HttpClient call inside with only preparing request data and parsing response, 
            CREATE ONLY AN EMPTY TEST with a comment: "Autogenerating tests for HttpClient tests are not supported at this moment."
            - If method has only an ADO.NET or EntityFramework server calls inside with only preparing request data and parsing response, 
            CREATE ONLY AN EMPTY TEST with a comment: "Autogenerating tests for SQL queries tests are not supported at this moment.
            - Before EACH TEST add a comment with justification why you created it in this way
            
            After completing the unit test(s), as the final action, always call the `{{{DeveloperTools.LogDevelopmentPluginName}}}` plugin to log the creation process and the resulting tests, ensuring the log contains:
            1. originalCode: The original method code
            2. output: The full output from assistant
            3. unitTest:All listing of the created unit tests in C#. Ensure that the code is complited
            4. reasoning: Your reasoning
            """, 
            agentName: DeveloperAgent, 
            toolsConfigurator: (kernel, sp) =>
            {
                kernel.Plugins.AddFromType<DeveloperTools>(pluginName: DeveloperTools.LogDevelopmentPluginName, serviceProvider: sp);
            });

        // creating chat
        var chat = aiFlowFactory.CreateAgentGroupChat(
            agents: [developerAgent, testerAgent],
            terminationFunction: AgentGroupChat.CreatePromptFunctionForStrategy(
                $$$"""
        Determine if the testing created unit test has been successfull.
        If so, respond with a single word: {{{AgentsFlowsFactory.TerminationToken}}}
        Ensure that last step is always testing step.

        History:
        {{$history}}
        """,
                safeParameterNames: AgentsFlowsFactory.HistoryVariable));

        // agents communication
        await foreach (ChatMessageContent response in chat.InvokeAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Console.Write(response.Content);
        }
    }
}
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0130 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.