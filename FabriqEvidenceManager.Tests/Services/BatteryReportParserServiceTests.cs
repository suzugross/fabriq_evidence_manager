using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

public class BatteryReportParserServiceTests
{
    private readonly BatteryReportParserService _sut = new();

    [Fact]
    public void Parse_RealLaptopFixture_ExtractsAllBatteryFields()
    {
        var path = Path.Combine(TestPaths.GetTestDataDirectory("Battery"), "laptop_real.html");

        var result = _sut.Parse(path);

        // 実フィクスチャ: ASUSTeK B1402CVA, OEM X421-35, Li, Design 42068 / Full 38797 / Cycle 102
        Assert.Equal("X421-35", result.Name);
        Assert.Equal("OEM", result.Manufacturer);
        Assert.Equal("123456789", result.SerialNumber);
        Assert.Equal("Li", result.Chemistry);
        Assert.Equal(42068, result.DesignCapacityMwh);
        Assert.Equal(38797, result.FullChargeCapacityMwh);
        Assert.Equal(102, result.CycleCount);

        // Health % = 38797 / 42068 * 100 ≒ 92.2 (90-95 帯 → 黄になる)
        Assert.NotNull(result.HealthPercent);
        Assert.InRange(result.HealthPercent!.Value, 92.0, 92.5);
    }

    [Fact]
    public void HealthPercent_ReturnsNull_WhenDesignCapacityIsZero()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            // FullCharge は出るが DesignCapacity が 0 のケース (壊れた report)
            File.WriteAllText(tmp,
                "<html><body>"
                + @"<span class=""label"">DESIGN CAPACITY</span></td><td>0 mWh</td>"
                + @"<span class=""label"">FULL CHARGE CAPACITY</span></td><td>40,000 mWh</td>"
                + "</body></html>");
            var result = _sut.Parse(tmp);
            Assert.Equal(0, result.DesignCapacityMwh);
            Assert.Equal(40000, result.FullChargeCapacityMwh);
            Assert.Null(result.HealthPercent);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void Parse_HtmlWithoutBatteryFields_ReturnsAllNullsButNoException()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, "<html><body><h1>No battery info here</h1></body></html>");
            var result = _sut.Parse(tmp);

            Assert.Equal(string.Empty, result.Name);
            Assert.Null(result.DesignCapacityMwh);
            Assert.Null(result.FullChargeCapacityMwh);
            Assert.Null(result.CycleCount);
            Assert.Null(result.HealthPercent);
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
