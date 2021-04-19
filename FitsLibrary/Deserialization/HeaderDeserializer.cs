using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FitsLibrary.DocumentParts;
using FitsLibrary.DocumentParts.Objects;
using FitsLibrary.Extensions;

namespace FitsLibrary.Deserialization
{
    public class HeaderDeserializer : IHeaderDeserializer
    {
        /// <summary>
        /// Length of a header entry chunk, containing a single header entry
        /// </summary>
        public const int HeaderEntryChunkSize = 80;
        public const int HeaderBlockSize = 2880;
        public const int LogicalValuePosition = 20;
        public const char ContinuedStringMarker = '&';
        private const string ContinueKeyWord = "CONTINUE";

        /// <summary>
        /// Representation of the Headers END marker
        /// "END" + 77 spaces in ASCII
        /// </summary>
        public static readonly byte[] END_MARKER =
            new List<byte> { 0x45, 0x4e, 0x44 }
                .Concat(Enumerable.Repeat(element: (byte)0x20, count: 77))
                .ToArray();

        /// <summary>
        /// Deserializes the header part of the fits document
        /// </summary>
        /// <param name="dataStream">the stream from which to read the data from (should be at position 0)</param>
        /// <exception cref="InvalidDataException"></exception>
        public Task<Header> DeserializeAsync(PipeReader dataStream)
        {
            return Task.Run(() =>
            {
                PreValidateStream(dataStream);

                var endOfHeaderReached = false;
                var headerEntries = new List<HeaderEntry>();

                while (!endOfHeaderReached)
                {
                    var result = dataStream.ReadAsync().GetAwaiter().GetResult();
                    var headerBlock = result.Buffer;

                    if (result.IsCompleted)
                    {
                        dataStream.Complete();
                        throw new InvalidDataException("No END marker found for the fits header, fits file might be corrupted");
                    }

                    headerEntries.AddRange(ParseHeaderBlock(headerBlock, out endOfHeaderReached));
                    dataStream.AdvanceTo(result.Buffer.GetPosition(HeaderBlockSize), result.Buffer.End);
                }

                return new Header(headerEntries);
            });
        }

        private static List<HeaderEntry> ParseHeaderBlock(ReadOnlySequence<byte> headerBlock, out bool endOfHeaderReached)
        {
            endOfHeaderReached = false;
            var currentIndex = 0;
            Span<byte> headerBlockSpan = stackalloc byte[Convert.ToInt32(headerBlock.Length)];
            headerBlock.CopyTo(headerBlockSpan);
            var headerEntries = new List<HeaderEntry>();
            var isContinued = false;

            while (currentIndex < HeaderBlockSize)
            {
                var headerEntryChunk = headerBlockSpan.Slice(currentIndex, HeaderEntryChunkSize);
                currentIndex += HeaderEntryChunkSize;

                if (headerEntryChunk.SequenceEqual(END_MARKER))
                {
                    endOfHeaderReached = true;
                    break;
                }

                var parsedHeaderEntry = ParseHeaderEntryChunk(headerEntryChunk);
                if (!isContinued)
                {
                    if (ValueIsStringAndHasContinueMarker(parsedHeaderEntry.Value))
                    {
                        isContinued = true;
                        parsedHeaderEntry.Value = (parsedHeaderEntry.Value as string)!.Trim()[..^1];
                    }

                    headerEntries.Add(parsedHeaderEntry);
                }
                else
                {
                    if (!string.Equals(parsedHeaderEntry.Key, ContinueKeyWord, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("Unfinished continued value found");
                    }
                    var valueToAppend = parsedHeaderEntry.Value as string;
                    if (ValueIsStringAndHasContinueMarker(parsedHeaderEntry.Value))
                    {
                        valueToAppend = valueToAppend!.Trim()[..^1];
                        isContinued = true;
                    }
                    else
                    {
                        isContinued = false;
                    }
                    headerEntries[^1].Value = $"{headerEntries[^1].Value as string}{valueToAppend}";
                    if (parsedHeaderEntry.Comment != null)
                    {
                        headerEntries[^1].Comment += $" {parsedHeaderEntry.Comment}";
                    }
                }

            }

            return headerEntries;
        }

        private static bool ValueIsStringAndHasContinueMarker(object? value)
        {
            return value is string parsedString && parsedString.Trim().EndsWith(ContinuedStringMarker);
        }

        private static HeaderEntry ParseHeaderEntryChunk(ReadOnlySpan<byte> headerEntryChunk)
        {
            var key = Encoding.ASCII.GetString(headerEntryChunk[0..8]).Trim();
            if (HeaderEntryChunkHasValueMarker(headerEntryChunk)
                    || HeaderEntryEntryChunkHasContinueMarker(headerEntryChunk))
            {
                var value = Encoding.ASCII.GetString(headerEntryChunk[10..]).Trim();
                if (value.Contains('/', StringComparison.Ordinal))
                {
                    var comment = value[(value.IndexOf('/', StringComparison.Ordinal) + 1)..].Trim().Trim('\0');
                    value = value[0..value.IndexOf('/', StringComparison.Ordinal)].Trim();
                    var parsedValue = ParseValue(value);
                    return new HeaderEntry(key, parsedValue, comment);
                }
                else
                {
                    var parsedValue = ParseValue(value);
                    return new HeaderEntry(
                        key: key,
                        value: parsedValue,
                        comment: null);
                }
            }

            return new HeaderEntry(
                key: key,
                value: null,
                comment: null);
        }

        private static bool HeaderEntryEntryChunkHasContinueMarker(ReadOnlySpan<byte> headerEntryChunk)
        {
            return string.Equals(Encoding.ASCII.GetString(headerEntryChunk[..8]), ContinueKeyWord, StringComparison.Ordinal);
        }

        private static object? ParseValue(string value)
        {
            value = value.Trim('\0');
            if (string.IsNullOrEmpty(value.Trim()))
            {
                return null;
            }

            if (value.Trim().StartsWith('\''))
            {
                return value.Trim()[1..^1];
            }

            if (value.Contains(".", StringComparison.Ordinal))
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            if (string.Equals(value, "T", StringComparison.Ordinal) || string.Equals(value, "F", StringComparison.Ordinal))
            {
                return string.Equals(value, "T", StringComparison.Ordinal);
            }

            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static bool HeaderEntryChunkHasValueMarker(ReadOnlySpan<byte> headerEntryChunk)
        {
            return headerEntryChunk[8] == 0x3D && headerEntryChunk[9] == 0x20;
        }

        private static void PreValidateStream(PipeReader dataStream)
        {
            if (dataStream == null)
                throw new ArgumentNullException(nameof(dataStream), "The Stream from which to read from can not be NULL");
        }
    }
}
