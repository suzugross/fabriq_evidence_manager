namespace FabriqEvidenceManager.Models;

/// <summary>
/// LSA Protection (RunAsPPL) の解釈状態 (evidence_config §23d)。
/// HKLM\SYSTEM\CurrentControlSet\Control\Lsa\RunAsPPL レジストリ値由来。
/// </summary>
public enum LsaRunAsPplState
{
    /// <summary>"Unknown (...)" / 取得失敗 / parse 不能</summary>
    Unknown,
    /// <summary>"Off" / "Off (registry value absent)" — 監査上 NG</summary>
    Off,
    /// <summary>"On (PPL)" — 1 = PPL on (UEFI Lock なし)</summary>
    On,
    /// <summary>"On + UEFI Lock (PPL)" — 2 = PPL on + UEFI Lock (推奨状態)</summary>
    OnWithUefiLock,
}

/// <summary>
/// LSA Protection 情報。evidence_config §23d で 5 種類の RunAsPPL テキスト出力を受ける。
/// セクション全体が "(LSA Protection registry query failed: ...)" の場合は
/// <see cref="SecurityBaselineData.Lsa"/> 自体が null になる。
/// </summary>
public sealed class LsaProtectionInfo
{
    public LsaRunAsPplState RunAsPpl { get; init; }

    /// <summary>UI にそのまま出す元テキスト (例: "Off (registry value absent)")</summary>
    public string RunAsPplRawText { get; init; } = string.Empty;

    /// <summary>RunAsPPLBoot レジストリ値 (取得不能 / 不在時 null)</summary>
    public int? RunAsPplBoot { get; init; }
}
