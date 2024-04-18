using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;
using FitsLibrary.Deserialization.Head;
using FitsLibrary.DocumentParts.ImageData;

namespace FitsLibrary;

/// <summary>
/// Contains several helper methods to deal with .fits files, like reading
/// the primary header of the file without reading the whole file
/// </summary>
public static class FitsDocumentHelper
{
    public static Task<ImageHeader> ReadHeaderAsync(string filePath)
    {
        return ReadHeaderAsync(File.OpenRead(filePath));
    }

    public static async Task<ImageHeader> ReadHeaderAsync(Stream inputStream)
    {
        var pipeReader = PipeReader.Create(
                inputStream,
                new StreamPipeReaderOptions(
                    bufferSize: FitsDocumentReader.ChunkSize,
                    minimumReadSize: FitsDocumentReader.ChunkSize))!;

        var headerDeserializer = new HeaderDeserializer();

        var headerResult = await headerDeserializer
            .DeserializeAsync(pipeReader)
            .ConfigureAwait(false);

        return new ImageHeader(headerResult.header.Entries);
    }

    public static Task<DataContentType> GetDocumentContentType(string filePath)
    {
        return GetDocumentContentType(File.OpenRead(filePath));
    }

    public static async Task<DataContentType> GetDocumentContentType(Stream inputStream)
    {
        var header = await ReadHeaderAsync(inputStream);
        return header.DataContentType;
    }
}
