using System;
using System.Collections.Generic;
using System.Linq;
using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Helpers;

/// <summary>
/// インストール済みアプリ件数の表示文字列化ヘルパ。
/// 実 §11 CSV には Wow6432Node 重複登録 (Desktop) や Get-AppxPackage マルチユーザ重複
/// (Store) で同名行が入りうる。一覧表示の "件数" がベースライン突合の TotalBaselineCount
/// (Name dedup 後) と食い違う症状を解消するため、CSV 行数と Name 一意件数を併記する。
/// </summary>
public static class InstalledAppCountFormatter
{
    /// <summary>
    /// アプリ一覧の件数を表示用に整形する。
    /// 重複ゼロ: "{N} 件" / 重複あり: "{N} 件 (一意 {unique})" / 空: "0 件"。
    /// </summary>
    public static string Format(IReadOnlyList<InstalledAppEntry> apps)
    {
        var rows = apps.Count;
        if (rows == 0) return "0 件";
        var unique = apps.Select(a => a.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        return rows == unique ? $"{rows} 件" : $"{rows} 件 (一意 {unique})";
    }
}
