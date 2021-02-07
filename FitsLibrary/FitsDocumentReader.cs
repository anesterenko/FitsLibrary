using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FitsLibrary.Deserialization;
using FitsLibrary.DocumentParts;
using FitsLibrary.Validation;
using FitsLibrary.Validation.Header;

namespace FitsLibrary
{
    public class FitsDocumentReader : IFitsDocumentReader
    {
        private IReadOnlyList<IValidator<Header>> headerValidators;
        private IContentDeserializer contentDeserializer;
        private IHeaderDeserializer headerDeserializer;

        public FitsDocumentReader()
        {
            UseValidatorsForReading();
            UseDeserializersForReading();
        }

        private void UseDeserializersForReading()
        {
            headerDeserializer = new HeaderDeserializer();
            contentDeserializer = new ContentDeserializer();
        }

        /// <summary>
        /// Used for Unit Testing
        /// </summary>
        /// <param name="headerDeserializer"></param>
        /// <param name="headerValidators"></param>
        internal FitsDocumentReader(
                IHeaderDeserializer headerDeserializer,
                List<IValidator<Header>> headerValidators,
                IContentDeserializer contentDeserializer)
        {
            this.headerValidators = headerValidators;
            this.contentDeserializer = contentDeserializer;
            this.headerDeserializer = headerDeserializer;
        }

        private void UseValidatorsForReading()
        {
            headerValidators = new List<IValidator<Header>>
            {
                new KeywordsMustBeUniqueValidator(),
                new MandatoryHeaderEntriesValidator(),
            };
        }

        public async Task<FitsDocument> ReadAsync(Stream inputStream)
        {
            var header = await headerDeserializer
                .DeserializeAsync(inputStream)
                .ConfigureAwait(false);

            var validatorTasks = new List<Task<ValidationResult>>();
            foreach (var headerValidator in headerValidators)
            {
                validatorTasks.Add(headerValidator.ValidateAsync(header));
            }

            var validationResults = await Task.WhenAll(validatorTasks).ConfigureAwait(continueOnCapturedContext: false);

            foreach (var validationResult in validationResults)
            {
                if (!validationResult.ValidationSuccessful)
                {
                    throw new InvalidDataException($"Validation failed for the header of the fits file: {validationResult.ValidationFailureMessage}");
                }
            }

            var content = await contentDeserializer
                .DeserializeAsync(inputStream, header)
                .ConfigureAwait(false);

            return new FitsDocument(
                header: header,
                content: content);
        }

        public Task<FitsDocument> ReadAsync(string filePath)
        {
            return ReadAsync(File.OpenRead(filePath));
        }
    }
}
