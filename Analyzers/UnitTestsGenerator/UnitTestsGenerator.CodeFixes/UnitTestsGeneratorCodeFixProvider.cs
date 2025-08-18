using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/*
 NOT USED => see refactoring modele
 */
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnitTestsGeneratorCodeFixProvider)), Shared]
public class UnitTestsGeneratorCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(UnitTestsGenerator.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        string title = "Generate unit tests";

        if (node is MethodDeclarationSyntax methodDecl)
        {
            title += $" for method '{methodDecl.Identifier.Text}'";
        }
        else if (node is ClassDeclarationSyntax classDecl)
        {
            title += $" for class '{classDecl.Identifier.Text}'";
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedSolution: cancellationToken => FixSelectQueryAsync(context, cancellationToken),
                equivalenceKey: "GenerateUnitTestsKey"),
            diagnostic);
    }

    private Task<Solution> FixSelectQueryAsync(CodeFixContext context, CancellationToken cancellationToken)
    {
        // Placeholder: In future, you can add logic to generate test files or update prompts
        return Task.FromResult(context.Document.Project.Solution);
    }
}
