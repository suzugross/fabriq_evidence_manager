using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// 納品用エビデンスディレクトリの自動整理を行う。
/// PC名のみのフォルダを新規作成し、対象PCの各種エビデンスファイルを収集・配置する。
/// 元データには一切の変更を加えず、コピーのみ行う。
/// </summary>
public interface IEvidenceCollectorService
{
    /// <summary>
    /// 親ディレクトリの下に "{YYYY_MM_DD_HHmmss}_fabriq_evi" 形式の納品用ディレクトリを作成し、
    /// そのパスを返す。同日複数回出力時の衝突回避のため秒精度のタイムスタンプを含む。
    /// </summary>
    /// <param name="parentDirectoryPath">ユーザーが選択した出力先親ディレクトリ</param>
    /// <returns>作成された納品用ディレクトリの絶対パス</returns>
    string CreateDeliveryDirectory(string parentDirectoryPath);

    /// <summary>
    /// 指定されたPCエビデンス群を、出力先ディレクトリに整理して配置する。
    /// コピーに失敗したファイルがある場合、そのエラー情報をリストとして返す。
    /// </summary>
    /// <param name="pcEvidences">収集対象のPCエビデンス一覧</param>
    /// <param name="outputDirectoryPath">出力先ディレクトリの絶対パス</param>
    /// <param name="progress">進捗報告 (0〜100)</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>コピーに失敗したファイルのエラー情報リスト (成功時は空リスト)</returns>
    Task<IReadOnlyList<string>> CollectAsync(
        IReadOnlyList<PcEvidence> pcEvidences,
        string outputDirectoryPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
