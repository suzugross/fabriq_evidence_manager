namespace FabriqEvidenceManager.Models;

/// <summary>
/// export_history CSV の1行分を表すモデル。
/// fabriq の各モジュール実行結果を記録する。
/// </summary>
public sealed class ExportHistoryEntry
{
    /// <summary>実行タイムスタンプ (例: "2026-03-12 17:47:38")</summary>
    public string Timestamp { get; init; } = string.Empty;

    /// <summary>管理番号</summary>
    public string KanriNo { get; init; } = string.Empty;

    /// <summary>対象PC名</summary>
    public string PCName { get; init; } = string.Empty;

    /// <summary>モジュール名 (例: "Registry Config (HKCU) [seg:master_firewall_disable]")</summary>
    public string ModuleName { get; init; } = string.Empty;

    /// <summary>カテゴリ (例: "Registry", "Security", "System", "Evidence")</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>実行ステータス (Success / Error / Skipped)</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>メッセージ</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Windows ユーザー</summary>
    public string WindowsUser { get; init; } = string.Empty;

    /// <summary>作業者名</summary>
    public string Worker { get; init; } = string.Empty;

    /// <summary>メディアシリアル</summary>
    public string MediaSerial { get; init; } = string.Empty;

    /// <summary>セッションID</summary>
    public string SessionID { get; init; } = string.Empty;

    /// <summary>Success かどうか</summary>
    public bool IsSuccess => Status.Equals("Success", StringComparison.OrdinalIgnoreCase);

    /// <summary>Error かどうか</summary>
    public bool IsError => Status.Equals("Error", StringComparison.OrdinalIgnoreCase);
}
