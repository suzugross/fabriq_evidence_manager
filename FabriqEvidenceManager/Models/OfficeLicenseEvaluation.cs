namespace FabriqEvidenceManager.Models;

/// <summary>
/// Office ライセンスの監査評価結果。
/// PcEvidence.OfficeLicenseEvaluation で算出され、ValidateEvidence (赤警告) と
/// EvaluateCaution (オレンジ要確認) の両方で利用される。
/// </summary>
public enum OfficeLicenseEvaluation
{
    /// <summary>OfficeLicense 未取得 (manifest §22 不在 / §22 Failed)</summary>
    NotEvaluated,
    /// <summary>Office 未インストール (C2R/OSPP どちらも未検出)</summary>
    NotInstalled,
    /// <summary>manifest §22 verdict=Success / 旧 evidence の OSPP IsLicensed=true</summary>
    Licensed,
    /// <summary>manifest §22 verdict=Partial (M365 サブスクリプションだが vNext 未 Provisioned)</summary>
    SignInPending,
    /// <summary>manifest §22 verdict=Failed / 旧 evidence の OSPP IsLicensed=false</summary>
    AuthFailed,
}
