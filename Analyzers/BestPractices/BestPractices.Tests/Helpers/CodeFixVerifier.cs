using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.Data.SqlClient;

namespace BestPractices.Tests.Helpers
{
    public static class CodeFixVerifier<TAnalyzer, TCodeFix>
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.Diagnostic()"/>
        public static DiagnosticResult Diagnostic()
            => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic();

        /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.Diagnostic(string)"/>
        public static DiagnosticResult Diagnostic(string diagnosticId)
            => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(diagnosticId);

        /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.Diagnostic(DiagnosticDescriptor)"/>
        public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
            => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(descriptor);

        /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyAnalyzerAsync(string, DiagnosticResult[])"/>
        public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new Test<TAnalyzer, TCodeFix>
            {
                TestCode = source,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync(CancellationToken.None);
        }

        /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyCodeFixAsync(string, string)"/>
        public static async Task VerifyCodeFixAsync(string source, string fixedSource)
            => await VerifyCodeFixAsync(source, DiagnosticResult.EmptyDiagnosticResults, fixedSource);

        /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyCodeFixAsync(string, DiagnosticResult, string)"/>
        public static async Task VerifyCodeFixAsync(string source, DiagnosticResult expected, string? fixedSource)
            => await VerifyCodeFixAsync(source, [expected], fixedSource);

        /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyCodeFixAsync(string, DiagnosticResult[], string)"/>
        public static async Task VerifyCodeFixAsync(string source, DiagnosticResult[] expected, string? fixedSource)
        {
            var test =
                fixedSource is not null
                ? new Test<TAnalyzer, TCodeFix>
                {
                    TestState =
                    {
                        Sources = { source },
                        ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                        AdditionalReferences = { MetadataReference.CreateFromFile(typeof(SqlConnection).Assembly.Location) }
                    },
                    FixedState = { Sources = { fixedSource } }
                } 
                : new Test<TAnalyzer, TCodeFix>
                {
                    TestState =
                    {
                        Sources = { source },
                        ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                        AdditionalReferences = { MetadataReference.CreateFromFile(typeof(SqlConnection).Assembly.Location) }
                    },
                };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync(CancellationToken.None);
        }
    }
}
