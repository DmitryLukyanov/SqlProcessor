using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

/*
 NOT USED => see refactoring modele
 */

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UnitTestsGenerator : DiagnosticAnalyzer
{
    public const string DiagnosticId = "TRSP03";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Generate unit tests",
        messageFormat: "Generate unit tests",
        category: "UnitTests",
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.Unnecessary]); // Ensures it doesn’t show up

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration, SyntaxKind.ClassDeclaration);
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;

        // Only show if cursor is on this node (optional filtering)
        var diagnostic = Diagnostic.Create(Rule, node.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }
}