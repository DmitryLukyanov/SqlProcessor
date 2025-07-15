using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SqlProcessor;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QueryReview
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(QueryReviewCodeFixProvider)), Shared]
    public class QueryReviewCodeFixProvider : CodeFixProvider
    {
        // TODO: remove hardcoded values
        private static readonly IEnumerable<(string TableName, IEnumerable<string> SupportedField)> __tablesInfo =
        [
            ("CustomersTable", ["Name", "LastName"])
        ];

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get 
            { 
                var fixableDiagnosticIds = ImmutableArray.Create(QueryReviewAnalyzer.DiagnosticId); 
                return fixableDiagnosticIds;
            }
        }

        public sealed override FixAllProvider GetFixAllProvider() =>
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.CodeFixSelectFields,
                    createChangedSolution: cancellationToken =>
                    {
                        return FixSelectQueryAsync(context, declaration, cancellationToken);
                    },
                    equivalenceKey: nameof(CodeFixResources.CodeFixSelectFields)),
                diagnostic);
        }

#pragma warning disable IDE0060 // Remove unused parameter
        private async Task<Solution> FixSelectQueryAsync(CodeFixContext context, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            var document = context.Document;

            // 1. Grab the root and the offending string‑literal node.
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            // Take the first (and only) diagnostic for this location
            var diagnostic = context.Diagnostics.Single();

            var literal = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) as LiteralExpressionSyntax;

            // Safety checks.
            if (literal is null || !literal.IsKind(SyntaxKind.StringLiteralExpression))
                return document.Project.Solution;

            // 2. Run the literal text through your fixer.
            var originalSql = literal.Token.ValueText; // unescaped text

            var generator = new TSqlBatchSelectParametersGenerator(originalSql, __tablesInfo);
            var fixedSql = generator
                .Render()
                .TrimEnd('\n').TrimEnd('\r').TrimEnd('\n').TrimEnd('\r').TrimEnd(';'); // TODO: make it oneliner in a better way

            // If nothing changed, bail out.
            if (originalSql == fixedSql)
                return document.Project.Solution;

            // 3. Create a new literal with the fixed SQL.
            var newLiteral = SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(fixedSql));

            // 4. Replace the node and return the updated solution.
            var newRoot = root.ReplaceNode(literal, newLiteral);
            var newDocument = document.WithSyntaxRoot(newRoot);

            return newDocument.Project.Solution;
        }
    }
}
