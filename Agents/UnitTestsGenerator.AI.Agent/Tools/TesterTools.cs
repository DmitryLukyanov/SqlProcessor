using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;
using UnitTestsGenerator.AI.Agent.Agents;

namespace UnitTestsGenerator.AI.Agent.Tools
{
    public class TesterTools
    {
        public const string ReviewCreatedUnitTestsPluginName = nameof(ReviewCreatedUnitTests);

        [KernelFunction(ReviewCreatedUnitTestsPluginName)]
        [Description(@"Test a previouly created unit test")]
        [return: Description($$$"""
            Determine if the testing has been successful. If so, the response will be just: {{{AgentsFlowsFactory.TerminationToken}}}. 
            Any other text will contain an error details.
            After calling the tool, include the tool’s full return value in your message without paraphrasing.
            """)]
        public string ReviewCreatedUnitTests(string unitTestContent)
        {
            try
            {
                if (IsSyntaxValid(unitTestContent, out var diagnostics))
                {
                    return true.ToString();
                }

                return BuildDiagnosticsReport(diagnostics);
            }
            catch (Exception ex)
            {
                return $"TOOL-ERROR: {ex.GetType().Name}: {ex.Message}";
            }
        }

        private static string BuildDiagnosticsReport(Diagnostic[] diagnostics)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Unit test validation failed. Diagnostics:");
            foreach (var d in diagnostics.OrderBy(d => d.Location.SourceSpan.Start))
            {
                var span = d.Location.GetLineSpan();
                var line = span.StartLinePosition.Line + 1;
                var col = span.StartLinePosition.Character + 1;
                sb.AppendLine($"- {d.Id} {d.Severity} at {line}:{col} — {d.GetMessage()}");
            }
            return sb.ToString();
        }

        private static bool IsSyntaxValid(
            string code,
            out Diagnostic[] syntaxErrors,
            LanguageVersion languageVersion = LanguageVersion.Latest, // or LatestMajor/Default
            bool treatAsScript = false)
        {
            var parseOptions = new CSharpParseOptions(
                languageVersion: languageVersion,
                kind: treatAsScript ? SourceCodeKind.Script : SourceCodeKind.Regular);

            var tree = CSharpSyntaxTree.ParseText(code, parseOptions);

            // Collect only errors (ignore warnings/infos) from the parser/lexer
            syntaxErrors = tree
                .GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error && !d.IsSuppressed)
                .ToArray();

            return syntaxErrors.Length == 0;
        }
    }
}
