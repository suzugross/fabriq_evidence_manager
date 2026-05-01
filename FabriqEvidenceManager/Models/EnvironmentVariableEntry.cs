namespace FabriqEvidenceManager.Models;

/// <summary>
/// 27_EnvironmentVariables.csv の 1 行 = 環境変数 1 件。
/// evidence_config v1.6.0+ §27 (kernel 3.0.0+) は Machine スコープと User スコープの
/// 両方を 1 ファイルに出力する。
/// Scope は将来 fabriq 側で "Process" 等が増える可能性を考慮して enum 化せず raw 保持。
/// </summary>
public sealed class EnvironmentVariableEntry
{
    /// <summary>変数スコープ。"Machine" (システム環境変数) または "User" (ユーザー環境変数)。</summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>変数名 (例: "Path", "TEMP", "OneDrive")。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>変数値。Path 系は数百〜千文字超になりうる。</summary>
    public string Value { get; init; } = string.Empty;
}
