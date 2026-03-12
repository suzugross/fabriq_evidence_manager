using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// PC情報台帳をExcelファイルとして一括エクスポートする。
/// 列構成: PC名, シリアルナンバー, イーサネットMAC, Wi-Fi MAC, BitLocker回復キー(ドライブ別)
/// </summary>
public interface IExcelExportService
{
    /// <summary>
    /// 指定されたPCエビデンス群をExcelファイルとして出力する。
    /// </summary>
    /// <param name="pcEvidences">エクスポート対象のPCエビデンス一覧</param>
    /// <param name="outputFilePath">出力先Excelファイルの絶対パス (.xlsx)</param>
    /// <param name="options">突合結果・チェックリスト・実行履歴などの追加データ (省略可)</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    Task ExportAsync(
        IReadOnlyList<PcEvidence> pcEvidences,
        string outputFilePath,
        ExcelExportOptions? options = null,
        CancellationToken cancellationToken = default);
}
