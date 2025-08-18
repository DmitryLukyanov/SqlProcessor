using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestsGenerator.CodeRefactoring
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(UnitTestsGeneratorCodeRefactoringProvider)), Shared]
    public class UnitTestsGeneratorCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            // TODO: Replace the following code with your own analysis, generating a CodeAction for each refactoring to offer

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Find the node at the selection.
            var node = root.FindNode(context.Span);

            switch (node)
            {
                case ClassDeclarationSyntax classDeclarationSyntax: 
                    {
                        var action = CodeAction.Create($"Create unit tests for '{classDeclarationSyntax?.Identifier.ValueText}'", c => SendRequestToUnitTestsAgentAsync(context.Document, classDeclarationSyntax, c));
                        context.RegisterRefactoring(action);
                    }
                    break;
            }
        }

        private async Task<Solution> SendRequestToUnitTestsAgentAsync(Document document, ClassDeclarationSyntax methodDeclarationSyntax, CancellationToken cancellationToken)
        {
            return document?.Project?.Solution;
        }
    }
}
