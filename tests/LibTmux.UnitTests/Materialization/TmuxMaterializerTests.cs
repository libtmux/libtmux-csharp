using System.Text;
using LibTmux.Internal;

namespace LibTmux.UnitTests.Materialization;

public sealed class TmuxMaterializerTests
{
    private static readonly TmuxVersion Version = TmuxVersion.Parse("3.7b");

    [Fact]
    public void Unmaterialized_server_has_no_generation_to_own_rows()
    {
        var context = new MaterializationContext(Server.Open(), Version);

        Assert.Throws<InvalidOperationException>(() => context.Generation);
    }

    [Fact]
    public void Context_requires_a_server_and_a_valid_version()
    {
        Assert.Throws<ArgumentNullException>(
            () => new MaterializationContext(null!, Version));
        Assert.Throws<ArgumentException>(
            () => new MaterializationContext(Server.Open(), default));
    }

    [Fact]
    public void Server_projection_names_its_child_enumeration()
    {
        ServerProjectionDescriptor descriptor = ServerProjection.Descriptor;

        Assert.Equal("list-sessions", descriptor.ListCommand);
        Assert.Equal("session_id", descriptor.ChildIdAttribute);
        Assert.Equal("server_", descriptor.FormatterPrefix);
    }

    [Fact]
    public void Window_edge_stays_uncaptured_until_a_snapshot_orders_it()
    {
        var edge = new SessionWindowEdge
        {
            SessionId = SessionId.Parse("$1"),
            WindowId = WindowId.Parse("@2"),
            WindowIndex = 3,
        };

        Assert.Null(edge.Ordinal);
        Assert.Equal(7, (edge with { Ordinal = 7 }).Ordinal);
        Assert.Null(edge.Ordinal);
    }

    [Fact]
    public void State_replacement_leaves_the_materialized_row_intact()
    {
        var state = new EntityMaterializationState
        {
            RawFields = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["session_id"] = "$1",
            },
            Server = Server.Open(),
            Generation = new ServerGeneration(11, 22),
            SessionId = SessionId.Parse("$1"),
        };

        EntityMaterializationState linked = state with
        {
            WindowEdge = new SessionWindowEdge
            {
                SessionId = SessionId.Parse("$1"),
                WindowId = WindowId.Parse("@4"),
                WindowIndex = 0,
                Ordinal = 0,
            },
        };

        Assert.Null(state.WindowEdge);
        Assert.NotNull(linked.WindowEdge);
        Assert.Same(state.RawFields, linked.RawFields);
        Assert.Equal(state.Generation, linked.Generation);
    }

    [Fact]
    public void Decoding_projects_invalid_utf8_as_lowercase_hex_escapes()
    {
        string projected = Utf8BackslashDecoder.ProjectValue([0x61, 0xFF, 0x62]);

        Assert.Equal("a\\xffb", projected);
    }

    [Fact]
    public void Decoding_a_value_keeps_embedded_newlines()
    {
        string projected = Utf8BackslashDecoder.ProjectValue(
            Encoding.UTF8.GetBytes("first\nsecond\r\n"));

        Assert.Equal("first\nsecond\r\n", projected);
    }

    [Fact]
    public void Framed_field_limit_never_exceeds_the_capture_ceiling()
    {
        var bounded = new TmuxTransportLimits(MaxCapturedBytesPerStream: 4);

        Assert.Equal(4, bounded.MaxFramedFieldBytes);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TmuxTransportLimits(
                MaxCapturedBytesPerStream: 4,
                MaxFramedFieldBytesValue: 5));
    }
}
