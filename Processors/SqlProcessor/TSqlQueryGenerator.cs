using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlProcessor
{
    public class TSqlQueryGenerator(
        string query,
        IEnumerable<(string TableName, string FieldName)>? requiredTables = null,
        IEnumerable<RestrictedQuery>? restrictedQueries = null)
    {
        private readonly string _originalQuery = query ?? throw new ArgumentNullException(nameof(query));

        public string Render(out Dictionary<string, List<(string KeyWithPosition, object? DefaultValue)>> createdParameters)
        {
            using var reader = new StringReader(_originalQuery);
            var parser = new TSql130Parser(true);

            var tree = parser.Parse(reader, out var errors);

            if (errors.Count > 0)
            {
                throw new InvalidProgramException($"Thrown errors in query: {string.Join(",", errors.Select(i => i.Message))}");
            }

            var tsqlBatchVisitor = new TSqlBatchVisitor(requiredTables, restrictedQueries);
            tree.Accept(tsqlBatchVisitor);
            createdParameters = tsqlBatchVisitor.CreatedParameters;

            var generator = new Sql130ScriptGenerator();
            generator.GenerateScript(tree, out var result);

            return result;
        }
    }

    public sealed class TSqlBatchVisitor(
        IEnumerable<(string TableName, string FieldName)>? requiredTables,
        IEnumerable<RestrictedQuery>? restrictedQueries)
        : TSqlFragmentVisitor
    {
        private delegate bool ScalarExpressionValidationDelegate(string? variableName, params ScalarExpression?[] values);

        #region Static
#pragma warning disable IDE0052 // Remove unread private members
        private readonly static ScalarExpressionValidationDelegate __noopValidation = (v, exprs) => true;
#pragma warning restore IDE0052 // Remove unread private members

        private readonly static ScalarExpressionValidationDelegate __validateAreAllConstantsCondition =
            (v, exprs) => !AreAllLiterals(exprs);
        private static bool AreAllLiterals(params ScalarExpression?[] expressions) => // TODO: merge with above
            expressions != null &&
            expressions.Length != 0 &&
            expressions.All(c => TryGetScalarPrefix(GetLiteralOrNull(c), out _));

        private static object? GetLiteralValueOrNull(ScalarExpression? scalarExpression) =>
            // consider TOP level UnaryExpression as exception and look at first child instead
            scalarExpression is UnaryExpression unaryExpression &&
            TryGetScalarValue(unaryExpression.Expression as Literal, unaryExpressionType: unaryExpression.UnaryExpressionType, out var value)
                ? value
                : (TryGetScalarValue(GetLiteralOrNull(scalarExpression), unaryExpressionType: null, out value) ? value : null);
        private static Literal? GetLiteralOrNull(ScalarExpression? scalarExpression) =>
            // 1
            (scalarExpression as Literal) ??
            // 1 * 2
            (scalarExpression is BinaryExpression binaryExpression
                ? (GetLiteralOrNull(binaryExpression.FirstExpression)) ?? (GetLiteralOrNull(binaryExpression.SecondExpression))
                : null) ??
            // -1
            (scalarExpression is UnaryExpression unaryExpression
                ? GetLiteralOrNull(unaryExpression.Expression)
                : null);

        public static readonly IEnumerable<RestrictedQuery> DefaultRestrictedQueries = Enum.GetValues(typeof(RestrictedQuery)).Cast<RestrictedQuery>();

        public static readonly IEnumerable<(string TableName, string ColumnName)> DefaultRequiredTables = [];
        #endregion

        #region Properties
        private readonly IEnumerable<(string TableName, string ColumnName)> _requiredTables = requiredTables ?? [];
        private readonly IEnumerable<RestrictedQuery> _restrictedQueries = restrictedQueries ?? [];

        public Dictionary<string, List<(string KeyWithPosition, object? DefaultValue)>> CreatedParameters { get; } = new Dictionary<string, List<(string Key, object? DefaultValue)>>(comparer: StringComparer.OrdinalIgnoreCase);

        #endregion Properties

        #region Methods
        // TOP X
        public override void ExplicitVisit(TopRowFilter node)
        {
            if (node.Expression is IntegerLiteral integerLiteral)
            {
                if (TryGetScalarPrefix(integerLiteral, out var _)) // Not really required check, use it to ensure that the constant in TOP is a literal
                {
                    node.Expression = SetVariableExpressionIfRequired(originalExpression: node.Expression, effectiveVariableName: "TOP");
                }
            }

            base.ExplicitVisit(node);
        }

        // FROM ..
        public override void ExplicitVisit(FromClause node)
        {
            if (node.TableReferences != null)
            {
                foreach (var table in IterateTables(node.TableReferences))
                {
                    AddHintIfRequired(table);
                }
            }

            base.ExplicitVisit(node);

            static void AddHintIfRequired(NamedTableReference namedTableReference)
            {
                // Do not add 'hk => hk.HintKind == TableHintKind.NoLock' condition below since the SQL Server
                // has restrictions on hints combinations for a table.
                // So, the suggested rule is: add 'nolock' hint automatically if no other hints are specified,
                // and leave only specified if there is any

                // Read more here https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver16#remarks

                if (!namedTableReference.TableHints.OfType<TableHint>().Any())
                {
                    // TODO: Handling only NoLock for now, consider adding the rest later..
                    namedTableReference.TableHints.Add(new TableHint() { HintKind = TableHintKind.NoLock });
                }
            }
        }


        // having ..
        public override void ExplicitVisit(HavingClause node)
        {
            // Use __noopValidation instead __validateAreAllConstantsCondition if validation on only conts in the query is not required
            if (!ProcessWhereStep(node.SearchCondition, validateFunc: __validateAreAllConstantsCondition))
            {
                // TODO: create a better error message
                throw new InvalidOperationException("At least one filter condition must consist healthy query fragment, please validate it");
            }

            base.ExplicitVisit(node);
        }

        // inner/left/right/full join
        public override void ExplicitVisit(QualifiedJoin node)
        {
            // Use __noopValidation instead __validateAreAllConstantsCondition if validation on only conts in the query is not required

            if (!ProcessWhereStep(node.SearchCondition, validateFunc: __validateAreAllConstantsCondition))
            {
                // TODO: create a better error message
                throw new InvalidOperationException("At least one filter condition must consist healthy query fragment, please validate it");
            }

            base.ExplicitVisit(node);
        }

        // where ...
        public override void ExplicitVisit(WhereClause node)
        {
            // Use __noopValidation instead __validateAreAllConstantsCondition if validation on only conts in the query is not required

            if (!ProcessWhereStep(node.SearchCondition, validateFunc: __validateAreAllConstantsCondition))
            {
                // TODO: create a better error message
                throw new InvalidOperationException("At least one filter condition must consist healthy query fragment, please validate it");
            }

            base.ExplicitVisit(node);

        }

        // SELECT * ..
        public override void ExplicitVisit(QuerySpecification node)
        {
            foreach (var table in IterateTables(node.FromClause.TableReferences))
            {
                foreach (var schemaIdentifier in table.SchemaObject.Identifiers)
                {
                    foreach (var (TableName, ColumnName) in _requiredTables)
                    {
                        if (string.Equals(TableName, schemaIdentifier.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            var mockedColumn = CreateMockedColumn(ColumnName, table.Alias, table.SchemaObject.Identifiers);

                            if (node.WhereClause == null)
                            {
                                node.WhereClause = GetEffectiveWhereClause(mockedColumn, appendTo: null);
                            }
                            else
                            {
                                if (!ProcessWhereStep(fragment: node.WhereClause.SearchCondition, validateFunc: (variableName, _) => string.Equals(GetColumnNameFromVariableName(variableName), ColumnName, StringComparison.OrdinalIgnoreCase)))
                                {
                                    node.WhereClause = GetEffectiveWhereClause(mockedColumn, appendTo: node.WhereClause);
                                }
                            }
                        }
                    }
                }
            }

            base.ExplicitVisit(node);

            string GetColumnNameFromVariableName(string? variableName) => (variableName ?? string.Empty).Split('_').Last();

            ColumnReferenceExpression CreateMockedColumn(string requestedColumnName, Identifier alias, IList<Identifier> tableIdentifiers)
            {
                if (tableIdentifiers == null || tableIdentifiers.Count == 0)
                {
                    throw new InvalidOperationException("Table identifiers collection most be not empty"); // should not happen
                }

                var multiPartIdentifier = new MultiPartIdentifier();
                if (alias != null)
                {
                    multiPartIdentifier.Identifiers.Add(alias);
                    multiPartIdentifier.Identifiers.Add(new Identifier { Value = requestedColumnName });
                }
                else
                {
                    foreach (var identifier in tableIdentifiers)
                    {
                        multiPartIdentifier.Identifiers.Add(identifier);
                    }

                    multiPartIdentifier.Identifiers.Add(new Identifier { Value = requestedColumnName });
                }

                return new ColumnReferenceExpression
                {
                    MultiPartIdentifier = multiPartIdentifier
                };
            }

            WhereClause? GetEffectiveWhereClause(ColumnReferenceExpression column, WhereClause? appendTo = null)
            {
                if (TryGetEffectiveVariableNameFromColumnIfAny(column, out var effectiveVariableName))
                {
                    var defaultValue = new NullLiteral();
                    var variable = SetVariableExpressionIfRequired(originalExpression: defaultValue, effectiveVariableName);

                    BooleanExpression searchCondition;

                    if (appendTo != null)
                    {

                        /*
                         * BEFORE: FROM WHERE SomeField1 = 1 or SomeField2 = 2
                         * AFTER:  FROM WHERE (BinaryId = 1) AND (SomeField1 = 1 or SomeField2 = 2)
                         */

                        searchCondition = new BooleanBinaryExpression()
                        {
                            FirstExpression = new BooleanComparisonExpression
                            {
                                ComparisonType = BooleanComparisonType.Equals,
                                FirstExpression = column,
                                SecondExpression = variable
                            },
                            SecondExpression = appendTo.SearchCondition,
                            BinaryExpressionType = BooleanBinaryExpressionType.And
                        };
                    }
                    else
                    {
                        /*
                         * BEFORE: FROM
                         * AFTER:  FROM WHERE BinaryId = 1
                         */
                        searchCondition = new BooleanComparisonExpression
                        {
                            FirstExpression = column,
                            SecondExpression = variable,
                            ComparisonType = BooleanComparisonType.Equals,
                        };
                    }

                    return new WhereClause { SearchCondition = searchCondition };
                }

                return appendTo;
            }
        }

        // Restricted operation ...
        public override void ExplicitVisit(TSqlBatch node)
        {
            if (node != null)
            {
                foreach (var statement in node.Statements)
                {
                    switch (statement)
                    {
                        case DropDatabaseStatement dropDatabase when _restrictedQueries.Contains(RestrictedQuery.DROP_DATABASE):
                            throw CreateRestrictedQueryException(RestrictedQuery.DROP_DATABASE);

                        case DropTableStatement dropTable when _restrictedQueries.Contains(RestrictedQuery.DROP_TABLE):
                            throw CreateRestrictedQueryException(RestrictedQuery.DROP_TABLE);

                        case DropIndexStatement dropIndex when _restrictedQueries.Contains(RestrictedQuery.DROP_INDEX):
                            throw CreateRestrictedQueryException(RestrictedQuery.DROP_INDEX);

                        case DropProcedureStatement dropProcedure when _restrictedQueries.Contains(RestrictedQuery.DROP_PROCEDURE):
                            throw CreateRestrictedQueryException(RestrictedQuery.DROP_PROCEDURE);

                        case CreateTableStatement createTable when _restrictedQueries.Contains(RestrictedQuery.CREATE_TABLE):
                            throw CreateRestrictedQueryException(RestrictedQuery.CREATE_TABLE);

                        case CreateDatabaseStatement createDatabase when _restrictedQueries.Contains(RestrictedQuery.CREATE_DATABASE):
                            throw CreateRestrictedQueryException(RestrictedQuery.CREATE_DATABASE);

                        case CreateProcedureStatement createProcedure when _restrictedQueries.Contains(RestrictedQuery.CREATE_PROCEDURE):
                            throw CreateRestrictedQueryException(RestrictedQuery.CREATE_PROCEDURE);

                        case CreateFunctionStatement createFunction when _restrictedQueries.Contains(RestrictedQuery.CREATE_FUNCTION):
                            throw CreateRestrictedQueryException(RestrictedQuery.CREATE_FUNCTION);

                        case InsertStatement insert when _restrictedQueries.Contains(RestrictedQuery.INSERT):
                            throw CreateRestrictedQueryException(RestrictedQuery.INSERT);

                        case UpdateStatement updateTable when _restrictedQueries.Contains(RestrictedQuery.UPDATE):
                            throw CreateRestrictedQueryException(RestrictedQuery.UPDATE);

                        case DeleteStatement deleteTable when _restrictedQueries.Contains(RestrictedQuery.DELETE):
                            throw CreateRestrictedQueryException(RestrictedQuery.DELETE);

                        case TruncateTableStatement truncateTable when _restrictedQueries.Contains(RestrictedQuery.TRUNCATE):
                            throw CreateRestrictedQueryException(RestrictedQuery.TRUNCATE);

                        case BeginTransactionStatement beginTran when _restrictedQueries.Contains(RestrictedQuery.TRANSACTION_BEGIN):
                            throw CreateRestrictedQueryException(RestrictedQuery.TRANSACTION_BEGIN);

                        case CommitTransactionStatement commitTran when _restrictedQueries.Contains(RestrictedQuery.TRANSACTION_COMMIT):
                            throw CreateRestrictedQueryException(RestrictedQuery.TRANSACTION_COMMIT);

                        case RollbackTransactionStatement rollbackTran when _restrictedQueries.Contains(RestrictedQuery.TRANSACTION_ROLLBACK):
                            throw CreateRestrictedQueryException(RestrictedQuery.TRANSACTION_ROLLBACK);

                        case AlterTableStatement alterTable when _restrictedQueries.Contains(RestrictedQuery.ALTER_TABLE):
                            throw CreateRestrictedQueryException(RestrictedQuery.ALTER_TABLE);

                        case AlterProcedureStatement alterProc when _restrictedQueries.Contains(RestrictedQuery.ALTER_PROCEDURE):
                            throw CreateRestrictedQueryException(RestrictedQuery.ALTER_PROCEDURE);

                        case AlterFunctionStatement alterFunction when _restrictedQueries.Contains(RestrictedQuery.ALTER_FUNCTION):
                            throw CreateRestrictedQueryException(RestrictedQuery.ALTER_FUNCTION);
                    }
                }

                base.ExplicitVisit(node);
            }

            static Exception CreateRestrictedQueryException(RestrictedQuery restrictedQuery) =>
                new InvalidOperationException($"{restrictedQuery} is not allowed in the query in this tool. Please contact the DBA Team.");
        }


        // each supported where step
        private bool ProcessWhereStep(BooleanExpression fragment, ScalarExpressionValidationDelegate validateFunc)
        {
            /*
             * The logic in BooleanBinaryExpression uses 'logical OR' with the below rules:
_____________________
x          y          x|y
true       true       true
true       false      true
false      true       true
false      false      false
---------------------
                The target is to have at least one healthy condition in the where subqueries to consider the whole query as healthy
             */

            return fragment switch
            {
                BooleanBinaryExpression booleanBinaryExpression => ProcessBooleanBinaryExpression(booleanBinaryExpression, validateFunc),
                BooleanComparisonExpression searchExpression => ProcessBooleanComparisonExpression(searchExpression, validateFunc),
                BooleanIsNullExpression searchIsNullExpression => ProcessBooleanIsNullExpression(searchIsNullExpression, validateFunc),
                BooleanTernaryExpression booleanTernaryExpression => ProcessBooleanTernaryExpression(booleanTernaryExpression, validateFunc),
                LikePredicate likePredicate => ProcessLikeExpression(likePredicate, validateFunc),
                InPredicate inPredicate => ProcessInPredicate(inPredicate, validateFunc),
                BooleanNotExpression booleanNotExpression => ProcessBooleanNotExpression(booleanNotExpression, validateFunc),
                BooleanParenthesisExpression booleanParenthesisExpression => ProcessBooleanParenthesisExpression(booleanParenthesisExpression, validateFunc),
                _ => false,
            };
        }

        // =, <>, >=, <=
        private bool ProcessBooleanComparisonExpression(BooleanComparisonExpression booleanComparisonExpression, ScalarExpressionValidationDelegate validationFunc)
        {
            var left = booleanComparisonExpression.FirstExpression;
            var right = booleanComparisonExpression.SecondExpression;

            if (TryGetEffectiveVariableNameFromColumnIfAny(left, right, out var effectiveVariableName))
            {
                booleanComparisonExpression.FirstExpression = SetVariableExpressionIfRequired(originalExpression: booleanComparisonExpression.FirstExpression, effectiveVariableName);
                booleanComparisonExpression.SecondExpression = SetVariableExpressionIfRequired(originalExpression: booleanComparisonExpression.SecondExpression, effectiveVariableName);
            }

            return validationFunc(effectiveVariableName, left, right);
        }

        // field is not null / field is null
#pragma warning disable IDE0060 // Remove unused parameter
        private bool ProcessBooleanIsNullExpression(BooleanIsNullExpression booleanIsNullExpression, ScalarExpressionValidationDelegate validateFunc)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            var left = booleanIsNullExpression.Expression;

#pragma warning disable IDE0059 // Unnecessary assignment of a value
            if (TryGetEffectiveVariableNameFromColumnIfAny(left, out var effectiveVariableName))
            {
                if (TryGetScalarPrefix(new NullLiteral(), out var prefix))
                {
                    /*

                     * TODO (NOT READY):
                     * Instead above, generate something like this:
                        (@b_ColumnName = 1 AND ColumnName IS NULL)
                        OR
                        (@b_ColumnName = 0 AND ColumnName IS NOT NULL)
                     */
                }
                else
                {
                    throw new InvalidOperationException($"TODO: not implemented yet");
                }
            }
#pragma warning restore IDE0059 // Unnecessary assignment of a value

            // TODO: back to this when the above question will be clarified, do not make any logic here yet
            return true;
        }

        // BinaryExpressionType => And / Or
        private bool ProcessBooleanBinaryExpression(BooleanBinaryExpression booleanBinaryExpression, ScalarExpressionValidationDelegate validateFunc) =>
            ProcessWhereStep(booleanBinaryExpression.FirstExpression, validateFunc) |
            ProcessWhereStep(booleanBinaryExpression.SecondExpression, validateFunc);


        // Field between 1 and 10
        private bool ProcessBooleanTernaryExpression(BooleanTernaryExpression booleanTernaryExpression, ScalarExpressionValidationDelegate validateFunc)
        {
            var columnLeft = booleanTernaryExpression.FirstExpression;
            var middle = booleanTernaryExpression.SecondExpression;
            var right = booleanTernaryExpression.ThirdExpression;

            if (TryGetEffectiveVariableNameFromColumnIfAny(columnLeft, out var effectiveVariableName))
            {
                booleanTernaryExpression.SecondExpression = SetVariableExpressionIfRequired(originalExpression: booleanTernaryExpression.SecondExpression, effectiveVariableName);
                booleanTernaryExpression.ThirdExpression = SetVariableExpressionIfRequired(originalExpression: booleanTernaryExpression.ThirdExpression, effectiveVariableName);
            }

            return validateFunc(effectiveVariableName, columnLeft, middle, right);
        }

        // like ..
        private bool ProcessLikeExpression(LikePredicate likePredicate, ScalarExpressionValidationDelegate validateFunc)
        {
            var columnLeft = likePredicate.FirstExpression;
            var right = likePredicate.SecondExpression;

            if (TryGetEffectiveVariableNameFromColumnIfAny(columnLeft, out var effectiveVariableName))
            {
                likePredicate.SecondExpression = SetVariableExpressionIfRequired(originalExpression: likePredicate.SecondExpression, effectiveVariableName);
            }

            return validateFunc(effectiveVariableName, columnLeft, right);
        }

        // in (1,2,3)
        private bool ProcessInPredicate(InPredicate inPredicate, ScalarExpressionValidationDelegate validateFunc)
        {
            var left = inPredicate.Expression;

            if (TryGetEffectiveVariableNameFromColumnIfAny(left, out var effectiveVariableName))
            {
                for (var i = 0; i < inPredicate.Values.Count; i++)
                {
                    inPredicate.Values[i] = SetVariableExpressionIfRequired(originalExpression: inPredicate.Values[i], effectiveVariableName);
                }
            }

            return validateFunc(effectiveVariableName, left);
        }

        // NOT FieldName = 'test'
        private bool ProcessBooleanNotExpression(BooleanNotExpression booleanNotExpression, ScalarExpressionValidationDelegate validateFunc) =>
            ProcessWhereStep(booleanNotExpression.Expression, validateFunc);

        // Process '('  and ')'
        private bool ProcessBooleanParenthesisExpression(BooleanParenthesisExpression booleanParenthesisExpression, ScalarExpressionValidationDelegate validateFunc) =>
            ProcessWhereStep(booleanParenthesisExpression.Expression, validateFunc);
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

        private static bool TryGetScalarPrefix(Literal? literal, out string? scalarPrefix)
        {
            scalarPrefix = null;

            switch (literal)
            {
                case RealLiteral:
                case MoneyLiteral:
                case NumericLiteral:
                case IntegerLiteral:
                case BinaryLiteral:
                    scalarPrefix = "@n_";
                    return true;
                case StringLiteral:
                case NullLiteral:
                    scalarPrefix = "@s_";
                    return true;
            }

            return false;
        }

        private static bool TryGetScalarValue(
            Literal? literal,
            UnaryExpressionType? unaryExpressionType, // TODO: probably not the best place for this logic
            out object? value)
        {
            value = null;
            if (literal == null) return false;

            // TODO: find a better approach that will make more straight difference between numeric types and string/null/binaries
            // TODO2: consider UnaryExpressionType.BitwiseNot too
            int modifier = unaryExpressionType.HasValue && unaryExpressionType.Value == UnaryExpressionType.Negative
                ? -1
                : 1;

            // see for details: https://learn.microsoft.com/en-us/sql/relational-databases/clr-integration-database-objects-types-net-framework/mapping-clr-parameter-data?view=sql-server-ver16&redirectedfrom=MSDN
            value = literal switch
            {
                RealLiteral => modifier * Single.Parse(literal.Value),
                MoneyLiteral or NumericLiteral => modifier * decimal.Parse(literal.Value),
                IntegerLiteral => modifier * int.Parse(literal.Value),
                BinaryLiteral => literal.Value, // let's save it as string for now
                StringLiteral => literal.Value,
                NullLiteral => null,
                _ => literal.Value,
            };

            return true;
        }

        private bool TryGetEffectiveVariableNameFromColumnIfAny(
            ScalarExpression? left,
            out string? effectiveVariableName) => TryGetEffectiveVariableNameFromColumnIfAny(left, right: null, out effectiveVariableName);

        private bool TryGetEffectiveVariableNameFromColumnIfAny(
            ScalarExpression? left,
            ScalarExpression? right,
            out string? effectiveVariableName)
        {
            var columnExpression = left as ColumnReferenceExpression ?? right as ColumnReferenceExpression; // left or right operant in operation

            effectiveVariableName = null;

            if (columnExpression != null)
            {
                effectiveVariableName = GetColumnName(columnExpression);
                return true;
            }
            else
            {
                // if on the right side is not a column but column in the function like `ISNULL`
                var functionCall = left as FunctionCall ?? right as FunctionCall; // left or right operant in operation
                if (functionCall != null)
                {
                    // try to find a column parameter
                    if (functionCall.Parameters == null || !functionCall.Parameters.Any(p => p is ColumnReferenceExpression))
                    {
                        /* not really a real life example, but can happen for queries like:
                         * WHERE  ISNULL(1, 1) = ISNULL(1, 1) where both function are constants */
                        return false;
                    }

                    // take the first found column

                    columnExpression = functionCall.Parameters.OfType<ColumnReferenceExpression>().First();
                    effectiveVariableName = $"{functionCall.FunctionName.Value}_{GetColumnName(columnExpression)}";
                    return true;
                }
            }
            return false;

            static string GetColumnName(ColumnReferenceExpression columnExpression)
            {
                var identifiers = columnExpression.MultiPartIdentifier?.Identifiers;
                if (identifiers == null || identifiers.Count == 0)
                {
                    throw new NotSupportedException($"MultiPartIdentifier must have at least one part");
                }
                return string.Join("_", columnExpression!.MultiPartIdentifier!.Identifiers.Select(i => i.Value));
            }
        }

        private ScalarExpression? SetVariableExpressionIfRequired(
            ScalarExpression? originalExpression,
            string? effectiveVariableName)
        {
            if (TryCreateVariableExpression(effectiveVariableName, originalExpression, out var variable))
            {
                return variable;
            }
            return originalExpression; // do nothing in this case
        }

        private bool TryCreateVariableExpression(string? effectiveVariableName, ScalarExpression? scalarExpression, out VariableReference? variable)
        {
            variable = null;

            var valueExpression = GetLiteralOrNull(scalarExpression);

            if (valueExpression != null)
            {
                if (TryGetScalarPrefix(valueExpression, out var prefix))
                {
                    var defaultValue = GetLiteralValueOrNull(scalarExpression);
                    var variableReferenceName = GetEffectiveVariableNameWithPosition($"{prefix}{effectiveVariableName}", @default: defaultValue);
                    // ScriptTokenStream is not updated, but that is expected
                    variable = new VariableReference { Name = variableReferenceName };
                    return true;
                }
                else
                {
                    throw new NotSupportedException($"Unsupported literal type {valueExpression.GetType()} of '{valueExpression.Value}'");
                }
            }

            return false;
        }

        private string GetEffectiveVariableNameWithPosition(string variableName, object? @default)
        {
            string result;
            if (CreatedParameters.TryGetValue(variableName,
                out List<(string KeyWithPosition, object? DefaultValue)>? list))
            {
                list.Add(
                    (result = $"{variableName}_{list.Count}", // count is +1 to the actual position
                    @default));
            }
            else
            {
                CreatedParameters.Add(
                    variableName,
                    [
                        (result = $"{variableName}_0", // 0 position
                        @default)
                    ]);
            }
            return result;
        }
    }

    // Restricted keywords
    public enum RestrictedQuery
    {
        CREATE_TABLE,
        CREATE_DATABASE,
        CREATE_PROCEDURE,
        CREATE_FUNCTION,
        INSERT,
        UPDATE,
        DELETE,
        TRUNCATE,
        DROP_TABLE,
        ALTER_TABLE,
        ALTER_PROCEDURE,
        ALTER_FUNCTION,
        TRANSACTION_BEGIN,
        TRANSACTION_COMMIT,
        TRANSACTION_ROLLBACK,
        DROP_DATABASE,
        DROP_INDEX,
        DROP_PROCEDURE
    }
}