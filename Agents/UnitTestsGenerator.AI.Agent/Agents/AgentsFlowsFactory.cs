using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using UnitTestsGenerator.AI.Agent.Utils;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0130 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
namespace UnitTestsGenerator.AI.Agent.Agents
{
    public sealed class AgentsFlowsFactory(
        Kernel kernel, 
        KernelPromptTemplateFactory kernelPromptTemplateFactory, 
        ILoggerFactory loggerFactory,
        ILogger<AgentsFlowsFactory> logger,
        IServiceProvider serviceProvider,
        KernelArgumentsPoolConfiguration kernelArgumentsPoolConfiguration)
    {
        public const string HistoryVariable = "history";
        public const string TerminationToken = "true";

        private readonly Kernel _kernel = kernel;
        private readonly ILogger<AgentsFlowsFactory> _logger = logger;
        private readonly ILoggerFactory _loggerFactory = loggerFactory;
        private readonly KernelPromptTemplateFactory _kernelPromptTemplateFactory = kernelPromptTemplateFactory;
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly KernelArgumentsPoolConfiguration _kernelArgumentsPoolConfiguration = kernelArgumentsPoolConfiguration;

        public async Task<ChatCompletionAgent> CreateAgent(
            string agentName,
            string template,
            Action<Kernel, IServiceProvider>? toolsConfigurator = null,
            params KeyValuePair<string, string>[] templateArguments)
        {
            var kernel = _kernel.Clone();

            toolsConfigurator?.Invoke(kernel, _serviceProvider);

            var kernelArguments = _kernelArgumentsPoolConfiguration.Pool.GetOrAdd(
                key: agentName,
                value: new KernelArguments(
                    new PromptExecutionSettings()
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                            options: new FunctionChoiceBehaviorOptions()
                            {
                                // serialize as JsonElement with type
                                RetainArgumentTypes = true,
                            })
                    }));

            foreach (var argument in templateArguments)
            {
                kernelArguments.Add(argument.Key, argument.Value);
            }
            // TODO: investigate the below
            //if (_kernelArgumentsPoolConfiguration.Arguments.TryGetValue(agentName, out var trackedField))
            //{
            //    kernelArguments.Add(trackedField, string.Empty);
            //}

            var templateValue = _kernelPromptTemplateFactory.Create(new PromptTemplateConfig(template));

            var renderedTemplate = await templateValue.RenderAsync(kernel, kernelArguments);
            _logger.LogDebug($"Rendered instruction: {renderedTemplate}");

            return new()
            {
                Name = agentName,
                Template = templateValue,
                Kernel = kernel,
                Arguments = kernelArguments,
                LoggerFactory = _loggerFactory,
                UseImmutableKernel = true
            };
        }

        public SelectionStrategy CreateSelectorStrategy(
            ChatCompletionAgent initialAgent) => 
            new SequentialSelectionStrategy()
            {
                InitialAgent = initialAgent
            };

        public KernelFunctionTerminationStrategy CreateTerminationStrategy(
            ChatCompletionAgent[] agentsForApprove,
            KernelFunction terminationFunction,
            string historyVariable,
            string terminationToken) => 
            new(terminationFunction, _kernel)
            {
                Agents = agentsForApprove,
                ResultParser = (result) =>
                {
                    return result.GetValue<string>()?.Equals(terminationToken, StringComparison.OrdinalIgnoreCase) ?? false;
                },
                HistoryVariableName = historyVariable,
                HistoryReducer = new ChatHistoryTruncationReducer(1),
                MaximumIterations = 10,
            };

        public AgentGroupChat CreateAgentGroupChat(
            ChatCompletionAgent[] agents,
            KernelFunction terminationFunction)
        {
            ArgumentOutOfRangeException.ThrowIfZero(agents.Length);

            var selectionStrategy = CreateSelectorStrategy(initialAgent: agents.First());
            var terminationStrategy = CreateTerminationStrategy(agentsForApprove: [agents.Last()], terminationFunction, HistoryVariable, TerminationToken);

            var agentGroupChatSettings = new AgentGroupChatSettings
            {
                SelectionStrategy = selectionStrategy,
                TerminationStrategy = terminationStrategy
            };
            return new AgentGroupChat(agents)
            {
                ExecutionSettings = agentGroupChatSettings,
                LoggerFactory = _loggerFactory
            };
        }
    }
}
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0130 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.