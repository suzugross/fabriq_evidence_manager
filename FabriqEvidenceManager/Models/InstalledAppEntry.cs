namespace FabriqEvidenceManager.Models;

/// <summary>
/// インストール済みアプリケーション1件のデータ。
/// 08_DesktopApps.csv / 08_StoreApps.csv から読み取る。
/// </summary>
public sealed class InstalledAppEntry
{
    /// <summary>アプリケーション名</summary>
    public required string Name { get; init; }

    /// <summary>バージョン</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>発行元</summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>インストール日 (DesktopApps のみ)</summary>
    public string InstallDate { get; init; } = string.Empty;

    /// <summary>ソース種別 (Desktop / Store)</summary>
    public InstalledAppSource Source { get; init; }
}

/// <summary>
/// アプリのインストールソース種別。
/// </summary>
public enum InstalledAppSource
{
    /// <summary>デスクトップアプリ (08_DesktopApps.csv)</summary>
    Desktop,

    /// <summary>ストアアプリ (08_StoreApps.csv)</summary>
    Store,
}
