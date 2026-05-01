namespace FabriqEvidenceManager.Models;

/// <summary>
/// 28_StartupItems.csv の 1 行 = 自動起動項目 1 件。
/// evidence_config v1.6.0+ §28 (kernel 3.0.0+) はレジストリ Run キー由来の項目と
/// タスクスケジューラ logon 起動の項目を 1 ファイルに統合して出力する。
/// Source は将来 "WMI_AutoStart" 等の追加に備えて enum 化せず raw 保持。
/// </summary>
public sealed class StartupItemEntry
{
    /// <summary>項目の出所。"Win32_StartupCommand" (Run キー) または "ScheduledTask" (タスク)。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>項目名 (例: "GoogleDriveFS", "Adobe Acrobat Update Task")。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>実行ユーザー (例: "NT AUTHORITY\\SYSTEM", "E-CRI\\y_suzuki")。タスクで空のことあり。</summary>
    public string User { get; init; } = string.Empty;

    /// <summary>実行コマンド/exe パス (引数を含むことあり)。</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>レジストリパス (Run キー) またはタスクフォルダ (例: "\\Microsoft\\Office\\")。</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>有効フラグ。CSV では "True"/"False"、TryParse 失敗時 false (LocalUserEntry 同流派)。</summary>
    public bool Enabled { get; init; }
}
