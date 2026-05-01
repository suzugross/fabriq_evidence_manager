using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

public class GroupPolicyParserServiceTests
{
    private readonly GroupPolicyParserService _sut = new();

    [Fact]
    public void Parse_FullModernPc_ExtractsAllSummaryFields()
    {
        // 実フィクスチャ (e-cri.local ドメイン参加機 / gpresult 成功 / HTML 240972 bytes)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("GroupPolicy"), "full_modern_pc_summary.txt");
        var fakeHtmlPath = @"C:\fake\24_GroupPolicy.html";

        var report = _sut.Parse(path, fakeHtmlPath);

        Assert.Equal(0, report.ExecutionStatus);
        Assert.True(report.IsSuccess);
        Assert.Equal(240972L, report.HtmlFileSizeBytes);
        Assert.True(report.PartOfDomain);
        Assert.Equal("e-cri.local", report.Domain);
        Assert.Equal("y_suzuki", report.ExecutingUser);
        Assert.Equal(fakeHtmlPath, report.HtmlFilePath);
    }

    [Fact]
    public void Parse_GpresultFailedSummary_NonZeroExitAndNotSuccess()
    {
        // 失敗ケース (exit code 1612 = "feature を install できなかった" 的なエラー)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("GroupPolicy"), "gpresult_failed_summary.txt");

        var report = _sut.Parse(path, htmlFilePath: null);

        Assert.Equal(1612, report.ExecutionStatus);
        Assert.False(report.IsSuccess);
        Assert.Equal(0L, report.HtmlFileSizeBytes);
        Assert.False(report.PartOfDomain);
        Assert.Equal(string.Empty, report.Domain);
        Assert.Equal("test", report.ExecutingUser);
        Assert.Null(report.HtmlFilePath);
    }

    [Fact]
    public void Parse_FullModernPc_HtmlPathNullWhenNotProvided()
    {
        // HTML ファイル不在時 (= dispatcher 側で File.Exists が false) は null を渡せる仕様
        var path = Path.Combine(TestPaths.GetTestDataDirectory("GroupPolicy"), "full_modern_pc_summary.txt");

        var report = _sut.Parse(path, htmlFilePath: null);

        Assert.Null(report.HtmlFilePath);
        // 他のフィールドは TXT から正常抽出されること
        Assert.Equal(0, report.ExecutionStatus);
        Assert.Equal("e-cri.local", report.Domain);
    }

    [Fact]
    public void Parse_FullModernPc_NoteLineDoesNotPolluteFields()
    {
        // "(NOTE: user-side RSoP reflects the executing user, not the end-user.)" のような
        // 括弧開始注釈行は ":" を含むが key:value として扱われないことを保証する。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("GroupPolicy"), "full_modern_pc_summary.txt");

        var report = _sut.Parse(path, htmlFilePath: null);

        // ExecutingUser が "y_suzuki" のまま (NOTE 行の "user-side RSoP..." で上書きされていない)
        Assert.Equal("y_suzuki", report.ExecutingUser);
    }
}
