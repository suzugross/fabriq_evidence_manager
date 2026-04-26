using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

/// <summary>
/// ChecklistParserService の回帰テスト。
/// v3.0.1 で修正したモジュールテーブル 7 列対応 (Verified 列追加) のガード。
/// </summary>
public class ChecklistParserServiceTests
{
    private readonly ChecklistParserService _sut = new();

    [Fact]
    public void ParseChecklistHtml_SevenColumnFormat_PopulatesMessageColumn()
    {
        // v3.0.1 回帰防止: kernel 2.x で Verified 列が追加され 7 列になっても
        // Message 列が消失せず、Time / Verified / Message が正しく分離されること。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("Checklist"), "seven_columns.html");

        var result = _sut.ParseChecklistHtml(path);

        Assert.Equal("OK", result.OverallStatus);
        Assert.Equal(2, result.ModuleItems.Count);

        var first = result.ModuleItems[0];
        Assert.Equal("10", first.Order);
        Assert.Equal("Change Hostname", first.ModuleName);
        Assert.Equal("System", first.Category);
        Assert.Equal("OK", first.Status);
        Assert.Equal("PASS", first.VerifiedStatus);
        Assert.Equal("22:18:00", first.Time);
        Assert.Contains("Hostname changed", first.Message);   // ★ Message が消失していないことが核心
    }

    [Fact]
    public void ParseChecklistHtml_VerifiedColumnNotRun_ParsesAsHyphen()
    {
        var path = Path.Combine(TestPaths.GetTestDataDirectory("Checklist"), "seven_columns.html");

        var result = _sut.ParseChecklistHtml(path);

        var second = result.ModuleItems[1];
        Assert.Equal("BitLocker Enable", second.ModuleName);
        Assert.Equal("-", second.VerifiedStatus);
        Assert.Equal("Success: 1, Skip: 0, Fail: 0", second.Message);
    }
}
