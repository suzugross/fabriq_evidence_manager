using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.ViewModels;

/// <summary>
/// PC 詳細ウィンドウ専用 ViewModel。
/// 1 PC = 1 ウィンドウで完結するよう、対象 PcEvidence と関連レポートを
/// 自身が保持する (MainWindowViewModel に追従しない)。
/// 異なる PC を別ウィンドウで並べて比較する用途に対応。
/// </summary>
public sealed partial class PcDetailViewModel : ObservableObject
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif"];

    public PcEvidence Pc { get; }
    public EvidenceCheckOptions CheckOptions { get; }
    public bool IsHostlistLoaded { get; }
    public bool IsBaselineLoaded { get; }

    /// <summary>突合レポート (双方向リアルタイムではなく、ウィンドウ起動時のスナップショット)</summary>
    public VerificationReport? VerificationReport { get; }
    public ChecklistResult? ChecklistResult { get; }
    public IReadOnlyList<ExportHistoryEntry>? ExportHistory { get; }
    public BaselineComparisonReport? BaselineReport { get; }

    public string? ChecklistFileName { get; }

    // ---- セクション存在フラグ (Visibility binding 用) ----

    public bool HasDomainStatus => Pc.DomainStatus is not null;
    public bool HasLocalUsers => Pc.LocalUsers.Count > 0;
    public bool HasLocalGroups => Pc.LocalGroups.Count > 0;
    public bool HasLocalGroupMembers => Pc.LocalGroupMembers.Count > 0;
    public bool HasFirewallProfiles => Pc.FirewallProfiles.Count > 0;
    public bool HasFirewallRules => Pc.FirewallRules.Count > 0;
    public bool HasOptionalFeatures => Pc.OptionalFeatures.Count > 0;
    public bool HasUserProfiles => Pc.UserProfiles.Count > 0;
    public bool HasDisks => Pc.Disks.Count > 0;
    public bool HasPartitions => Pc.Partitions.Count > 0;
    public bool HasPowerSettings => Pc.PowerSettings is not null;
    public bool HasWiFiProfiles => Pc.WiFiProfiles.Count > 0;
    public bool HasRestorePoints => Pc.RestorePoints.Count > 0;
    public bool HasDefenderStatus => Pc.DefenderStatus is not null;
    public bool HasWindowsUpdates => Pc.WindowsUpdates.Count > 0;
    public bool HasHardwareIdentifiers => Pc.HardwareIdentifiers is not null;
    // §31 サブブロックは BoolToVis が bool 専用のため object 参照ではなく
    // bool フラグを経由する (各 probe 失敗時に対応ブロックを自動非表示)
    public bool HasHwComputerSystem        => Pc.HardwareIdentifiers?.ComputerSystem is not null;
    public bool HasHwComputerSystemProduct => Pc.HardwareIdentifiers?.ComputerSystemProduct is not null;
    public bool HasHwBaseBoard             => Pc.HardwareIdentifiers?.BaseBoard is not null;
    public bool HasHwSystemEnclosure       => Pc.HardwareIdentifiers?.SystemEnclosure is not null;

    // §29 メモリ
    public bool HasMemorySlots          => Pc.MemorySlots.Count > 0;
    public bool HasMemoryArraySummaries => Pc.MemoryArraySummaries.Count > 0;
    public bool HasMemoryInventory      => HasMemorySlots || HasMemoryArraySummaries;

    /// <summary>装着総容量 (GB)。Phase C 上段サマリ表示用。</summary>
    public int MemoryInstalledTotalGB => Pc.MemorySlots.Sum(s => s.CapacityGB ?? 0);

    /// <summary>マザーボード設計上の最大搭載量 (GB)。アレイサマリ集計値。</summary>
    public int MemoryMaxCapacityGB => Pc.MemoryArraySummaries.Sum(a => a.MaxCapacityGB ?? 0);

    /// <summary>"装着/全スロット" の表示文字列 (例: "2/2"、アレイ情報無時 "2/-")。</summary>
    public string MemorySlotUsageDisplay
    {
        get
        {
            var installed = Pc.MemorySlots.Count;
            var total = Pc.MemoryArraySummaries.Sum(a => a.MemoryDevices ?? 0);
            return total > 0 ? $"{installed}/{total}" : $"{installed}/-";
        }
    }

    // ---- AUTO CAPTURE プレビュー画像 ----

    public ObservableCollection<string> PreviewImagePaths { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextPreviewImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPreviewImageCommand))]
    private int _currentPreviewIndex;

    public string? CurrentPreviewImagePath =>
        PreviewImagePaths.Count > 0 && CurrentPreviewIndex >= 0 && CurrentPreviewIndex < PreviewImagePaths.Count
            ? PreviewImagePaths[CurrentPreviewIndex]
            : null;

    public string PreviewCounterText =>
        PreviewImagePaths.Count > 0
            ? $"{CurrentPreviewIndex + 1} / {PreviewImagePaths.Count}"
            : "0 / 0";

    public bool HasMultipleImages => PreviewImagePaths.Count > 1;

    partial void OnCurrentPreviewIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentPreviewImagePath));
        OnPropertyChanged(nameof(PreviewCounterText));
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextPreviewImage() => CurrentPreviewIndex++;
    private bool CanGoNext() => CurrentPreviewIndex < PreviewImagePaths.Count - 1;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousPreviewImage() => CurrentPreviewIndex--;
    private bool CanGoPrevious() => CurrentPreviewIndex > 0;

    private readonly IPcMemoService _memoService;

    /// <summary>編集中のメモ本文 (UI バインド)。SaveMemoCommand で永続化。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveMemoCommand))]
    private string _memoText = string.Empty;

    /// <summary>最終更新日時/更新者 表示用文字列</summary>
    public string MemoLastUpdatedDisplay => Pc.Memo is null
        ? "(未保存)"
        : $"最終更新: {Pc.Memo.LastUpdatedAt:yyyy/MM/dd HH:mm} ({Pc.Memo.LastUpdatedBy})";

    public PcDetailViewModel(
        PcEvidence pc,
        EvidenceCheckOptions checkOptions,
        bool isHostlistLoaded,
        bool isBaselineLoaded,
        VerificationReport? verification,
        ChecklistResult? checklist,
        IReadOnlyList<ExportHistoryEntry>? history,
        BaselineComparisonReport? baseline,
        IPcMemoService memoService)
    {
        Pc = pc;
        CheckOptions = checkOptions;
        IsHostlistLoaded = isHostlistLoaded;
        IsBaselineLoaded = isBaselineLoaded;
        VerificationReport = verification;
        ChecklistResult = checklist;
        ExportHistory = history;
        BaselineReport = baseline;
        _memoService = memoService;

        ChecklistFileName = pc.ChecklistFilePath is not null && File.Exists(pc.ChecklistFilePath)
            ? Path.GetFileName(pc.ChecklistFilePath)
            : null;

        // 既存メモがあればロード
        MemoText = pc.Memo?.Text ?? string.Empty;

        LoadPreviewImages();
    }

    /// <summary>メモを永続化する。PcRootDirectoryPath が無い場合は実行不可。</summary>
    [RelayCommand(CanExecute = nameof(CanSaveMemo))]
    private void SaveMemo()
    {
        if (Pc.PcRootDirectoryPath is null) return;

        var memo = new PcMemo
        {
            Text = MemoText,
            LastUpdatedAt = DateTimeOffset.Now,
            LastUpdatedBy = Environment.UserName,
        };
        _memoService.Save(Pc.PcRootDirectoryPath, memo);
        Pc.Memo = memo;
        OnPropertyChanged(nameof(MemoLastUpdatedDisplay));
    }

    private bool CanSaveMemo() => Pc.PcRootDirectoryPath is not null;

    private void LoadPreviewImages()
    {
        if (Pc.AutoCaptureDirectoryPath is null || !Directory.Exists(Pc.AutoCaptureDirectoryPath))
            return;

        foreach (var path in Directory.EnumerateFiles(Pc.AutoCaptureDirectoryPath)
                     .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                     .OrderBy(f => f))
        {
            PreviewImagePaths.Add(path);
        }
    }
}
