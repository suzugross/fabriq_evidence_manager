namespace FabriqEvidenceManager.Helpers;

/// <summary>
/// evidence フォルダの構造モード。
/// </summary>
public enum EvidenceMode
{
    /// <summary>
    /// フラット構造: evidence_root/{category}/{pc_folder}/
    /// 従来の evidence フォルダ形式。
    /// </summary>
    Flat,

    /// <summary>
    /// ネスト構造: destination/{pc_folder}/evidence/{category}/
    /// log_uploader が作成する PC 単位ラッパー形式。
    /// </summary>
    Nested,
}
