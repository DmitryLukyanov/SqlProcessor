using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

namespace BestPractices
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IDisposableAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TRSP02";

        private static readonly LocalizableString Title = "IDisposable object not disposed";
        private static readonly LocalizableString MessageFormat =
            "IDisposable object '{0}' is not disposed or not disposed reliably (e.g., via using or finally)";
        private static readonly LocalizableString Description =
            "Detects IDisposable objects that are not wrapped in using or disposed reliably in a finally block.";
        private const string Category = "Reliability";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterOperationBlockStartAction(OnOperationBlockStart);
        }

        private static void OnOperationBlockStart(OperationBlockStartAnalysisContext context)
        {
            var disposableVariables = new ConcurrentBag<ILocalSymbol>();
            var disposeCalls = new ConcurrentDictionary<ILocalSymbol, bool>(SymbolEqualityComparer.Default);
            var usingDeclarations = new ConcurrentDictionary<ILocalSymbol, bool>(SymbolEqualityComparer.Default);

            context.RegisterOperationAction(c => AnalyzeVariableDeclaration(c, disposableVariables), OperationKind.VariableDeclarator);
            context.RegisterOperationAction(c => AnalyzeDisposeCall(c, disposeCalls), OperationKind.Invocation);
            context.RegisterOperationAction(c => AnalyzeUsingStatement(c, usingDeclarations), OperationKind.Using, OperationKind.UsingDeclaration);

            context.RegisterOperationBlockEndAction(c => AnalyzeDisposal(c, disposableVariables, disposeCalls, usingDeclarations));
        }

        private static void AnalyzeVariableDeclaration(OperationAnalysisContext context, ConcurrentBag<ILocalSymbol> variablesBag)
        {
            if (context.Operation is not IVariableDeclaratorOperation variableDecl)
            {
                return;
            }

            var initializer = variableDecl.GetVariableInitializer()?.Value;
            if (initializer?.Type == null || !IsDisposable(initializer.Type))
            {
                return;
            }

            variablesBag.Add(variableDecl.Symbol);

            static bool IsDisposable(ITypeSymbol typeSymbol) => typeSymbol
                .AllInterfaces
                .Any(i => i.SpecialType == SpecialType.System_IDisposable);
        }

        private static void AnalyzeDisposeCall(OperationAnalysisContext context, ConcurrentDictionary<ILocalSymbol, bool> map)
        {
            if (context.Operation is not IInvocationOperation invocation || 
                !IsDisposeName(invocation.TargetMethod.Name) ||
                invocation.Arguments.Length != 0)
            {
                return;
            }

            if (invocation.Instance is ILocalReferenceOperation localRef)
            { 
                map[localRef.Local] = true;
            }
        }

        private static void AnalyzeUsingStatement(OperationAnalysisContext context, ConcurrentDictionary<ILocalSymbol, bool> map)
        {
            switch (context.Operation)
            {
                case IUsingOperation usingOperation:
                    if (usingOperation.Resources is IVariableDeclarationGroupOperation group)
                    {
                        foreach (var decl in group.Declarations)
                        {
                            foreach (var variable in decl.Declarators)
                            {
                                map[variable.Symbol] = true;
                            }
                        }
                    }
                    break;

                case IUsingDeclarationOperation usingDeclaration:
                    foreach (var decl in usingDeclaration.DeclarationGroup.Declarations)
                    {
                        foreach (var variable in decl.Declarators)
                        {
                            map[variable.Symbol] = true;
                        }
                    }
                    break;
            }
        }

        private static void AnalyzeDisposal(
            OperationBlockAnalysisContext context,
            ConcurrentBag<ILocalSymbol> variables,
            ConcurrentDictionary<ILocalSymbol, bool> disposeCalls,
            ConcurrentDictionary<ILocalSymbol, bool> usingDeclarations)
        {
            // 1. Start with any block supplied by the analysis context
            var operation = context.OperationBlocks.SingleOrDefault();
            if (operation is null)
            {
                return;
            }

            // 2. Climb up until we reach the node whose Parent is null
            while (operation.Parent is not null)
            {
                operation = operation.Parent;
            }

            // 3. Build the CFG from that root
            ControlFlowGraph controlFlowGraph = operation switch
            {
                IMethodBodyOperation mbo => ControlFlowGraph.Create(mbo),
                IBlockOperation bo => ControlFlowGraph.Create(bo),
                _ => null
            };
            if (controlFlowGraph is null)
            {
                return;
            }

            // 4. Diagnostics as before …
            foreach (var variable in variables)
            {
                if (
                    // consider this as temporary unsafe
                    // disposeCalls.ContainsKey(variable) ||
                    usingDeclarations.ContainsKey(variable) ||
                    IsDisposedInFinally(controlFlowGraph, variable))
                {
                    continue;
                }

                var declSyntax = variable.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                if (declSyntax != null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            Rule, 
                            declSyntax.GetLocation(), 
                            variable.Name));
                }
            }
        }

        private static bool IsDisposedInFinally(ControlFlowGraph cfg, ILocalSymbol variable)
        {
            foreach (var block in cfg.Blocks)
            {
                if (block.EnclosingRegion.Kind != ControlFlowRegionKind.Finally)
                {
                    continue;
                }

                foreach (var operation in block.Operations)
                {
                    if (operation is IInvocationOperation invocation &&
                        IsDisposeName(invocation.TargetMethod.Name) &&
                        invocation.Instance is ILocalReferenceOperation localRef &&
                        SymbolEqualityComparer.Default.Equals(localRef.Local, variable))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        private static bool IsDisposeName(string methodName) => methodName == nameof(IDisposable.Dispose);
    }
}
