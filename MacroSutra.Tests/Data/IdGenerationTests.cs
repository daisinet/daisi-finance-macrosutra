using MacroSutra.Data;

namespace MacroSutra.Tests.Data;

public class IdGenerationTests
{
    [Theory]
    [InlineData("msu")]
    [InlineData("str")]
    [InlineData("trd")]
    [InlineData("bra")]
    [InlineData("pos")]
    [InlineData("sub")]
    public void GenerateId_StartsWithPrefix(string prefix)
    {
        var id = MacroSutraCosmo.GenerateId(prefix);
        Assert.StartsWith($"{prefix}-", id);
    }

    [Fact]
    public void GenerateId_HasCorrectFormat()
    {
        var id = MacroSutraCosmo.GenerateId("tst");

        // Format: prefix-yyMMddHHmmss-8hexchars
        var parts = id.Split('-');
        Assert.Equal(3, parts.Length);
        Assert.Equal("tst", parts[0]);
        Assert.Equal(12, parts[1].Length); // yyMMddHHmmss
        Assert.Equal(8, parts[2].Length);  // 8 hex chars
    }

    [Fact]
    public void GenerateId_IsUnique()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => MacroSutraCosmo.GenerateId("msu")).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void GenerateId_ContainsTimestamp()
    {
        var before = DateTime.UtcNow;
        var id = MacroSutraCosmo.GenerateId("msu");
        var after = DateTime.UtcNow;

        var parts = id.Split('-');
        var timestampStr = parts[1];
        var year = int.Parse(timestampStr[..2]) + 2000;
        var month = int.Parse(timestampStr[2..4]);

        Assert.InRange(year, before.Year, after.Year);
        Assert.InRange(month, 1, 12);
    }
}
