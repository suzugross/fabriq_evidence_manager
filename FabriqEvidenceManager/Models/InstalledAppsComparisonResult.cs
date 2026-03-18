namespace FabriqEvidenceManager.Models;

/// <summary>
/// ベースラインPCとのインストール済みアプリ照合結果。
/// Items には差分 (Mismatch / Missing) のみを格納し、一致アプリは件数サマリで保持する。
/// </summary>
public sealed class InstalledAppsComparisonResult
{
    /// <summary>差分のある項目一覧 (Mismatch / NoActual / NoExpected のみ)</summary>
    public List<VerificationItem> Items { get; init; } = [];

    /// <summary>ベースライン側の総アプリ数</summary>
    public int TotalBaselineCount { get; init; }

    /// <summary>対象PC側の総アプリ数</summary>
    public int TotalActualCount { get; init; }

    /// <summary>名前・バージョンともに一致したアプリ数</summary>
    public int MatchCount { get; init; }

    /// <summary>全アプリが一致しているか</summary>
    public bool AllMatch => Items.Count == 0 && TotalBaselineCount > 0;

    /// <summary>差分件数</summary>
    public int MismatchCount => Items.Count;
}
