using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

public class StartupItemsParserServiceTests
{
    private readonly StartupItemsParserService _sut = new();

    [Fact]
    public void Parse_FullModernPc_All29EntriesParsed()
    {
        // 実フィクスチャ (Win32_StartupCommand 14 + ScheduledTask 15 = 29 行)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("StartupItems"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);

        Assert.Equal(29, entries.Count);
    }

    [Fact]
    public void Parse_FullModernPc_SourceCounts()
    {
        // Source 別カウントを直接検証 (Phase C "Registry: M / Task: N" 表示の前提を保証)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("StartupItems"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);

        Assert.Equal(14, entries.Count(e => e.Source == "Win32_StartupCommand"));
        Assert.Equal(15, entries.Count(e => e.Source == "ScheduledTask"));
    }

    [Fact]
    public void Parse_FullModernPc_AllEntriesEnabled()
    {
        // 実フィクスチャは全項目 Enabled=True。bool.TryParse の正常動作を保証。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("StartupItems"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);

        Assert.All(entries, e => Assert.True(e.Enabled));
    }

    [Fact]
    public void Parse_FullModernPc_RegistryItemHasHkmlOrHkuLocation()
    {
        // Win32_StartupCommand エントリは HKLM\ または HKU\ で始まる Run キーを Location に持つ
        var path = Path.Combine(TestPaths.GetTestDataDirectory("StartupItems"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);
        var registryItems = entries.Where(e => e.Source == "Win32_StartupCommand").ToList();

        Assert.NotEmpty(registryItems);
        Assert.All(registryItems, e =>
            Assert.True(
                e.Location.StartsWith("HKLM\\", System.StringComparison.OrdinalIgnoreCase)
                || e.Location.StartsWith("HKU\\", System.StringComparison.OrdinalIgnoreCase),
                $"Registry item Location should start with HKLM or HKU: {e.Location}"));
    }

    [Fact]
    public void Parse_FullModernPc_ScheduledTaskUserCanBeEmpty()
    {
        // ScheduledTask の User 列は空欄になりうる仕様。空文字列として保持されることを確認。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("StartupItems"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);
        var taskEntries = entries.Where(e => e.Source == "ScheduledTask").ToList();

        Assert.Contains(taskEntries, e => string.IsNullOrEmpty(e.User));
    }

    [Fact]
    public void Parse_FullModernPc_OfficeAutomaticUpdatesPresent()
    {
        // 特定エントリ (Office Automatic Updates 2.0) のフィールド全件を検証
        var path = Path.Combine(TestPaths.GetTestDataDirectory("StartupItems"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);
        var office = entries.Single(e => e.Name == "Office Automatic Updates 2.0");

        Assert.Equal("ScheduledTask", office.Source);
        Assert.Equal("SYSTEM", office.User);
        Assert.Contains("OfficeC2RClient.exe", office.Command);
        Assert.Equal("\\Microsoft\\Office\\", office.Location);
        Assert.True(office.Enabled);
    }

    [Fact]
    public void Parse_EmptyCsv_ReturnsEmptyList()
    {
        // ヘッダのみで行なし → 空コレクション (例外なし)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("StartupItems"), "empty.csv");

        var entries = _sut.Parse(path);

        Assert.Empty(entries);
    }

    [Fact]
    public void Parse_MissingFile_ReturnsEmptyList()
    {
        var entries = _sut.Parse(Path.Combine(TestPaths.GetTestDataDirectory("StartupItems"), "does_not_exist.csv"));

        Assert.Empty(entries);
    }
}
