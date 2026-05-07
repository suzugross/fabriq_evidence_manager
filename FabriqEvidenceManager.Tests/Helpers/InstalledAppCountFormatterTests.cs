using FabriqEvidenceManager.Helpers;
using FabriqEvidenceManager.Models;
using Xunit;

namespace FabriqEvidenceManager.Tests.Helpers;

/// <summary>
/// InstalledAppCountFormatter の表示出力を検証する。
/// 実 §11 CSV には Name 重複行が含まれる (Wow6432Node / マルチユーザ Store) ため
/// PcDetailWindow の件数表示とベースライン突合の TotalBaselineCount がズレないよう
/// "{rows} 件 (一意 {unique})" 形式で両方の数を併記する仕様を保証する回帰テスト。
/// </summary>
public class InstalledAppCountFormatterTests
{
    [Fact]
    public void Format_Empty_Returns0Ken()
    {
        var result = InstalledAppCountFormatter.Format(System.Array.Empty<InstalledAppEntry>());
        Assert.Equal("0 件", result);
    }

    [Fact]
    public void Format_NoDuplicates_OmitsUniqueParenthetical()
    {
        var apps = new[]
        {
            new InstalledAppEntry { Name = "Adobe Acrobat", Version = "1.0" },
            new InstalledAppEntry { Name = "Chromium",      Version = "73.0" },
            new InstalledAppEntry { Name = "Microsoft Edge", Version = "120.0" },
        };

        var result = InstalledAppCountFormatter.Format(apps);

        Assert.Equal("3 件", result);
    }

    [Fact]
    public void Format_WithDuplicateNames_AppendsUniqueCount()
    {
        // Wow6432Node 由来の重複登録を再現 (同名 / 別 Publisher の 2 行があっても
        // Name 一意件数は 1 にカウントされ、CSV 行数 2 と併記表示される)
        var apps = new[]
        {
            new InstalledAppEntry { Name = "Microsoft.VCLibs.140.00", Version = "14.0", Publisher = "8wekyb3d8bbwe" },
            new InstalledAppEntry { Name = "Microsoft.VCLibs.140.00", Version = "14.0", Publisher = "8wekyb3d8bbwe" },
            new InstalledAppEntry { Name = "Adobe Acrobat", Version = "1.0" },
        };

        var result = InstalledAppCountFormatter.Format(apps);

        Assert.Equal("3 件 (一意 2)", result);
    }

    [Fact]
    public void Format_NameDedupIsCaseInsensitive()
    {
        // Comparator 側 (InstalledAppsCompareLogic) も case-insensitive で dedup するため
        // Formatter のカウントロジックと同じ意味的整合性を保証する。
        var apps = new[]
        {
            new InstalledAppEntry { Name = "Adobe Acrobat", Version = "1.0" },
            new InstalledAppEntry { Name = "ADOBE ACROBAT", Version = "1.0" },
        };

        var result = InstalledAppCountFormatter.Format(apps);

        Assert.Equal("2 件 (一意 1)", result);
    }

    [Fact]
    public void Format_RealFixtureProportions_DesktopShows41With38Unique()
    {
        // 実フィクスチャ (test01_3WNCV04 / Desktop 41 行 / 一意 38) の振る舞いを再現。
        // 数値の根拠: TestData/InstalledApps/full_modern_pc_desktop.csv の sort -u 結果。
        var apps = new System.Collections.Generic.List<InstalledAppEntry>();
        for (var i = 0; i < 38; i++)
            apps.Add(new InstalledAppEntry { Name = $"app_{i}", Version = "1.0" });
        // 3 件は既存名と同じ (Wow6432Node 重複相当)
        apps.Add(new InstalledAppEntry { Name = "app_0", Version = "1.0" });
        apps.Add(new InstalledAppEntry { Name = "app_1", Version = "1.0" });
        apps.Add(new InstalledAppEntry { Name = "app_2", Version = "1.0" });

        var result = InstalledAppCountFormatter.Format(apps);

        Assert.Equal("41 件 (一意 38)", result);
    }
}
