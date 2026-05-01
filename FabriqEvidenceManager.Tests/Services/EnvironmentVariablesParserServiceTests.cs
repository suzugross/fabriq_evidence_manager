using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

public class EnvironmentVariablesParserServiceTests
{
    private readonly EnvironmentVariablesParserService _sut = new();

    [Fact]
    public void Parse_FullModernPc_All21EntriesParsed()
    {
        // 実フィクスチャ (Machine 16 + User 5 = 21 行)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("EnvironmentVariables"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);

        Assert.Equal(21, entries.Count);
    }

    [Fact]
    public void Parse_FullModernPc_MachineAndUserScopeCounts()
    {
        // Machine と User のスコープ別件数を直接検証 (フリート集計の前提を保証)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("EnvironmentVariables"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);

        Assert.Equal(16, entries.Count(e => e.Scope == "Machine"));
        Assert.Equal(5,  entries.Count(e => e.Scope == "User"));
    }

    [Fact]
    public void Parse_FullModernPc_NumberOfProcessorsValueIs12()
    {
        // 数値変数の値が文字列として正しく保持されることを確認
        var path = Path.Combine(TestPaths.GetTestDataDirectory("EnvironmentVariables"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);
        var nproc = entries.Single(e => e.Scope == "Machine" && e.Name == "NUMBER_OF_PROCESSORS");

        Assert.Equal("12", nproc.Value);
    }

    [Fact]
    public void Parse_FullModernPc_LongPathValuePreserved()
    {
        // Machine Path は ; 区切りで複数パスを含む。CSV ダブルクォート処理が正しく動き、
        // 全文が 1 セルとして取り出せることを保証する。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("EnvironmentVariables"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);
        var machinePath = entries.Single(e => e.Scope == "Machine" && e.Name == "Path");

        // 一例: 末尾に nodejs パスがあること、; を含むこと、複数ディレクトリが連結されていること
        Assert.Contains("C:\\Program Files\\nodejs", machinePath.Value);
        Assert.Contains(";", machinePath.Value);
        Assert.True(machinePath.Value.Length > 100, "Machine Path should be substantially long");
    }

    [Fact]
    public void Parse_FullModernPc_UserOneDriveValueWithJapaneseCharacters()
    {
        // OneDrive 値は日本語企業名を含む UTF-8 文字列。BOM 付き UTF-8 が正しく読めることを保証。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("EnvironmentVariables"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);
        var oneDrive = entries.Single(e => e.Scope == "User" && e.Name == "OneDrive");

        Assert.Contains("OneDrive", oneDrive.Value);
        Assert.Contains("株式会社", oneDrive.Value);
    }

    [Fact]
    public void Parse_EmptyCsv_ReturnsEmptyList()
    {
        // ヘッダのみで行なし → 空コレクション (例外なし)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("EnvironmentVariables"), "empty.csv");

        var entries = _sut.Parse(path);

        Assert.Empty(entries);
    }

    [Fact]
    public void Parse_MissingFile_ReturnsEmptyList()
    {
        // ファイル不在時は例外を投げず空リスト
        var entries = _sut.Parse(Path.Combine(TestPaths.GetTestDataDirectory("EnvironmentVariables"), "does_not_exist.csv"));

        Assert.Empty(entries);
    }
}
