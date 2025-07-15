using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlProcessor
{
    public sealed class UnsupportedSelectArgumentsException(
        string message,
        string tableName,
        IEnumerable<string> unsupportedFields,
        IEnumerable<string> supportedFields) : Exception
    {
        public override string Message => $"{message.TrimEnd('.')} ['{string.Join("','", unsupportedFields)}']";
        public string TableName => tableName;
        public IEnumerable<string> UnsupportedFields => unsupportedFields;
        public IEnumerable<string> SupportedFields => supportedFields;
    }

    public class TSqlBatchSelectParametersGenerator(
        string query,
        IEnumerable<(string TableName, IEnumerable<string> SupportedField)>? tablesInfo = null)
    {
        private readonly string _originalQuery = query ?? throw new ArgumentNullException(nameof(query));

        public string Render()
        {
            using var reader = new StringReader(_originalQuery);
            var parser = new TSql130Parser(true);

            var tree = parser.Parse(reader, out var errors);

            if (errors.Count > 0)
            {
                throw new InvalidProgramException($"Thrown errors in query: {string.Join(",", errors.Select(i => i.Message))}");
            }

            var tsqlBatchVisitor = new TSqlBatchSelectParametersVisitor(tablesInfo, throwIfNotSupported: false);

            tree.Accept(tsqlBatchVisitor);

            var generator = new Sql130ScriptGenerator(
                new SqlScriptGeneratorOptions
                {
                    NewLineBeforeCloseParenthesisInMultilineList = false,
                    NewLineBeforeFromClause = false,
                    NewLineBeforeGroupByClause = false,
                    NewLineBeforeHavingClause = false,
                    NewLineBeforeJoinClause = false,
                    NewLineBeforeOffsetClause = false,
                    NewLineBeforeOpenParenthesisInMultilineList = false,
                    NewLineBeforeOrderByClause = false,
                    NewLineBeforeOutputClause = false,
                    NewLineBeforeWhereClause = false,
                    NewLineBeforeWindowClause = false,
                    AlignClauseBodies = false,
                    AlignColumnDefinitionFields = false,
                    AlignSetClauseItem = false,
                    IncludeSemicolons = false,
                    NumNewlinesAfterStatement = 0
                });
            generator.GenerateScript(tree, out var result);

            return result!;
        }

        public bool TryValidate(out string? errorMessage)
        {
            errorMessage = null;

            using var reader = new StringReader(_originalQuery);
            var parser = new TSql130Parser(true);

            var tree = parser.Parse(reader, out var errors);

            if (errors.Count > 0)
            {
                throw new InvalidProgramException($"Thrown errors in query: {string.Join(",", errors.Select(i => i.Message))}");
            }

            var tsqlBatchVisitor = new TSqlBatchSelectParametersVisitor(tablesInfo, throwIfNotSupported: true);

            try
            {
                tree.Accept(tsqlBatchVisitor);
                return true;
            }
            catch (UnsupportedSelectArgumentsException ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }

    public sealed class TSqlBatchSelectParametersVisitor(
        IEnumerable<(string TableName, IEnumerable<string> SupportedFields)>? tablesInfo,
        bool throwIfNotSupported)
    : TSqlFragmentVisitor
    {
        #region Properties
        private readonly IEnumerable<(string TableName, IEnumerable<string> SupportedFields)> _supportedTables = tablesInfo ?? [];

        #endregion Properties

        #region Methods

        // SELECT * ..
        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.SelectElements == null || node.SelectElements.Count == 0)
            {
                throw new NotSupportedException("SELECT query must have columnt elements"); // TODO: is there case when SelectElement is null or empty?
            }
            var fields = node
                .SelectElements
                .Cast<SelectScalarExpression>()
                .Select(i =>
                {
                    var columnReference = (ColumnReferenceExpression)i.Expression;
                    return new
                    {
                        Original = i,
                        FieldName = string.Join(".", columnReference.MultiPartIdentifier.Identifiers.Select(ii => ii.Value))
                    };
                });

            foreach (var table in IterateTables(node.FromClause.TableReferences))
            {
                foreach (var schemaIdentifier in table.SchemaObject.Identifiers)
                {
                    foreach (var (TableName, SupportedFields) in _supportedTables)
                    {
                        if (string.Equals(TableName, schemaIdentifier.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            var unsupportedFields = fields
                                .Where(i => !SupportedFields.Contains(i.FieldName, StringComparer.OrdinalIgnoreCase)).ToList();

                            if (unsupportedFields != null && unsupportedFields.Any())
                            {
                                if (throwIfNotSupported)
                                {
                                    throw new UnsupportedSelectArgumentsException(
                                        message: $"The provided query has unsupported fields",
                                        tableName: TableName,
                                        unsupportedFields: unsupportedFields.Select(i => i.FieldName).ToList(),
                                        supportedFields: SupportedFields);
                                }
                                else
                                {
                                    foreach (var unsupportedField in unsupportedFields)
                                    {
                                        node.SelectElements.Remove(unsupportedField.Original);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        #endregion

        private IEnumerable<NamedTableReference> IterateTables(IList<TableReference> reference)
        {
            foreach (var tableReference in reference)
            {
                switch (tableReference)
                {
                    case QualifiedJoin qualifiedJoin:
                        yield return EnsureTableIsValid(qualifiedJoin.FirstTableReference, nameof(qualifiedJoin.FirstTableReference));
                        yield return EnsureTableIsValid(qualifiedJoin.SecondTableReference, nameof(qualifiedJoin.SecondTableReference));
                        break;

                    case NamedTableReference namedTableReference:
                        yield return namedTableReference;
                        break;

                    case QueryDerivedTable:
                        // do nothing, this case assumes that a table is an nested query that itself will be handled separatelly later
                        break;
                    default:
                        // TODO: if we're here, then update the above condition
                        throw new NotSupportedException($"Table reference type {tableReference.GetType().Name} is not supported yet");
                }
            }

            static NamedTableReference EnsureTableIsValid(TableReference tableReference, string key)
            {
                if (tableReference is NamedTableReference namedTable)
                {
                    return namedTable;
                }
                else
                {
                    throw new NotSupportedException($"{key} type {tableReference.GetType().Name} is not supported yet");
                }
            }
        }
    }
}
