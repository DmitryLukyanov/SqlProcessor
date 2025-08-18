using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit.Sdk;
using Document = Microsoft.CodeAnalysis.Document;

namespace UnitTestsGenerator.Tests
{
    public class SmokeTests
    {
        // https://www.youtube.com/watch?v=-oxN-hNjD0Y
        [Fact]
        public async Task Smoke_test()
        {
            var originalCode = @"
public static class Class1
{
    public static void Method1(Action<string /*firstName*/> write)
    {
        write(""Adam"");
    }
}
";
            // no changes
            var expectedCode = @"
public static class Class1
{
    public static void Method1(Action<string /*firstName*/> write)
    {
        write(""Adam"");
    }
}
";
            var (workspace, project, document, documentId) = PrepareSolution(originalCode, debugName: nameof(Smoke_test));
            var root = (await document.GetSyntaxRootAsync())!; // look at original code
            var methodDeclarationSyntax = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
            var updatedDocument = await CreateSubjectAndReturnUpdatedDocument(workspace, document, documentId, span: methodDeclarationSyntax.Identifier.Span);

            var actualUpdatedText = (await updatedDocument.GetTextAsync()).ToString();

            Assert.Equal(expectedCode, actualUpdatedText);
        }

        private (AdhocWorkspace, Project, Document, DocumentId) PrepareSolution(string originalCode, string debugName)
        {
            var workspace = new AdhocWorkspace();
            var solution = workspace.AddSolution(
                SolutionInfo.Create(
                    id: SolutionId.CreateNewId(debugName: debugName),
                    version: VersionStamp.Default,
                    filePath: ""));
            var projectId = ProjectId.CreateNewId();
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddProject(projectId, "Project1", "Project1", LanguageNames.CSharp);
            solution = solution.AddDocument(documentId, "Document.cs", originalCode);
            var project = solution!
                .GetProject(projectId)!
                .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

            if (!workspace.TryApplyChanges(project.Solution))
            {
                throw new XunitException("The mock solution configuration has been failed"); // somehow this timing is done before applying code refactoring
            }

            var document = project.GetDocument(documentId)!;

            return (workspace, project, document, documentId);
        }

        private async Task<Document> CreateSubjectAndReturnUpdatedDocument(
            AdhocWorkspace workspace, 
            Document document, 
            DocumentId documentId, 
            TextSpan span,
            CancellationToken cancellationToken = default)
        {
            var subject = new UnitTestsGenerator.CodeRefactoring.UnitTestsGeneratorCodeRefactoringProvider();

            //var root = (await document.GetSyntaxRootAsync())!; // look at original code
            //var parameter = root.DescendantNodes().OfType<ParameterSyntax>().Single();
            var registeredCodeActions = new List<CodeAction>();

            await subject.ComputeRefactoringsAsync(
                new CodeRefactoringContext(
                    document,
                    span: span,
                    registerRefactoring: (codeAction) =>
                    {
                        registeredCodeActions.Add(codeAction);
                    },
                    cancellationToken));

            var codeAction = registeredCodeActions.Single(); // consider only single action for now
            var operations = await codeAction.GetOperationsAsync(cancellationToken);

            foreach (var operation in operations)
            {
                operation.Apply(workspace, cancellationToken);
            }

            return workspace.CurrentSolution.GetDocument(documentId)!;
        }
    }
}
