using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// PC ルートディレクトリ直下の <c>manager_memo.json</c> を読み書きする。
/// fabriq が生成する evidence/ サブツリーには触らず、PC ルート (タイムスタンプ付き
/// PC ディレクトリ) に co-located で配置する。
/// </summary>
public interface IPcMemoService
{
    /// <summary>メモを読み込む (ファイル不在時 null)。読み取り失敗時も null。</summary>
    PcMemo? Load(string pcRootDirectory);

    /// <summary>メモを書き込む。既存ファイルは上書き、空テキストでもファイルは生成する。</summary>
    void Save(string pcRootDirectory, PcMemo memo);

    /// <summary>メモファイルを削除する (UI でのクリア操作用)。</summary>
    void Delete(string pcRootDirectory);
}
