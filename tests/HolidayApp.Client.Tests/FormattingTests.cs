using HolidayApp.Client.Formatting;

namespace HolidayApp.Client.Tests;

public class FormattingTests
{
    [Theory]
    [InlineData(0.084, "80 m")]
    [InlineData(0.85, "850 m")]
    [InlineData(1.25, "1,3 km")]
    [InlineData(9.94, "9,9 km")]
    [InlineData(274.4, "274 km")]
    public void Distancias_em_formato_portugues(double km, string expected)
    {
        Assert.Equal(expected, Pt.Km(km));
    }
}
