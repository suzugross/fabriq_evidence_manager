using CommunityToolkit.Mvvm.ComponentModel;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// 設定ダイアログでベースラインカテゴリの有効/無効を保持する mutable モデル。
/// <see cref="BaselineCategoryInfo"/> がイミュータブルなメタデータなのに対し、
/// こちらは UI バインディング用の状態保持。
/// </summary>
public sealed partial class BaselineCategoryOption : ObservableObject
{
    public required string CategoryId { get; init; }
    public required string DisplayName { get; init; }

    [ObservableProperty]
    private bool _isEnabled;
}
