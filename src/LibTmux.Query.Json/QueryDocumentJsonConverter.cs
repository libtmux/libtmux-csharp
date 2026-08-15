using System.Text.Json;
using System.Text.Json.Serialization;

namespace LibTmux.Query.Json;

/// <summary>Reads and writes the stable v1 wire form of a query document.</summary>
/// <remarks>
/// The wire form is hand-written rather than reflection-derived so the schema
/// is decoupled from the CLR shape: renaming a record property must not change
/// the bytes a v1 reader expects.
/// </remarks>
internal sealed class QueryDocumentJsonConverter : JsonConverter<QueryDocument>
{
    private readonly QueryJsonLimits _limits;
    private int _nodes;

    internal QueryDocumentJsonConverter(QueryJsonLimits limits) => _limits = limits;

    public override QueryDocument Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        throw new NotSupportedException("Query documents are read through QueryJson.");

    public override void Write(
        Utf8JsonWriter writer,
        QueryDocument value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        _nodes = 0;
        writer.WriteStartObject();
        writer.WriteString("schema", value.Schema);
        writer.WriteNumber("version", value.Version);
        writer.WriteString("target", Wire(value.Target));
        writer.WritePropertyName("predicate");
        WriteNode(writer, value.Predicate, depth: 1);
        writer.WriteEndObject();
    }

    private static string Wire(QueryTarget target) => target switch
    {
        QueryTarget.Session => "session",
        QueryTarget.Window => "window",
        QueryTarget.Pane => "pane",
        _ => "client",
    };

    private static string Wire(QueryComparison comparison) => comparison switch
    {
        QueryComparison.Equal => "eq",
        QueryComparison.NotEqual => "ne",
        QueryComparison.LessThan => "lt",
        QueryComparison.LessThanOrEqual => "le",
        QueryComparison.GreaterThan => "gt",
        _ => "ge",
    };

    private static string Wire(QueryStringOperation operation) => operation switch
    {
        QueryStringOperation.EqualsOrdinal => "equals",
        QueryStringOperation.EqualsOrdinalIgnoreCase => "equalsIgnoreCase",
        QueryStringOperation.StartsWithOrdinal => "startsWith",
        QueryStringOperation.EndsWithOrdinal => "endsWith",
        _ => "contains",
    };

    private void WriteNode(Utf8JsonWriter writer, QueryNode node, int depth)
    {
        if (depth > _limits.MaximumDepth)
        {
            throw new JsonException("Query document exceeds the maximum nesting depth.");
        }

        if (++_nodes > _limits.MaximumNodes)
        {
            throw new JsonException("Query document exceeds the maximum node count.");
        }

        writer.WriteStartObject();
        switch (node)
        {
            case AndNode and:
                WriteOperands(writer, "and", and.Operands, depth);
                break;
            case OrNode or:
                WriteOperands(writer, "or", or.Operands, depth);
                break;
            case NotNode not:
                writer.WriteString("kind", "not");
                writer.WritePropertyName("operand");
                WriteNode(writer, not.Operand, depth + 1);
                break;
            case ComparisonNode comparison:
                writer.WriteString("kind", "comparison");
                writer.WriteString("operator", Wire(comparison.Operator));
                WritePair(writer, comparison.Left, comparison.Right, depth);
                break;
            case StringNode text:
                writer.WriteString("kind", "string");
                writer.WriteString("operator", Wire(text.Operator));
                WritePair(writer, text.Left, text.Right, depth);
                break;
            case RegexNode regex:
                WriteRegex(writer, regex, depth);
                break;
            case QuantifierNode quantifier:
                writer.WriteString("kind", "quantifier");
                writer.WriteString(
                    "quantifier",
                    quantifier.Quantifier == QueryQuantifier.Any ? "any" : "all");
                writer.WritePropertyName("relation");
                WriteNode(writer, quantifier.Relation, depth + 1);
                writer.WritePropertyName("predicate");
                WriteNode(writer, quantifier.Predicate, depth + 1);
                break;
            case FieldNode field:
                writer.WriteString("kind", "field");
                writer.WriteString("target", Wire(field.Target));
                writer.WriteString("name", field.WireName);
                break;
            case ConstantNode constant:
                writer.WriteString("kind", "constant");
                WriteConstant(writer, constant.Value);
                break;
            default:
                throw new JsonException($"Node '{node.GetType().Name}' has no v1 wire form.");
        }

        writer.WriteEndObject();
    }

    private void WriteRegex(Utf8JsonWriter writer, RegexNode regex, int depth)
    {
        if (regex.Pattern.Length > _limits.MaximumPatternLength)
        {
            throw new JsonException("Regex pattern exceeds the maximum length.");
        }

        writer.WriteString("kind", "regex");
        writer.WriteString("dialect", regex.Dialect);
        writer.WriteString("pattern", regex.Pattern);
        writer.WriteNumber("semanticOptions", (int)regex.SemanticOptions);
        writer.WritePropertyName("input");
        WriteNode(writer, regex.Input, depth + 1);
    }

    private void WriteOperands(
        Utf8JsonWriter writer,
        string kind,
        IReadOnlyList<QueryNode> operands,
        int depth)
    {
        writer.WriteString("kind", kind);
        writer.WriteStartArray("operands");
        foreach (QueryNode operand in operands)
        {
            WriteNode(writer, operand, depth + 1);
        }

        writer.WriteEndArray();
    }

    private void WritePair(Utf8JsonWriter writer, QueryNode left, QueryNode right, int depth)
    {
        writer.WritePropertyName("left");
        WriteNode(writer, left, depth + 1);
        writer.WritePropertyName("right");
        WriteNode(writer, right, depth + 1);
    }

    private void WriteConstant(Utf8JsonWriter writer, QueryConstant constant)
    {
        switch (constant)
        {
            case NullConstant:
                writer.WriteString("type", "null");
                writer.WriteNull("value");
                break;
            case BooleanConstant boolean:
                writer.WriteString("type", "boolean");
                writer.WriteBoolean("value", boolean.Value);
                break;
            case Int64Constant number:
                writer.WriteString("type", "int64");
                writer.WriteNumber("value", number.Value);
                break;
            case StringConstant text:
                writer.WriteString("type", "string");
                WriteBoundedString(writer, text.Value);
                break;
            case InstantConstant instant:
                writer.WriteString("type", "instant");
                writer.WriteNumber("value", instant.UnixSeconds);
                break;
            case EnumConstant member:
                writer.WriteString("type", "enum");
                writer.WriteString("enumType", member.Type);
                WriteBoundedString(writer, member.Value);
                break;
            case TypedIdConstant id:
                writer.WriteString("type", "typedId");
                writer.WriteString("target", Wire(id.Target));
                WriteBoundedString(writer, id.Value);
                break;
            default:
                throw new JsonException(
                    $"Constant '{constant.GetType().Name}' has no v1 wire form.");
        }
    }

    private void WriteBoundedString(Utf8JsonWriter writer, string value)
    {
        if (value.Length > _limits.MaximumStringLength)
        {
            throw new JsonException("String value exceeds the maximum length.");
        }

        // A lone surrogate cannot round-trip through UTF-8, so it must never
        // reach the wire.
        foreach (char character in value)
        {
            if (char.IsSurrogate(character) && !char.IsSurrogatePair(value, value.IndexOf(character, StringComparison.Ordinal)))
            {
                throw new JsonException("String value contains an unpaired surrogate.");
            }
        }

        writer.WriteString("value", value);
    }
}

/// <summary>Reads the stable v1 wire form back into a query document.</summary>
internal sealed class QueryDocumentJsonReader
{
    private readonly QueryJsonLimits _limits;
    private int _nodes;

    internal QueryDocumentJsonReader(QueryJsonLimits limits) => _limits = limits;

    internal static QueryTarget ReadTarget(JsonElement element) =>
        element.GetString() switch
        {
            "session" => QueryTarget.Session,
            "window" => QueryTarget.Window,
            "pane" => QueryTarget.Pane,
            "client" => QueryTarget.Client,
            _ => throw new JsonException("Query document names an unknown target."),
        };

    internal QueryNode ReadNode(JsonElement element, int depth)
    {
        if (depth > _limits.MaximumDepth)
        {
            throw new JsonException("Query document exceeds the maximum nesting depth.");
        }

        if (++_nodes > _limits.MaximumNodes)
        {
            throw new JsonException("Query document exceeds the maximum node count.");
        }

        return element.GetProperty("kind").GetString() switch
        {
            "and" => new AndNode([.. ReadOperands(element, depth)]),
            "or" => new OrNode([.. ReadOperands(element, depth)]),
            "not" => new NotNode(ReadNode(element.GetProperty("operand"), depth + 1)),
            "comparison" => new ComparisonNode(
                ReadComparison(element.GetProperty("operator")),
                ReadNode(element.GetProperty("left"), depth + 1),
                ReadNode(element.GetProperty("right"), depth + 1)),
            "string" => new StringNode(
                ReadStringOperation(element.GetProperty("operator")),
                ReadNode(element.GetProperty("left"), depth + 1),
                ReadNode(element.GetProperty("right"), depth + 1)),
            "regex" => new RegexNode(
                ReadNode(element.GetProperty("input"), depth + 1),
                element.GetProperty("dialect").GetString() ?? "dotnet",
                element.GetProperty("pattern").GetString() ?? string.Empty,
                (System.Text.RegularExpressions.RegexOptions)element
                    .GetProperty("semanticOptions")
                    .GetInt32()),
            "quantifier" => new QuantifierNode(
                element.GetProperty("quantifier").GetString() == "any"
                    ? QueryQuantifier.Any
                    : QueryQuantifier.All,
                (FieldNode)ReadNode(element.GetProperty("relation"), depth + 1),
                ReadNode(element.GetProperty("predicate"), depth + 1)),
            "field" => new FieldNode(
                ReadTarget(element.GetProperty("target")),
                element.GetProperty("name").GetString()
                    ?? throw new JsonException("Field names no wire name.")),
            "constant" => new ConstantNode(ReadConstant(element)),
            _ => throw new JsonException("Query document names an unknown node kind."),
        };
    }

    private static QueryComparison ReadComparison(JsonElement element) =>
        element.GetString() switch
        {
            "eq" => QueryComparison.Equal,
            "ne" => QueryComparison.NotEqual,
            "lt" => QueryComparison.LessThan,
            "le" => QueryComparison.LessThanOrEqual,
            "gt" => QueryComparison.GreaterThan,
            "ge" => QueryComparison.GreaterThanOrEqual,
            _ => throw new JsonException("Query document names an unknown comparison."),
        };

    private static QueryStringOperation ReadStringOperation(JsonElement element) =>
        element.GetString() switch
        {
            "equals" => QueryStringOperation.EqualsOrdinal,
            "equalsIgnoreCase" => QueryStringOperation.EqualsOrdinalIgnoreCase,
            "startsWith" => QueryStringOperation.StartsWithOrdinal,
            "endsWith" => QueryStringOperation.EndsWithOrdinal,
            "contains" => QueryStringOperation.ContainsOrdinal,
            _ => throw new JsonException("Query document names an unknown string operation."),
        };

    private static QueryConstant ReadConstant(JsonElement element) =>
        element.GetProperty("type").GetString() switch
        {
            "null" => new NullConstant(),
            "boolean" => new BooleanConstant(element.GetProperty("value").GetBoolean()),
            "int64" => new Int64Constant(element.GetProperty("value").GetInt64()),
            "string" => new StringConstant(
                element.GetProperty("value").GetString() ?? string.Empty),
            "instant" => new InstantConstant(element.GetProperty("value").GetInt64()),
            "enum" => new EnumConstant(
                element.GetProperty("enumType").GetString() ?? string.Empty,
                element.GetProperty("value").GetString() ?? string.Empty),
            "typedId" => new TypedIdConstant(
                ReadTarget(element.GetProperty("target")),
                element.GetProperty("value").GetString() ?? string.Empty),
            _ => throw new JsonException("Query document names an unknown constant type."),
        };

    private IEnumerable<QueryNode> ReadOperands(JsonElement element, int depth)
    {
        foreach (JsonElement operand in element.GetProperty("operands").EnumerateArray())
        {
            yield return ReadNode(operand, depth + 1);
        }
    }
}
