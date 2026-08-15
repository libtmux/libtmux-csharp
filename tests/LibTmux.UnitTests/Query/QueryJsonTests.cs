using System.Text.Json;
using LibTmux.Query;
using LibTmux.Query.Json;

namespace LibTmux.UnitTests.Query;

public sealed class QueryJsonTests
{
    private sealed record Row(string SessionName, long SessionWindows);

    public static TheoryData<string, QueryDocument> Goldens =>
        new()
        {
            {
                "string-and-comparison",
                QueryExtensions.Translate<Row>(
                    row => row.SessionName.StartsWith("dev") && row.SessionWindows > 1)
            },
            {
                "negated-contains",
                QueryExtensions.Translate<Row>(row => !row.SessionName.Contains("prod"))
            },
            {
                "disjunction",
                QueryExtensions.Translate<Row>(
                    row => row.SessionName == "a" || row.SessionWindows <= 3)
            },
            { "legacy-name-contains", QueryEdgeParser.ParseNameContains(QueryTarget.Window, "log") },
        };

    [Theory]
    [MemberData(nameof(Goldens))]
    public void Round_trips_every_version_one_golden_byte_for_byte(
        string name,
        QueryDocument document)
    {
        Assert.NotEmpty(name);

        string json = QueryJson.Serialize(document);
        QueryDocument restored = QueryJson.Deserialize(json);

        // Byte-for-byte, not merely equivalent: the wire form is the stable
        // artifact, so a reserialized document must be indistinguishable.
        Assert.Equal(json, QueryJson.Serialize(restored));
        Assert.Equal(document, restored);
        Assert.DoesNotContain("\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Limits_may_tighten_the_frozen_ceilings_but_never_widen_them()
    {
        QueryDocument document =
            QueryEdgeParser.ParseNameContains(QueryTarget.Session, "dev");
        string json = QueryJson.Serialize(document);

        Assert.NotNull(QueryJson.Deserialize(json, QueryJsonLimits.V1 with { MaximumNodes = 8 }));
        // Widening would let this reader accept a document another v1 reader
        // must reject, which is exactly what a frozen schema forbids.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => QueryJson.Deserialize(json, QueryJsonLimits.V1 with { MaximumNodes = 4096 }));
    }

    [Fact]
    public void An_oversized_or_too_deep_document_is_refused()
    {
        QueryDocument document =
            QueryEdgeParser.ParseNameContains(QueryTarget.Session, "dev");
        string json = QueryJson.Serialize(document);

        Assert.Throws<JsonException>(
            () => QueryJson.Deserialize(json, QueryJsonLimits.V1 with { MaximumUtf8Bytes = 4 }));
        Assert.Throws<JsonException>(
            () => QueryJson.Deserialize(json, QueryJsonLimits.V1 with { MaximumNodes = 1 }));
    }

    [Fact]
    public void An_unknown_node_kind_is_refused_rather_than_guessed()
    {
        const string json =
            """{"schema":"libtmux.query","version":1,"target":"session","predicate":{"kind":"telepathy"}}""";

        Assert.Throws<JsonException>(() => QueryJson.Deserialize(json));
    }
}
