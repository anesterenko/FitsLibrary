using System.Threading.Tasks;
using FitsLibrary.DocumentParts.ImageData;
using FitsLibrary.Validation.Header;
using FluentAssertions;
using NUnit.Framework;

namespace FitsLibrary.Tests.Validation.Header;

public class MandatoryHeaderEntriesValidatorTests
{
    [Test]
    public async Task Validate_WithNoHeaderEntries_ValidationUnsuccessfulAsync()
    {
        // Arrange
        var testee = new MandatoryHeaderEntriesValidator(["SomeMandatoryField"]);
        var header = new HeaderBuilder()
            .WithEmptyHeader()
            .Build();

        // Act
        var result = await testee.ValidateAsync(header);

        // Assert
        result.ValidationSuccessful.Should().Be(false);
        result.ValidationFailureMessage.Should().Be("The FITS header is missing required fields (or they are in the wrong location).");
    }

    [Test]
    public async Task Validate_WithAllMandatoryKeywordsWith0Axis_ValidationSuccessfulAsync()
    {
        // Arrange
        var testee = new MandatoryHeaderEntriesValidator(["SIMPLE", "BITPIX", "NAXIS"]);
        var header = new HeaderBuilder()
            .WithValidFitsFormat()
            .WithContentDataType(DataContentType.INT16)
            .WithNumberOfAxis(0)
            .WithEndEntry()
            .Build();

        // Act
        var result = await testee.ValidateAsync(header);

        // Assert
        result.ValidationSuccessful.Should().Be(true);
        result.ValidationFailureMessage.Should().BeNull();
    }

    [Test]
    public async Task Validate_WithAllMandatoryKeywordsWith3Axis_ValidationSuccessfulAsync()
    {
        // Arrange
        var testee = new MandatoryHeaderEntriesValidator(["SIMPLE", "BITPIX", "NAXIS"]);
        var header = new HeaderBuilder()
            .WithValidFitsFormat()
            .WithContentDataType(DataContentType.INT16)
            .WithNumberOfAxis(3)
            .WithAxisOfSize(1, 1000)
            .WithAxisOfSize(2, 1000)
            .WithAxisOfSize(3, 1000)
            .WithEndEntry()
            .Build();

        // Act
        var result = await testee.ValidateAsync(header);

        // Assert
        result.ValidationSuccessful.Should().Be(true);
        result.ValidationFailureMessage.Should().BeNull();
    }

    [Test]
    public async Task Validate_WithAllMandatoryKeywordsWith3AxisButWronglyDefinedSizes_ValidationFailsAsync()
    {
        // Arrange
        var testee = new MandatoryHeaderEntriesValidator(["SIMPLE", "BITPIX", "NAXIS"]);
        var header = new HeaderBuilder()
            .WithValidFitsFormat()
            .WithContentDataType(DataContentType.INT16)
            .WithNumberOfAxis(3)
            .WithAxisOfSize(1, 1000)
            .WithAxisOfSize(2, 1000)
            .WithAxisOfSize(4, 1000)
            .WithEndEntry()
            .Build();

        // Act
        var result = await testee.ValidateAsync(header);

        // Assert
        result.ValidationSuccessful.Should().Be(false);
        result.ValidationFailureMessage.Should().Be("The FITS header is missing required fields (or they are in the wrong location).");
    }

    [Test]
    public async Task Validate_WithAllMandatoryKeywordsWith3AxisButNotAllNAXIS_ValidationFailsAsync()
    {
        // Arrange
        var testee = new MandatoryHeaderEntriesValidator(["SIMPLE", "BITPIX", "NAXIS"]);
        var header = new HeaderBuilder()
            .WithValidFitsFormat()
            .WithContentDataType(DataContentType.INT16)
            .WithNumberOfAxis(3)
            .WithAxisOfSize(1, 1000)
            .WithAxisOfSize(2, 1000)
            .WithEndEntry()
            .Build();

        // Act
        var result = await testee.ValidateAsync(header);

        // Assert
        result.ValidationSuccessful.Should().Be(false);
        result.ValidationFailureMessage.Should().Be("The FITS header is missing required fields (or they are in the wrong location).");
    }

    [Test]
    public async Task Validate_WithAllMandatoryKeywordsButNAXISIsNotTypeInt_ValidationFailsAsync()
    {
        // Arrange
        var testee = new MandatoryHeaderEntriesValidator(["SIMPLE", "BITPIX", "NAXIS"]);
        var header = new HeaderBuilder()
            .WithValidFitsFormat()
            .WithContentDataType(DataContentType.INT16)
            .WithNumberOfAxis("test")
            .WithEndEntry()
            .Build();

        // Act
        var result = await testee.ValidateAsync(header);

        // Assert
        result.ValidationSuccessful.Should().Be(false);
        result.ValidationFailureMessage.Should().Be("The FITS header contains the field 'NAXIS' but it is not of type integer");
    }
}
