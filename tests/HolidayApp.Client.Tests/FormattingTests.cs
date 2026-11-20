using HolidayApp.Client.Formatting;

namespace HolidayApp.Client.Tests;

public class FormattingTests
{
    [Theory]
    [InlineData(0.084, "80 m")]
    [InlineData(0.85, "850 m")]
    [InlineData(1.25, "1.3 km")]
    [InlineData(9.94, "9.9 km")]
    [InlineData(274.4, "274 km")]
    public void Formats_distances(double km, string expected)
    {
        Assert.Equal(expected, Display.Km(km));
    }

    [Theory]
    [InlineData(20, "1 min")]
    [InlineData(900, "15 min")]
    [InlineData(3600, "1 h")]
    [InlineData(3900, "1 h 05")]
    [InlineData(9000, "2 h 30")]
    public void Formats_durations(double seconds, string expected)
    {
        Assert.Equal(expected, Display.Duration(seconds));
    }

    [Theory]
    [InlineData(0, "€0.00")]
    [InlineData(1234.5, "€1,234.50")]
    [InlineData(-12, "-€12.00")]
    public void Formats_money_in_euros(decimal value, string expected)
    {
        Assert.Equal(expected, Display.Money(value));
    }
}
