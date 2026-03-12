using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// hostlist.csv の読み込みと PC名による検索を提供するサービス。
/// hostlist は任意機能であり、未読み込み時は空のまま動作する。
/// </summary>
public interface IHostlistService
{
    /// <summary>読み込み済みかどうか</summary>
    bool IsLoaded { get; }

    /// <summary>読み込み済みエントリ数</summary>
    int Count { get; }

    /// <summary>
    /// hostlist.csv を読み込む。
    /// </summary>
    /// <param name="csvPath">CSVファイルの絶対パス</param>
    /// <returns>読み込んだエントリ数</returns>
    int Load(string csvPath);

    /// <summary>
    /// NewPCName で検索する。見つからない場合は null。
    /// </summary>
    HostlistEntry? FindByPcName(string pcName);

    /// <summary>
    /// 読み込み済みの全エントリを返す。
    /// </summary>
    IReadOnlyList<HostlistEntry> GetAll();

    /// <summary>
    /// 読み込みをクリアする。
    /// </summary>
    void Clear();
}
