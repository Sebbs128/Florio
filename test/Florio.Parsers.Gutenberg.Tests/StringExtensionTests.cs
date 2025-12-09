using Florio.Parsers.Gutenberg.Extensions;

namespace Florio.Parsers.Gutenberg.Tests;

public class StringExtensionTests
{
    [Theory]
    [InlineData("affielíre", "Affielíre")]
    [InlineData("affielísc[o]", "Affielísc[o]")]
    [InlineData("affielít[o]", "Affielít[o]")]
    [InlineData("audíre", "Audíre")]
    [InlineData("ód[o]", "Ód[o]")]
    [InlineData("udíj", "Vdíj")]
    [InlineData("udít[o]", "Vdít[o]")]
    [InlineData("s[o]prandáre", "S[o]prandáre")]
    [InlineData("s[o]prauád[o]", "S[o]prauád[o]")]
    [InlineData("s[o]prandái", "S[o]prandái")]
    [InlineData("s[o]prandát[o]", "S[o]prandát[o]")]
    [InlineData("variábile", "Variábile")]
    public void CapitaliseFirstLetter_String_ReturnsExpected(string input, string expected)
    {
        var actual = input.CapitaliseFirstLetter();
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("affielíre", "Affielíre")]
    [InlineData("affielísc[o]", "Affielísc[o]")]
    [InlineData("affielít[o]", "Affielít[o]")]
    [InlineData("audíre", "Audíre")]
    [InlineData("ód[o]", "Ód[o]")]
    [InlineData("udíj", "Vdíj")]
    [InlineData("udít[o]", "Vdít[o]")]
    [InlineData("s[o]prandáre", "S[o]prandáre")]
    [InlineData("s[o]prauád[o]", "S[o]prauád[o]")]
    [InlineData("s[o]prandái", "S[o]prandái")]
    [InlineData("s[o]prandát[o]", "S[o]prandát[o]")]
    [InlineData("variábile", "Variábile")]
    public void CapitaliseFirstLetter_Span_ReturnsExpected(string input, string expected)
    {
        var actual = input.AsSpan().CapitaliseFirstLetter();
        Assert.Equal(expected, actual);
    }
}
