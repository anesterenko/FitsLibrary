using FitsLibrary.DocumentParts;

namespace FitsLibrary
{
    /// <summary>
    /// Represents a .fits document for read and write access
    /// </summary>
    public class FitsDocument
    {
        /// <summary>
        /// A list of headers in this document
        /// </summary>
        public Header Header { get; }

        /// <summary>
        /// Creates a new .fits document with a the given data
        /// </summary>
        /// <param name="header">The main header</param>
        public FitsDocument(Header header)
        {
            Header = header;
        }
    }
}
