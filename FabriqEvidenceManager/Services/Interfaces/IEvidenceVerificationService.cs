using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// hostlist の期待値とエビデンスの実測値を突き合わせ、検証レポートを生成する。
/// hostlist が未読み込みの場合は HostlistFound=false のレポートを返す。
/// </summary>
public interface IEvidenceVerificationService
{
    /// <summary>
    /// 指定PCの突合レポートを生成する。
    /// </summary>
    /// <param name="pc">PcEvidence (pc_information パスが必要)</param>
    /// <returns>突合レポート</returns>
    VerificationReport Verify(PcEvidence pc);
}
