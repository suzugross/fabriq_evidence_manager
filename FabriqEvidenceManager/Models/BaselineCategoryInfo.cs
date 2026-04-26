namespace FabriqEvidenceManager.Models;

/// <summary>
/// ベースラインカテゴリの読み取り専用メタデータ。
/// <see cref="Services.Interfaces.IBaselineService.AvailableCategories"/> 経由で公開される。
/// </summary>
public sealed record BaselineCategoryInfo(string CategoryId, string DisplayName, bool IsEnabledByDefault);
