using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace LibTmux.Mcp;

/// <summary>Checks a JSON value against a UTF-8 byte ceiling without retaining it.</summary>
internal static class Utf8JsonBudget
{
    private static readonly ConditionalWeakTable<JsonSerializerOptions, EnvelopeSize>
        StructuredEnvelopeSizes = new();
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonEncodingProfile>
        JsonEncodingProfiles = new();

    // MCP 2.2 appends server identity and may wrap a result in task metadata
    // after application filters run, so every result keeps room for both.
    internal const int ProtocolMetadataReserve = 512;

    /// <summary>Answers whether the serialized value fits the ceiling.</summary>
    internal static bool Fits<T>(T value, int maxBytes, JsonSerializerOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            using var sink = new LimitedWriteStream(maxBytes);
            JsonSerializer.Serialize(sink, value, options);
            return true;
        }
        catch (BudgetExceededException)
        {
            return false;
        }
    }

    /// <summary>Counts the serialized UTF-8 bytes without retaining them.</summary>
    internal static int GetByteCount<T>(T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var sink = new LimitedWriteStream(int.MaxValue);
        JsonSerializer.Serialize(sink, value, options);
        return checked((int)sink.Length);
    }

    /// <summary>Budgets the complete MCP result produced for a structured return value.</summary>
    internal static int GetStructuredToolResultByteCount<T>(
        T value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var sink = new JsonFragmentCountingStream(options);
        JsonSerializer.Serialize(sink, value, options);
        return checked(
            GetStructuredEnvelopeSize(options)
            + sink.RawLength
            + sink.EmbeddedStringContentLength
            + ProtocolMetadataReserve);
    }

    /// <summary>Answers whether a result fits after protocol metadata is appended.</summary>
    internal static bool FitsToolResult(
        CallToolResult value,
        int maxBytes,
        JsonSerializerOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            maxBytes,
            ProtocolMetadataReserve);
        return Fits(value, maxBytes - ProtocolMetadataReserve, options);
    }

    /// <summary>Counts one JSON fragment in both copies of a structured tool result.</summary>
    internal static int GetStructuredJsonFragmentByteCount<T>(
        T value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var sink = new JsonFragmentCountingStream(options);
        JsonSerializer.Serialize(sink, value, options);
        return checked(sink.RawLength + sink.EmbeddedStringContentLength);
    }

    /// <summary>Counts a bounded-text fragment without materializing large JSON strings.</summary>
    internal static int GetStructuredJsonFragmentByteCount(
        BoundedText value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        var skeleton = new BoundedText(
            [],
            value.Truncated,
            value.DroppedLines,
            value.DroppedBytes);
        int bytes = GetStructuredJsonFragmentByteCount<BoundedText>(skeleton, options);
        if (value.Lines.Count == 0)
        {
            return bytes;
        }

        int embeddedQuoteBytes = MeasureJsonString("\"", options).RawContentLength;
        for (int index = 0; index < value.Lines.Count; index++)
        {
            JsonStringMetrics line = MeasureJsonString(value.Lines[index], options);
            bytes = checked(
                bytes
                + 2
                + line.RawContentLength
                + (2 * embeddedQuoteBytes)
                + line.EmbeddedContentLength
                + (index > 0 ? 2 : 0));
        }

        return bytes;
    }

    /// <summary>Counts a matched-line fragment without materializing its text.</summary>
    internal static int GetStructuredJsonFragmentByteCount(
        MatchedLine value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        int skeleton = GetStructuredJsonFragmentByteCount<MatchedLine>(
            new MatchedLine(value.Row, string.Empty),
            options);
        JsonStringMetrics text = MeasureJsonString(value.Text, options);
        return checked(skeleton + text.RawContentLength + text.EmbeddedContentLength);
    }

    /// <summary>Counts one string's content in the raw and embedded JSON copies.</summary>
    internal static int GetStructuredJsonStringContentByteCount(
        string value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        JsonStringMetrics metrics = MeasureJsonString(value, options);
        return checked(metrics.RawContentLength + metrics.EmbeddedContentLength);
    }

    private static int GetStructuredEnvelopeSize(JsonSerializerOptions options) =>
        StructuredEnvelopeSizes.GetValue(
            options,
            static current => new EnvelopeSize(MeasureStructuredEnvelope(current))).Value;

    private static int MeasureStructuredEnvelope(JsonSerializerOptions options)
    {
        JsonElement structured = JsonSerializer.SerializeToElement(new { }, options);
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "{}" }],
            StructuredContent = structured,
        };

        return checked(GetByteCount(result, options) - 4);
    }

    private static JsonStringMetrics MeasureJsonString(
        string value,
        JsonSerializerOptions options)
    {
        JsonEncodingProfile profile = JsonEncodingProfiles.GetValue(
            options,
            static current => new JsonEncodingProfile(
                current.Encoder ?? JavaScriptEncoder.Default));
        int rawLength = 0;
        int embeddedLength = 0;
        ReadOnlySpan<char> remaining = value;
        while (!remaining.IsEmpty)
        {
            JsonStringMetrics rune;
            int consumed;
            if (remaining[0] <= 0x7f)
            {
                rune = profile.Ascii[remaining[0]];
                consumed = 1;
            }
            else
            {
                OperationStatus status = Rune.DecodeFromUtf16(
                    remaining,
                    out Rune decoded,
                    out consumed);
                if (status != OperationStatus.Done)
                {
                    decoded = Rune.ReplacementChar;
                    consumed = 1;
                }

                rune = EncodeRune(decoded, profile.Encoder);
            }

            rawLength = checked(rawLength + rune.RawContentLength);
            embeddedLength = checked(embeddedLength + rune.EmbeddedContentLength);
            remaining = remaining[consumed..];
        }

        return new JsonStringMetrics(rawLength, embeddedLength);
    }

    private static JsonStringMetrics EncodeRune(Rune rune, JavaScriptEncoder encoder)
    {
        Span<byte> source = stackalloc byte[4];
        Span<byte> encoded = stackalloc byte[64];
        Span<byte> embedded = stackalloc byte[384];
        int sourceLength = rune.EncodeToUtf8(source);
        OperationStatus first = encoder.EncodeUtf8(
            source[..sourceLength],
            encoded,
            out int firstConsumed,
            out int firstWritten,
            isFinalBlock: true);
        OperationStatus second = encoder.EncodeUtf8(
            encoded[..firstWritten],
            embedded,
            out int secondConsumed,
            out int secondWritten,
            isFinalBlock: true);
        if (first != OperationStatus.Done
            || firstConsumed != sourceLength
            || second != OperationStatus.Done
            || secondConsumed != firstWritten)
        {
            throw new InvalidOperationException("The JSON encoder exceeded its rune bound.");
        }

        return new JsonStringMetrics(firstWritten, secondWritten);
    }

    private sealed class EnvelopeSize(int value)
    {
        internal int Value { get; } = value;
    }

    private sealed class JsonEncodingProfile
    {
        internal JsonEncodingProfile(JavaScriptEncoder encoder)
        {
            Encoder = encoder;
            Ascii = new JsonStringMetrics[128];
            for (int value = 0; value < Ascii.Length; value++)
            {
                Ascii[value] = EncodeRune(new Rune(value), encoder);
            }
        }

        internal JsonStringMetrics[] Ascii { get; }

        internal JavaScriptEncoder Encoder { get; }
    }

    private sealed class LimitedWriteStream(int maxBytes) : Stream
    {
        private int _bytesWritten;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _bytesWritten;

        public override long Position
        {
            get => _bytesWritten;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, buffer.Length - count);
            Count(count);
        }

        public override void Write(ReadOnlySpan<byte> buffer) => Count(buffer.Length);

        private void Count(int count)
        {
            if (count > maxBytes - _bytesWritten)
            {
                throw new BudgetExceededException();
            }

            _bytesWritten += count;
        }
    }

    private sealed class JsonFragmentCountingStream : Stream
    {
        private readonly int _backslashExpansion;
        private readonly int _quoteExpansion;
        private int _embeddedExpansion;
        private int _rawLength;

        internal JsonFragmentCountingStream(JsonSerializerOptions options)
        {
            _quoteExpansion = MeasureJsonString("\"", options).RawContentLength - 1;
            _backslashExpansion = MeasureJsonString("\\", options).RawContentLength - 1;
        }

        internal int RawLength => _rawLength;

        internal int EmbeddedStringContentLength => checked(_rawLength + _embeddedExpansion);

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _rawLength;

        public override long Position
        {
            get => _rawLength;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, buffer.Length - count);
            Count(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer) => Count(buffer);

        private void Count(ReadOnlySpan<byte> buffer)
        {
            _rawLength = checked(_rawLength + buffer.Length);
            foreach (byte value in buffer)
            {
                if (value is (byte)'"' or (byte)'\\')
                {
                    _embeddedExpansion = checked(
                        _embeddedExpansion
                        + (value == (byte)'"' ? _quoteExpansion : _backslashExpansion));
                }
            }
        }
    }

    private readonly record struct JsonStringMetrics(
        int RawContentLength,
        int EmbeddedContentLength);

    private sealed class BudgetExceededException : Exception;
}
