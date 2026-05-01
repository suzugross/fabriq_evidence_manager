namespace FabriqEvidenceManager.Models;

/// <summary>
/// 31_HardwareIdentifiers.txt のパース結果を表す集約モデル。
/// evidence_config v1.6.0+ §31 (kernel 3.0.0+) が出力する 4 ブロック構成
/// (Win32_ComputerSystem / Win32_ComputerSystemProduct / Win32_BaseBoard / Win32_SystemEnclosure) に対応。
/// 各 probe は §23 同様 inner try/catch で個別退避されるため、セクション自体が成功でも
/// 個別ブロックが取得不能な場合がある。取得不能ブロックは対応プロパティが null になる。
/// </summary>
public sealed class HardwareIdentifiersData
{
    /// <summary>Win32_ComputerSystem。"(Win32_ComputerSystem query failed: ...)" 時 null。</summary>
    public ComputerSystemInfo? ComputerSystem { get; init; }

    /// <summary>Win32_ComputerSystemProduct。"(Win32_ComputerSystemProduct query failed: ...)" 時 null。</summary>
    public ComputerSystemProductInfo? ComputerSystemProduct { get; init; }

    /// <summary>Win32_BaseBoard。"(Win32_BaseBoard query failed: ...)" 時 null。</summary>
    public BaseBoardInfo? BaseBoard { get; init; }

    /// <summary>Win32_SystemEnclosure。"(Win32_SystemEnclosure query failed: ...)" 時 null。</summary>
    public SystemEnclosureInfo? SystemEnclosure { get; init; }
}
