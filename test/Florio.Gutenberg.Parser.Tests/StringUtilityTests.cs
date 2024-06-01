namespace Florio.Gutenberg.Parser.Tests
{
    public class StringUtilityTests
    {
        [Theory]
        [InlineData("Apẻndi[o]", "Apẻndio")]
        [InlineData("Sẻttezz[ó]ni", "Sẻttezzóni")]
        [InlineData("[O]bbróbri[o]", "Obbróbrio")]
        [InlineData("Óbit[o]", "Óbito")]
        [InlineData("[Ó]ber[o]", "Óbero")]
        public void GetPrintableString_ReturnsExpected(string input, string expected)
        {
            string actual = StringUtilities.GetPrintableString(input);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("Apẻndi[o]", "apendio")]
        [InlineData("Sẻttezz[ó]ni", "settezzoni")]
        [InlineData("[O]bbróbri[o]", "obbrobrio")]
        [InlineData("Óbit[o]", "obito")]
        [InlineData("[Ó]ber[o]", "obero")]
        public void NormalizeString_ReturnsExpected(string input, string expected)
        {
            string actual = StringUtilities.GetPrintableNormalizedString(input);

            Assert.Equal(expected, actual);
        }
    }
}
