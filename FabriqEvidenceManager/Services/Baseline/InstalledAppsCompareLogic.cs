using System;
using System.Collections.Generic;
using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Baseline;

/// <summary>
/// インストール済みアプリの差分検出ロジック (Desktop / Store 共用)。
/// アプリ名 (case-insensitive) をキーに Version を比較し、差分のみ Items に格納する。
/// Desktop / Store の各 Comparator が同じロジックを使うため共通化 (DRY)。
/// </summary>
internal static class InstalledAppsCompareLogic
{
    /// <summary>
    /// ベースライン PC のアプリ一覧と対象 PC のアプリ一覧を突合する。
    /// </summary>
    /// <param name="baseline">ベースライン側のアプリ一覧 (片側ソースのみ)</param>
    /// <param name="actual">対象 PC 側のアプリ一覧 (片側ソースのみ)</param>
    /// <returns>差分集計結果。一致アプリは MatchCount に、差分は Items に。</returns>
    public static InstalledAppsComparisonResult Compare(
        IReadOnlyList<InstalledAppEntry> baseline,
        IReadOnlyList<InstalledAppEntry> actual)
    {
        // Name (case-insensitive) をキーに辞書化 (同名は最後勝ち)
        var baselineMap = new Dictionary<string, InstalledAppEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in baseline)
            baselineMap[app.Name] = app;

        var actualMap = new Dictionary<string, InstalledAppEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in actual)
            actualMap[app.Name] = app;

        var diffItems = new List<VerificationItem>();
        var matchCount = 0;

        // ベースライン側の各アプリを基準に比較
        foreach (var (name, baselineApp) in baselineMap)
        {
            if (actualMap.TryGetValue(name, out var actualApp))
            {
                if (baselineApp.Version.Equals(actualApp.Version, StringComparison.OrdinalIgnoreCase))
                {
                    matchCount++;
                }
                else
                {
                    diffItems.Add(new VerificationItem
                    {
                        ItemName = name,
                        Expected = baselineApp.Version,
                        Actual = actualApp.Version,
                        Status = VerificationStatus.Mismatch,
                    });
                }
            }
            else
            {
                diffItems.Add(new VerificationItem
                {
                    ItemName = name,
                    Expected = baselineApp.Version,
                    Actual = "(未検出)",
                    Status = VerificationStatus.NoActual,
                });
            }
        }

        // 対象 PC のみ存在するアプリ
        foreach (var (name, actualApp) in actualMap)
        {
            if (!baselineMap.ContainsKey(name))
            {
                diffItems.Add(new VerificationItem
                {
                    ItemName = name,
                    Expected = "(ベースラインなし)",
                    Actual = actualApp.Version,
                    Status = VerificationStatus.NoExpected,
                });
            }
        }

        return new InstalledAppsComparisonResult
        {
            Items = diffItems,
            TotalBaselineCount = baselineMap.Count,
            TotalActualCount = actualMap.Count,
            MatchCount = matchCount,
        };
    }
}
