namespace FabriqEvidenceManager.Models;

/// <summary>
/// 01_SystemInfo.txt から抽出したシステム基本情報。
/// ベースライン照合でPC間の OS/バージョン/ハードウェア構成を比較するために使用する。
/// </summary>
public sealed class SystemInfoData
{
    /// <summary>ホスト名</summary>
    public string Hostname { get; init; } = "";

    /// <summary>OS名 (例: "Microsoft Windows 11 Pro")</summary>
    public string OsName { get; init; } = "";

    /// <summary>OSバージョン (例: "10.0.26200 (Build: 26200)")</summary>
    public string Version { get; init; } = "";

    /// <summary>CPU (例: "12th Gen Intel(R) Core(TM) i5-1235U")</summary>
    public string Cpu { get; init; } = "";

    /// <summary>メモリ (例: "16 GB")</summary>
    public string Memory { get; init; } = "";
}
