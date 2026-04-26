using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.ViewModels;

/// <summary>
/// 環境設定ダイアログの ViewModel。
/// エビデンス取得チェックとベースライン比較カテゴリの 2 セクションを保持する。
/// 変更は即時反映 (OK/Cancel パターンではない) で、設定値変更を MainWindowViewModel
/// 側に通知して PC 一覧の再評価を促す。
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IBaselineService _baselineService;
    private readonly Action _onBaselineCategoriesChanged;

    /// <summary>
    /// エビデンス取得チェック (MAC / BitLocker / pc_information 等)。
    /// MainWindowViewModel と同一インスタンスを参照、変更は CheckOptions 自身の
    /// PropertyChanged 経由で MainWindowViewModel が拾う。
    /// </summary>
    public EvidenceCheckOptions CheckOptions { get; }

    /// <summary>ベースラインカテゴリの ON/OFF オプション一覧</summary>
    public ObservableCollection<BaselineCategoryOption> BaselineCategories { get; }

    /// <summary>ベースラインがロード済みか (UI ヒント表示用)</summary>
    public bool IsBaselineLoaded => _baselineService.IsLoaded;

    public SettingsViewModel(
        EvidenceCheckOptions checkOptions,
        IBaselineService baselineService,
        Action onBaselineCategoriesChanged)
    {
        CheckOptions = checkOptions;
        _baselineService = baselineService;
        _onBaselineCategoriesChanged = onBaselineCategoriesChanged;

        var enabled = baselineService.EnabledCategoryIds.ToHashSet(StringComparer.Ordinal);
        BaselineCategories = new ObservableCollection<BaselineCategoryOption>(
            baselineService.AvailableCategories.Select(c =>
            {
                var option = new BaselineCategoryOption
                {
                    CategoryId = c.CategoryId,
                    DisplayName = c.DisplayName,
                    IsEnabled = enabled.Contains(c.CategoryId),
                };
                option.PropertyChanged += OnCategoryOptionChanged;
                return option;
            }));
    }

    private void OnCategoryOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BaselineCategoryOption.IsEnabled)) return;

        var enabledIds = BaselineCategories.Where(c => c.IsEnabled).Select(c => c.CategoryId);
        _baselineService.SetEnabledCategoryIds(enabledIds);
        _onBaselineCategoriesChanged.Invoke();
    }
}
