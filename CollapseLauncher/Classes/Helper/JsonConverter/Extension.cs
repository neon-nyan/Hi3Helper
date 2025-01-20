using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CollapseLauncher.Helper.Metadata;
// ReSharper disable ReplaceSliceWithRangeIndexer

namespace CollapseLauncher.Helper.JsonConverter
{
    internal static class Extension
    {
        private const int DataPoolLimit = 512 << 10;
        internal static string GetServeV3String(Utf8JsonReader jsonReader)
        {
            ReadOnlySpan<byte> data = jsonReader.ValueSpan;
            bool isBufferUsePool = data.Length <= DataPoolLimit;

            // Get the output data span and decode the Base64 data to raw data
            byte[] dataBytes = null;

            try
            {
                // Get the status if the string is probably Base64 encoded or not
                bool isValidB64Data = Base64.IsValid(data);
                if (!isValidB64Data) return jsonReader.ValueIsEscaped ?
                    ReturnUnescapedData(data) :
                    Encoding.UTF8.GetString(data);

                // Try to decode the Base64 data
                dataBytes = isBufferUsePool ? ArrayPool<byte>.Shared.Rent(data.Length) : new byte[data.Length];
                OperationStatus opStatus = Base64.DecodeFromUtf8(data, dataBytes, out _, out int dataB64DecodedWritten);
                if (opStatus != OperationStatus.Done)
                    throw new InvalidOperationException($"Data is not a valid Base64! (Status: {opStatus})");

                // Assign read-only span as per Base64 written length
                ReadOnlySpan<byte> dataBase64Decoded = dataBytes.AsSpan(0, dataB64DecodedWritten);

                // Check if the data is Base64 encoded and also a ServeV3 data. If no, then return as a raw string
                bool isValidServeV3Data = DataCooker.IsServeV3Data(dataBase64Decoded);
                if (!isValidServeV3Data) return jsonReader.ValueIsEscaped ?
                    ReturnUnescapedData(data) :
                    Encoding.UTF8.GetString(data);

                // Try to decode the ServeV3 data and return it
                return DecodeJsonServeV3Data(dataBase64Decoded, jsonReader.ValueIsEscaped);
            }
            finally
            {
                if (isBufferUsePool && dataBytes != null) ArrayPool<byte>.Shared.Return(dataBytes, true);
            }
        }

        private static string DecodeJsonServeV3Data(ReadOnlySpan<byte> dataBytes, bool isValueEscaped)
        {
            // Get the size and initialize the output buffer
            DataCooker.GetServeV3DataSize(dataBytes, out long compressedSize, out long decompressedSize);

            bool isBufferUsePool = decompressedSize <= DataPoolLimit;
            byte[] outBuffer = isBufferUsePool ?
                ArrayPool<byte>.Shared.Rent((int)decompressedSize) :
                new byte[decompressedSize];

            try
            {
                // Decode the ServeV3 data to the outBuffer
                DataCooker.ServeV3Data(dataBytes, outBuffer, (int)compressedSize, (int)decompressedSize, out int dataWritten);

                // Get the string out of outBuffer and return it
                return isValueEscaped ? ReturnUnescapedData(outBuffer.AsSpan(0, dataWritten)) : Encoding.UTF8.GetString(outBuffer.AsSpan(0, dataWritten));
            }
            finally
            {
                if (isBufferUsePool) ArrayPool<byte>.Shared.Return(outBuffer, true);
            }
        }

        internal static string ReturnUnescapedData(ReadOnlySpan<byte> data)
        {
            // Get the buffer array
            bool isUsePool = data.Length <= DataPoolLimit;
            byte[] bufferOut = isUsePool ? ArrayPool<byte>.Shared.Rent(data.Length) : new byte[data.Length];

            try
            {
                int indexOfFirstEscape = data.IndexOf(JsonConstants.BackSlash);
                if (indexOfFirstEscape == -1) return Encoding.UTF8.GetString(data);

                if (!TryUnescape(data, bufferOut, indexOfFirstEscape, out int written))
                {
                    throw new
                        InvalidOperationException("String data contains invalid character data and unescape is failed!");
                }

                string outString = Encoding.UTF8.GetString(bufferOut.AsSpan(0, written));
                return outString;

            }
            finally
            {
                if (isUsePool) ArrayPool<byte>.Shared.Return(bufferOut, true);
            }
        }

        internal static unsafe string StripTabsAndNewlinesUtf8(ReadOnlySpan<char> s)
        {
            if (s.Length == 0) return "";

            int len = s.Length;
            char* newChars = stackalloc char[len];
            char* currentChar = newChars;

            for (int i = 0; i < len; ++i)
            {
                char c = s[i];
                if (c == JsonConstants.CarriageReturn
                 || c == JsonConstants.LineFeed
                 || c == JsonConstants.Tab) continue;
                *currentChar++ = c;
            }
            return new string(newChars, 0, (int)(currentChar - newChars));
        }

        #region .NET Runtime reference
        // Docs:
        // https://github.com/dotnet/runtime/blob/907eff84ef204a2d71c10e7cd726b76951b051bd/src/libraries/System.Text.Json/src/System/Text/Json/Reader/JsonReaderHelper.Unescaping.cs#L429

        internal static class JsonConstants
        {
            public const byte OpenBrace = (byte)'{';
            public const byte CloseBrace = (byte)'}';
            public const byte OpenBracket = (byte)'[';
            public const byte CloseBracket = (byte)']';
            public const byte Space = (byte)' ';
            public const byte CarriageReturn = (byte)'\r';
            public const byte LineFeed = (byte)'\n';
            public const byte Tab = (byte)'\t';
            public const byte ListSeparator = (byte)',';
            public const byte KeyValueSeparator = (byte)':';
            public const byte Quote = (byte)'"';
            public const byte BackSlash = (byte)'\\';
            public const byte Slash = (byte)'/';
            public const byte BackSpace = (byte)'\b';
            public const byte FormFeed = (byte)'\f';
            public const byte Asterisk = (byte)'*';
            public const byte Colon = (byte)':';
            public const byte Period = (byte)'.';
            public const byte Plus = (byte)'+';
            public const byte Hyphen = (byte)'-';
            public const byte UtcOffsetToken = (byte)'Z';
            public const byte TimePrefix = (byte)'T';

            public const string NewLineLineFeed = "\n";
            public const string NewLineCarriageReturnLineFeed = "\r\n";

            // \u2028 and \u2029 are considered respectively line and paragraph separators
            // UTF-8 representation for them is E2, 80, A8/A9
            public const byte StartingByteOfNonStandardSeparator = 0xE2;

            public static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];
            public static ReadOnlySpan<byte> TrueValue => "true"u8;
            public static ReadOnlySpan<byte> FalseValue => "false"u8;
            public static ReadOnlySpan<byte> NullValue => "null"u8;

            public static ReadOnlySpan<byte> NaNValue => "NaN"u8;
            public static ReadOnlySpan<byte> PositiveInfinityValue => "Infinity"u8;
            public static ReadOnlySpan<byte> NegativeInfinityValue => "-Infinity"u8;

            // Used to search for the end of a number
            public static ReadOnlySpan<byte> Delimiters => ",}] \n\r\t/"u8;

            // Explicitly skipping ReverseSolidus since that is handled separately
            public static ReadOnlySpan<byte> EscapableChars => "\"nrt/ubf"u8;

            public const int RemoveFlagsBitMask = 0x7FFFFFFF;

            // In the worst case, an ASCII character represented as a single utf-8 byte could expand 6x when escaped.
            // For example: '+' becomes '\u0043'
            // Escaping surrogate pairs (represented by 3 or 4 utf-8 bytes) would expand to 12 bytes (which is still <= 6x).
            // The same factor applies to utf-16 characters.
            public const int MaxExpansionFactorWhileEscaping = 6;

            // In the worst case, a single UTF-16 character could be expanded to 3 UTF-8 bytes.
            // Only surrogate pairs expand to 4 UTF-8 bytes but that is a transformation of 2 UTF-16 characters going to 4 UTF-8 bytes (factor of 2).
            // All other UTF-16 characters can be represented by either 1 or 2 UTF-8 bytes.
            public const int MaxExpansionFactorWhileTranscoding = 3;

            // When transcoding from UTF8 -> UTF16, the byte count threshold where we rent from the array pool before performing a normal alloc.
            public const long ArrayPoolMaxSizeBeforeUsingNormalAlloc =
#if NET6_0_OR_GREATER
                1024 * 1024 * 1024; // ArrayPool limit increased in .NET 6
#else
            1024 * 1024;
#endif

            // The maximum number of characters allowed when writing raw UTF-16 JSON. This is the maximum length that we can guarantee can
            // be safely transcoded to UTF-8 and fit within an integer-length span, given the max expansion factor of a single character (3).
            public const int MaxUtf16RawValueLength = int.MaxValue / MaxExpansionFactorWhileTranscoding;

            public const int MaxEscapedTokenSize = 1_000_000_000;   // Max size for already escaped value.
            public const int MaxUnescapedTokenSize = MaxEscapedTokenSize / MaxExpansionFactorWhileEscaping;  // 166_666_666 bytes
            public const int MaxCharacterTokenSize = MaxEscapedTokenSize / MaxExpansionFactorWhileEscaping; // 166_666_666 characters

            public const int MaximumFormatBooleanLength = 5;
            public const int MaximumFormatInt64Length = 20;   // 19 + sign (i.e. -9223372036854775808)
            public const int MaximumFormatUInt32Length = 10;  // i.e. 4294967295
            public const int MaximumFormatUInt64Length = 20;  // i.e. 18446744073709551615
            public const int MaximumFormatDoubleLength = 128;  // default (i.e. 'G'), using 128 (rather than say 32) to be future-proof.
            public const int MaximumFormatSingleLength = 128;  // default (i.e. 'G'), using 128 (rather than say 32) to be future-proof.
            public const int MaximumFormatDecimalLength = 31; // default (i.e. 'G')
            public const int MaximumFormatGuidLength = 36;    // default (i.e. 'D'), 8 + 4 + 4 + 4 + 12 + 4 for the hyphens (e.g. 094ffa0a-0442-494d-b452-04003fa755cc)
            public const int MaximumEscapedGuidLength = MaxExpansionFactorWhileEscaping * MaximumFormatGuidLength;
            public const int MaximumFormatDateTimeLength = 27;    // StandardFormat 'O', e.g. 2017-06-12T05:30:45.7680000
            public const int MaximumFormatDateTimeOffsetLength = 33;  // StandardFormat 'O', e.g. 2017-06-12T05:30:45.7680000-07:00
            public const int MaxDateTimeUtcOffsetHours = 14; // The UTC offset portion of a TimeSpan or DateTime can be no more than 14 hours and no less than -14 hours.
            public const int DateTimeNumFractionDigits = 7;  // TimeSpan and DateTime formats allow exactly up to many digits for specifying the fraction after the seconds.
            public const int MaxDateTimeFraction = 9_999_999;  // The largest fraction expressible by TimeSpan and DateTime formats
            public const int DateTimeParseNumFractionDigits = 16; // The maximum number of fraction digits the Json DateTime parser allows
            public const int MaximumDateTimeOffsetParseLength = MaximumFormatDateTimeOffsetLength +
                (DateTimeParseNumFractionDigits - DateTimeNumFractionDigits); // Like StandardFormat 'O' for DateTimeOffset, but allowing 9 additional (up to 16) fraction digits.
            public const int MinimumDateTimeParseLength = 10; // YYYY-MM-DD
            public const int MaximumEscapedDateTimeOffsetParseLength = MaxExpansionFactorWhileEscaping * MaximumDateTimeOffsetParseLength;

            public const int MaximumLiteralLength = 5; // Must be able to fit null, true, & false.

            // Encoding Helpers
            public const char HighSurrogateStart = '\ud800';
            public const char HighSurrogateEnd = '\udbff';
            public const char LowSurrogateStart = '\udc00';
            public const char LowSurrogateEnd = '\udfff';

            public const int UnicodePlane01StartValue = 0x10000;
            public const int HighSurrogateStartValue = 0xD800;
            public const int HighSurrogateEndValue = 0xDBFF;
            public const int LowSurrogateStartValue = 0xDC00;
            public const int LowSurrogateEndValue = 0xDFFF;
            public const int BitShiftBy10 = 0x400;

            // The maximum number of parameters a constructor can have where it can be considered
            // for a path on deserialization where we don't box the constructor arguments.
            public const int UnboxedParameterCountThreshold = 4;

            // Two space characters is the default indentation.
            public const char DefaultIndentCharacter = ' ';
            public const char TabIndentCharacter = '\t';
            public const int DefaultIndentSize = 2;
            public const int MinimumIndentSize = 0;
            public const int MaximumIndentSize = 127; // If this value is changed, the impact on the options masking used in the JsonWriterOptions struct must be checked carefully.

        }

        /// <summary>
        /// Used when writing to buffers not guaranteed to fit the unescaped result.
        /// </summary>
        [DebuggerNonUserCode]
        private static bool TryUnescape(ReadOnlySpan<byte> source, Span<byte> destination, int idx, out int written)
        {
            Debug.Assert(idx >= 0 && idx < source.Length);
            Debug.Assert(source[idx] == JsonConstants.BackSlash);

            if (!source.Slice(0, idx).TryCopyTo(destination))
            {
                written = 0;
                goto DestinationTooShort;
            }

            written = idx;

            while (true)
            {
                Debug.Assert(source[idx] == JsonConstants.BackSlash);

                if (written == destination.Length)
                {
                    goto DestinationTooShort;
                }

                switch (source[++idx])
                {
                    case JsonConstants.Quote:
                        destination[written++] = JsonConstants.Quote;
                        break;
                    case (byte)'n':
                        destination[written++] = JsonConstants.LineFeed;
                        break;
                    case (byte)'r':
                        destination[written++] = JsonConstants.CarriageReturn;
                        break;
                    case JsonConstants.BackSlash:
                        destination[written++] = JsonConstants.BackSlash;
                        break;
                    case JsonConstants.Slash:
                        destination[written++] = JsonConstants.Slash;
                        break;
                    case (byte)'t':
                        destination[written++] = JsonConstants.Tab;
                        break;
                    case (byte)'b':
                        destination[written++] = JsonConstants.BackSpace;
                        break;
                    case (byte)'f':
                        destination[written++] = JsonConstants.FormFeed;
                        break;
                    default:
                        Debug.Assert(source[idx] == 'u', "invalid escape sequences must have already been caught by Utf8JsonReader.Read()");

                        // The source is known to be valid JSON, and hence if we see a \u, it is guaranteed to have 4 hex digits following it
                        // Otherwise, the Utf8JsonReader would have already thrown an exception.
                        Debug.Assert(source.Length >= idx + 5);

                        bool result = Utf8Parser.TryParse(source.Slice(idx + 1, 4), out int scalar, out int bytesConsumed, 'x');
                        Debug.Assert(result);
                        Debug.Assert(bytesConsumed == 4);
                        idx += 4;

                        if (IsInRangeInclusive((uint)scalar, JsonConstants.HighSurrogateStartValue, JsonConstants.LowSurrogateEndValue))
                        {
                            // The first hex value cannot be a low surrogate.
                            if (scalar >= JsonConstants.LowSurrogateStartValue)
                            {
                                throw new InvalidOperationException($"Cannot read invalid UTF-16 JSON text as string. Invalid low scalar value: '0x{scalar:X2}'");
                            }

                            Debug.Assert(IsInRangeInclusive((uint)scalar, JsonConstants.HighSurrogateStartValue, JsonConstants.HighSurrogateEndValue));

                            // We must have a low surrogate following a high surrogate.
                            if (source.Length < idx + 7 || source[idx + 1] != '\\' || source[idx + 2] != 'u')
                            {
                                throw new InvalidOperationException("Cannot read incomplete UTF-16 JSON text as string with missing low surrogate.");
                            }

                            // The source is known to be valid JSON, and hence if we see a \u, it is guaranteed to have 4 hex digits following it
                            // Otherwise, the Utf8JsonReader would have already thrown an exception.
                            result = Utf8Parser.TryParse(source.Slice(idx + 3, 4), out int lowSurrogate, out bytesConsumed, 'x');
                            Debug.Assert(result);
                            Debug.Assert(bytesConsumed == 4);
                            idx += 6;

                            // If the first hex value is a high surrogate, the next one must be a low surrogate.
                            if (!IsInRangeInclusive((uint)lowSurrogate, JsonConstants.LowSurrogateStartValue, JsonConstants.LowSurrogateEndValue))
                            {
                                throw new InvalidOperationException($"Cannot read invalid UTF-16 JSON text as string. Invalid surrogate value: '0x{lowSurrogate:X2}'");
                            }

                            // To find the Unicode scalar:
                            // (0x400 * (High surrogate - 0xD800)) + Low surrogate - 0xDC00 + 0x10000
                            scalar = JsonConstants.BitShiftBy10 * (scalar - JsonConstants.HighSurrogateStartValue)
                                + (lowSurrogate - JsonConstants.LowSurrogateStartValue)
                                + JsonConstants.UnicodePlane01StartValue;
                        }

                        var rune = new Rune(scalar);
                        bool success = rune.TryEncodeToUtf8(destination.Slice(written), out int bytesWritten);
                        if (!success)
                        {
                            goto DestinationTooShort;
                        }

                        Debug.Assert(bytesWritten <= 4);
                        written += bytesWritten;
                        break;
                }

                if (++idx == source.Length)
                {
                    goto Success;
                }

                if (source[idx] != JsonConstants.BackSlash)
                {
                    ReadOnlySpan<byte> remaining = source.Slice(idx);
                    int nextUnescapedSegmentLength = remaining.IndexOf(JsonConstants.BackSlash);
                    if (nextUnescapedSegmentLength < 0)
                    {
                        nextUnescapedSegmentLength = remaining.Length;
                    }

                    if ((uint)(written + nextUnescapedSegmentLength) >= (uint)destination.Length)
                    {
                        goto DestinationTooShort;
                    }

                    Debug.Assert(nextUnescapedSegmentLength > 0);
                    switch (nextUnescapedSegmentLength)
                    {
                        case 1:
                            destination[written++] = source[idx++];
                            break;
                        case 2:
                            destination[written++] = source[idx++];
                            destination[written++] = source[idx++];
                            break;
                        case 3:
                            destination[written++] = source[idx++];
                            destination[written++] = source[idx++];
                            destination[written++] = source[idx++];
                            break;
                        default:
                            remaining.Slice(0, nextUnescapedSegmentLength).CopyTo(destination.Slice(written));
                            written += nextUnescapedSegmentLength;
                            idx += nextUnescapedSegmentLength;
                            break;
                    }

                    Debug.Assert(idx == source.Length || source[idx] == JsonConstants.BackSlash);

                    if (idx == source.Length)
                    {
                        goto Success;
                    }
                }
            }

        Success:
            return true;

        DestinationTooShort:
            return false;
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="value"/> is between
        /// <paramref name="lowerBound"/> and <paramref name="upperBound"/>, inclusive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInRangeInclusive(uint value, uint lowerBound, uint upperBound)
            => value - lowerBound <= upperBound - lowerBound;
        #endregion
    }
}
