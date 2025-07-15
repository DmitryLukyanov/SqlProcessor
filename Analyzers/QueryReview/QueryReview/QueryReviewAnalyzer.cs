using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using SqlProcessor;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace QueryReview
{
    /// <summary>
    /// Flags any direct assignment to the ADO.NET <c>CommandText</c> property so that raw SQL can be reviewed.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class QueryReviewAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TRSP01"; // TODO: rename into TR01

        private static readonly LocalizableString Title = "Review CommandText assignment";
        private static readonly LocalizableString MessageFormat = "The CommandText SQL query contains unexisted fields; review the embedded SQL.";
        private static readonly LocalizableString Description = "Detects direct assignments to ADO.NET CommandText property so that SQL strings can be audited or parameterised.";
        private const string Category = "Security";

        // TODO: remove hardcoded values
        private static readonly IEnumerable<(string TableName, IEnumerable<string> SupportedField)> __tablesInfo =
        [
            ("CustomersTable", ["Name", "LastName"])
        ];

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            // Analyse only user‑written code and run concurrently where possible.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Trigger for every simple assignment (includes object‑initialiser assignments)
            context.RegisterOperationAction(AnalyzeAssignment, OperationKind.SimpleAssignment);
        }

        private static void AnalyzeAssignment(OperationAnalysisContext context)
        {
            var assignment = (IAssignmentOperation)context.Operation;

            // target must be a property reference named "CommandText"
            if (assignment.Target is not IPropertyReferenceOperation propertyRef ||
                propertyRef.Property.Name != "CommandText")
            {
                return;
            }

            // Optional: verify the containing type looks like an ADO.NET command (ends with "Command")
            if (!propertyRef.Property.ContainingType.Name.EndsWith("Command"))
            {
                return;
            }

            // Try to extract the string being assigned
            var sqlOp = assignment.Value;

            // works for string literals and compile‑time concatenations like "SELECT " + "1"
            if (!sqlOp.ConstantValue.HasValue || sqlOp.ConstantValue.Value is not string sqlText)
            {
                return;
            }

            var generator = new TSqlBatchSelectParametersGenerator(
                sqlText,
                tablesInfo: __tablesInfo);

#pragma warning disable IDE0059 // Unnecessary assignment of a value
            if (!generator.TryValidate(out var errorMessage))
            {
                // Report the diagnostic at the property access location
                var diagnostic = Diagnostic.Create(Rule, sqlOp.Syntax.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
#pragma warning restore IDE0059 // Unnecessary assignment of a value
        }
    }
}
