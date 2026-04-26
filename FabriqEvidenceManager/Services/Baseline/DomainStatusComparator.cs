using System;
using System.Collections.Generic;
using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Baseline;

/// <summary>
/// ドメイン参加状態 (DomainStatusData) のベースライン照合。
///
/// PcEvidence.DomainStatus の typed model を直接参照し、フリート全 PC が
/// 同じドメイン / Azure AD テナントに参加しているか検証する。
/// CurrentUser は PC ごとに異なるため比較対象外。
///
/// 結果は <see cref="BaselineComparisonReport.DomainStatusComparison"/> に書き込む。
/// </summary>
public sealed class DomainStatusComparator : IBaselineComparator
{
    private DomainStatusData? _baseline;

    public string CategoryId => "DomainStatus";
    public string DisplayName => "ドメイン参加状態";
    public bool IsEnabledByDefault => true;
    public bool HasBaselineData => _baseline is not null;

    public void CacheBaseline(PcEvidence baselinePc)
    {
        _baseline = baselinePc.DomainStatus;
    }

    public void ClearBaseline() => _baseline = null;

    public void Compare(BaselineComparisonReport report, PcEvidence targetPc)
    {
        if (_baseline is null) return;
        var actual = targetPc.DomainStatus;
        if (actual is null) return;

        var items = new List<VerificationItem>
        {
            CompareField("ドメイン参加",      FormatBool(_baseline.PartOfDomain),    FormatBool(actual.PartOfDomain)),
            CompareField("ドメイン名",        _baseline.Domain,                       actual.Domain),
            CompareField("ドメインロール",    _baseline.DomainRole,                   actual.DomainRole),
            CompareField("Azure AD 参加",     FormatBool(_baseline.AzureAdJoined),    FormatBool(actual.AzureAdJoined)),
            CompareField("AD ドメイン参加",   FormatBool(_baseline.DomainJoined),     FormatBool(actual.DomainJoined)),
            CompareField("AD ドメイン名",     _baseline.DomainName,                   actual.DomainName),
            CompareField("Azure AD テナント", _baseline.TenantName,                   actual.TenantName),
        };

        report.DomainStatusComparison = new DomainStatusComparisonResult { Items = items };
    }

    private static string FormatBool(bool value) => value ? "参加" : "未参加";

    private static VerificationItem CompareField(string itemName, string? expected, string? actual)
    {
        var exp = (expected ?? string.Empty).Trim();
        var act = (actual ?? string.Empty).Trim();

        VerificationStatus status;
        if (string.IsNullOrEmpty(exp) && string.IsNullOrEmpty(act))
            // 両方空 = データ不在で揃っている = Match (LicenseComparator と同じ規約)
            status = VerificationStatus.Match;
        else if (string.IsNullOrEmpty(exp))
            status = VerificationStatus.NoExpected;
        else if (string.IsNullOrEmpty(act))
            status = VerificationStatus.NoActual;
        else
            status = exp.Equals(act, StringComparison.OrdinalIgnoreCase)
                ? VerificationStatus.Match
                : VerificationStatus.Mismatch;

        return new VerificationItem
        {
            ItemName = itemName,
            Expected = exp,
            Actual = act,
            Status = status,
        };
    }
}
