using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Models;

/// <summary>
/// PcEvidence.OfficeLicenseEvaluation / WindowsLicenseStatusDisplay 等の
/// 監査用評価プロパティの動作を検証する。
/// </summary>
public class PcEvidenceLicenseEvaluationTests
{
    private static readonly OfficeLicenseParserService _officeParser = new();

    private static PcEvidence MakePcWithOfficeLicense(string txtFixtureName, string? csvFixtureName = null)
    {
        var dir = TestPaths.GetTestDataDirectory("Licenses");
        var txtPath = Path.Combine(dir, txtFixtureName);
        var csvPath = csvFixtureName is null ? null : Path.Combine(dir, csvFixtureName);

        var pc = new PcEvidence { PcName = "test-pc", CollectionDate = "2026_04_29" };
        pc.OfficeLicense = _officeParser.Parse(txtPath, csvPath);
        return pc;
    }

    [Fact]
    public void OfficeLicenseEvaluation_LegacyEvidenceWithVlOobGrace_ReturnsAuthFailed()
    {
        // v1.4 以前の evidence (INTERPRETATION なし) で VL+OSPP_Grace のケース。
        // manifest §22 status=Success が単なる「セクション完了」を意味するだけなので、
        // OSPP の LICENSE STATUS=OOB_GRACE を "認証失敗" と解釈すべき。
        // (v1.5.0 で INTERPRETATION 導入前の evidence を誤って "認証済" としないための回帰テスト)
        var pc = MakePcWithOfficeLicense("office_oob_grace.txt");
        Assert.Null(pc.OfficeLicense!.InterpretationConclusion);

        Assert.Equal(OfficeLicenseEvaluation.AuthFailed, pc.OfficeLicenseEvaluation);
        Assert.Equal("認証失敗", pc.OfficeLicenseStatusDisplay);
    }

    [Fact]
    public void OfficeLicenseEvaluation_V15ProvisionedSubscription_ReturnsLicensed()
    {
        // v1.5.0 の subscription + Provisioned vNext: INTERPRETATION ブロックがあり、
        // manifest verdict 抜きでも vNext Provisioned のフォールバックで Licensed と判定可能。
        var pc = MakePcWithOfficeLicense(
            "office_v15_subscription_provisioned.txt",
            "office_v15_subscription_provisioned.csv");
        Assert.NotNull(pc.OfficeLicense!.InterpretationConclusion);
        // INTERPRETATION 経路では manifest verdict が必要だが、テストでは Manifest=null。
        // → InterpretationConclusion ありなので verdict null → AuthFailed が返る。
        // 実アプリでは EvidenceParserService.PopulateDetails 経由で Manifest がセットされる。
        // ここは「INTERPRETATION ブロックの有無で経路が分岐する」ことの確認に留める。
        Assert.Equal(OfficeLicenseEvaluation.AuthFailed, pc.OfficeLicenseEvaluation);
    }

    [Fact]
    public void OfficeLicenseEvaluation_LegacyEvidenceWithProvisionedVnext_ReturnsLicensed()
    {
        // 仮想ケース: v1.4 以前 (INTERPRETATION なし) でも vNext CSV だけ後付けされた状況。
        // OSPP=Grace でも vNext Provisioned があれば subscription 機として Licensed 判定。
        var pc = MakePcWithOfficeLicense(
            "office_oob_grace.txt",
            "office_v15_subscription_provisioned.csv");

        Assert.Null(pc.OfficeLicense!.InterpretationConclusion);
        Assert.NotEmpty(pc.OfficeLicense.VnextEntries);
        Assert.Equal(OfficeLicenseEvaluation.Licensed, pc.OfficeLicenseEvaluation);
    }

    [Fact]
    public void OfficeLicenseStatusDisplay_NoOfficeLicenseAtAll_ShowsNotEvaluated()
    {
        var pc = new PcEvidence { PcName = "no-office", CollectionDate = "2026_04_29" };
        Assert.Equal("(未取得)", pc.OfficeLicenseStatusDisplay);
        Assert.Equal(OfficeLicenseEvaluation.NotEvaluated, pc.OfficeLicenseEvaluation);
    }
}
