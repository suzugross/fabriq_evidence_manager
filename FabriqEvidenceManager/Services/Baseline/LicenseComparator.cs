using System;
using System.Collections.Generic;
using System.Linq;
using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Baseline;

/// <summary>
/// Windows / Office ライセンス情報のベースライン照合 (Phase 4 第 1 弾)。
///
/// PcEvidence.WindowsLicense / OfficeLicense の typed model を直接参照し、
/// パイロット PC とフリート全体で license posture が揃っているかを検証する。
///
/// 比較項目:
///   Windows: ライセンスファミリ / プロダクトキーチャネル / ライセンス状態 / KMS Machine
///   Office:  インストール状態 / C2R ProductReleaseIds / C2R UpdateChannel / ライセンス状態
///
/// 結果は <see cref="BaselineComparisonReport.LicenseComparison"/> に書き込む。
/// </summary>
public sealed class LicenseComparator : IBaselineComparator
{
    private WindowsLicenseData? _baselineWindows;
    private OfficeLicenseData? _baselineOffice;

    public string CategoryId => "License";
    public string DisplayName => "ライセンス";
    public bool IsEnabledByDefault => true;
    public bool HasBaselineData => _baselineWindows is not null || _baselineOffice is not null;

    public void CacheBaseline(PcEvidence baselinePc)
    {
        // PcEvidence の typed model をそのまま参照キャッシュ。再パース不要。
        _baselineWindows = baselinePc.WindowsLicense;
        _baselineOffice = baselinePc.OfficeLicense;
    }

    public void ClearBaseline()
    {
        _baselineWindows = null;
        _baselineOffice = null;
    }

    public void Compare(BaselineComparisonReport report, PcEvidence targetPc)
    {
        if (!HasBaselineData) return;

        var items = new List<VerificationItem>();

        // ---- Windows ライセンス比較 ----
        if (_baselineWindows is not null && targetPc.WindowsLicense is not null)
        {
            var bWin = _baselineWindows.Products.FirstOrDefault();
            var aWin = targetPc.WindowsLicense.Products.FirstOrDefault();

            items.Add(CompareField("Windows: ライセンスファミリ", bWin?.LicenseFamily, aWin?.LicenseFamily));
            items.Add(CompareField("Windows: プロダクトキーチャネル", bWin?.ProductKeyChannel, aWin?.ProductKeyChannel));
            items.Add(CompareField("Windows: ライセンス状態", FormatWindowsStatus(bWin), FormatWindowsStatus(aWin)));
            items.Add(CompareField("Windows: KMS Machine",
                _baselineWindows.Service?.KeyManagementServiceMachine,
                targetPc.WindowsLicense.Service?.KeyManagementServiceMachine));
        }

        // ---- Office ライセンス比較 ----
        if (_baselineOffice is not null && targetPc.OfficeLicense is not null)
        {
            items.Add(CompareField("Office: インストール状態",
                FormatBool(_baselineOffice.IsOfficeInstalled),
                FormatBool(targetPc.OfficeLicense.IsOfficeInstalled)));
            items.Add(CompareField("Office: C2R ProductReleaseIds",
                _baselineOffice.ClickToRun?.ProductReleaseIds,
                targetPc.OfficeLicense.ClickToRun?.ProductReleaseIds));
            items.Add(CompareField("Office: C2R UpdateChannel",
                _baselineOffice.ClickToRun?.UpdateChannel,
                targetPc.OfficeLicense.ClickToRun?.UpdateChannel));
            items.Add(CompareField("Office: ライセンス状態",
                _baselineOffice.Products.FirstOrDefault()?.LicenseStatusText,
                targetPc.OfficeLicense.Products.FirstOrDefault()?.LicenseStatusText));
        }

        if (items.Count > 0)
        {
            report.LicenseComparison = new LicenseComparisonResult { Items = items };
        }
    }

    private static string FormatWindowsStatus(WindowsLicensingProduct? p)
        => p is null ? string.Empty : $"{p.LicenseStatusText} ({p.LicenseStatusCode})";

    private static string FormatBool(bool value) => value ? "インストール済み" : "未インストール";

    private static VerificationItem CompareField(string itemName, string? expected, string? actual)
    {
        var exp = (expected ?? string.Empty).Trim();
        var act = (actual ?? string.Empty).Trim();

        VerificationStatus status;
        if (string.IsNullOrEmpty(exp) && string.IsNullOrEmpty(act))
            // 両方ともデータ不在 = 「揃っている」= Match。
            // (ベースライン PC ⇄ 自身を比較したときに 「Office 未インストール」 等で
            //  両側とも空のフィールドが NEW として false-positive 計上されないように)
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
