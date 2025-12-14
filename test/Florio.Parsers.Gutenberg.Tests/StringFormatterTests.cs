using Florio.Data;

namespace Florio.Parsers.Gutenberg.Tests;

public class StringFormatterTests
{
    [Theory]
    [InlineData("Abachísta", "Abachísta")]
    [InlineData("Apẻndi[o]", "Apẻndio")]
    [InlineData("Ẻ´ccene", "Ẻ´ccene")]
    [InlineData("Sẻttezz[ó]ni", "Sẻttezzóni")]
    [InlineData("[O]bbróbri[o]", "Obbróbrio")]
    [InlineData("Óbit[o]", "Óbito")]
    [InlineData("[Ó]ber[o]", "Óbero")]
    public void GetPrintableString_ReturnsExpected(string input, string expected)
    {
        IStringFormatter stringFormatter = new StringFormatter();
        string actual = stringFormatter.ToPrintableString(input);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Abachísta", "abachista")]
    [InlineData("Apẻndi[o]", "apendio")]
    [InlineData("Ẻ´ccene", "eccene")]
    [InlineData("Sẻttezz[ó]ni", "settezzoni")]
    [InlineData("[O]bbróbri[o]", "obbrobrio")]
    [InlineData("Óbit[o]", "obito")]
    [InlineData("[Ó]ber[o]", "obero")]
    public void PrintableNormalizeString_ReturnsExpected(string input, string expected)
    {
        IStringFormatter stringFormatter = new StringFormatter();
        string actual = stringFormatter.ToPrintableNormalizedString(input);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Abachísta", "abachista")]
    [InlineData("Apẻndi[o]", "apendio")]
    [InlineData("Ẻ´ccene", "eccene")]
    [InlineData("Sẻttezz[ó]ni", "settezzoni")]
    [InlineData("[O]bbróbri[o]", "obbrobrio")]
    [InlineData("Óbit[o]", "obito")]
    [InlineData("[Ó]ber[o]", "obero")]
    public void NormalizeStringForVector_ReturnsExpected(string input, string expected)
    {
        IStringFormatter stringFormatter = new StringFormatter();
        string actual = stringFormatter.NormalizeForVector(input);

        Assert.Equal(expected, actual);
    }
}
