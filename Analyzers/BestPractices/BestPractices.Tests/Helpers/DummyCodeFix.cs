using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Composition;

namespace BestPractices.Tests.Helpers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IDisposableAnalyzer)), Shared]
    public class DummyCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                var fixableDiagnosticIds = ImmutableArray.Create(IDisposableAnalyzer.DiagnosticId);
                return fixableDiagnosticIds;
            }
        }

        public sealed override FixAllProvider GetFixAllProvider() =>
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            WellKnownFixAllProviders.BatchFixer;

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            // do nothing for dummy codefix
            return Task.CompletedTask;
        }
    }
}
