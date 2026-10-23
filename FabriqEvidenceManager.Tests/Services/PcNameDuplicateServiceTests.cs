using System.Linq;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

/// <summary>
/// 計画 PC 名の重複 (誤って同じホスト名を別端末に設定したケース) の判定ルールを検証する。
/// </summary>
public class PcNameDuplicateServiceTests
{
    private readonly PcNameDuplicateService _sut = new();

    private static PcEvidence Pc(string pcName, string serial, string time = "120000") => new()
    {
        PcName = pcName,
        SerialNumber = serial,
        CollectionDate = $"2026_10_23_{time}",
    };

    [Fact]
    public void Evaluate_DifferentPcNames_NoDuplicate()
    {
        var a = Pc("PC-01", "SN-A");
        var b = Pc("PC-02", "SN-B");

        var groups = _sut.Evaluate([a, b]);

        Assert.Empty(groups);
        Assert.False(a.HasDuplicatePcName);
        Assert.False(b.HasDuplicatePcName);
        Assert.Equal(0, a.DuplicatePcNameMachineCount);
    }

    [Fact]
    public void Evaluate_SameNameDifferentSerial_FlagsBoth()
    {
        var a = Pc("PC-01", "SN-A");
        var b = Pc("PC-01", "SN-B", "130000");

        var groups = _sut.Evaluate([a, b]);

        var group = Assert.Single(groups);
        Assert.Equal("PC-01", group.PcName);
        Assert.Equal(2, group.MachineCount);
        Assert.Equal(2, group.Members.Count);
        Assert.Equal("PC-01 (別端末 2 台)", group.Summary);
        Assert.True(a.HasDuplicatePcName);
        Assert.True(b.HasDuplicatePcName);
        Assert.Equal(2, a.DuplicatePcNameMachineCount);
    }

    [Fact]
    public void Evaluate_SameNameSameSerial_RerunIsNotDuplicate()
    {
        // 同じ端末を同じ PC 名で再キッティング (フォルダは日時違いで 2 つ) → 通知しない
        var first = Pc("PC-01", "SN-A", "100000");
        var rerun = Pc("PC-01", "SN-A", "150000");

        var groups = _sut.Evaluate([first, rerun]);

        Assert.Empty(groups);
        Assert.False(first.HasDuplicatePcName);
        Assert.False(rerun.HasDuplicatePcName);
    }

    [Fact]
    public void Evaluate_PcNameCaseDiffers_TreatedAsSameName()
    {
        // Windows のホスト名は大文字小文字を区別しない
        var a = Pc("PC-01", "SN-A");
        var b = Pc("pc-01", "SN-B", "130000");

        var groups = _sut.Evaluate([a, b]);

        Assert.Single(groups);
        Assert.True(a.HasDuplicatePcName);
        Assert.True(b.HasDuplicatePcName);
    }

    [Fact]
    public void Evaluate_SerialNotationDiffers_TreatedAsSameMachine()
    {
        // フォルダ名由来 (記号をハイフン置換) と manifest 由来の表記ゆれ。同一端末なので重複ではない。
        var fromFolderName = Pc("PC-01", "VMware-56-4d-17-d5-59-28-27-c4-a9-1c", "100000");
        var fromManifest = Pc("PC-01", "VMware-56 4d 17 d5 59 28 27 c4-a9 1c", "150000");

        var groups = _sut.Evaluate([fromFolderName, fromManifest]);

        Assert.Empty(groups);
    }

    [Fact]
    public void Evaluate_BothSerialsUnknown_CountedAsSeparateMachines()
    {
        // シリアル不明は同一端末か判別できない → 見逃し防止のため別端末として扱う
        var a = Pc("PC-01", "", "100000");
        var b = Pc("PC-01", "", "150000");

        var group = Assert.Single(_sut.Evaluate([a, b]));

        Assert.Equal(2, group.MachineCount);
        Assert.True(a.HasDuplicatePcName);
        Assert.True(b.HasDuplicatePcName);
    }

    [Fact]
    public void Evaluate_UnknownPlaceholderSerial_TreatedAsUnknown()
    {
        // fabriq がシリアル取得不能時にフォルダ名へ入れる "UNKNOWN" 同士を同一端末とみなさない
        var a = Pc("PC-01", "UNKNOWN", "100000");
        var b = Pc("PC-01", "UNKNOWN", "150000");

        var group = Assert.Single(_sut.Evaluate([a, b]));

        Assert.Equal(2, group.MachineCount);
    }

    [Fact]
    public void Evaluate_RerunPlusAnotherMachine_AllMembersFlagged()
    {
        // 端末 A を 2 回実行 + 別端末 B に同じ名前 → 別端末 2 台、3 行すべてに印を付ける
        var aFirst = Pc("PC-01", "SN-A", "100000");
        var aRerun = Pc("PC-01", "SN-A", "110000");
        var b = Pc("PC-01", "SN-B", "120000");

        var group = Assert.Single(_sut.Evaluate([aFirst, aRerun, b]));

        Assert.Equal(2, group.MachineCount);
        Assert.Equal(3, group.Members.Count);
        Assert.All(new[] { aFirst, aRerun, b }, pc => Assert.Equal(2, pc.DuplicatePcNameMachineCount));
    }

    [Fact]
    public void Evaluate_AfterDuplicateRemoved_ResetsPreviousFlag()
    {
        // 片方のフォルダを取り除いて再評価したら、前回の印が残らないこと
        var a = Pc("PC-01", "SN-A");
        var b = Pc("PC-01", "SN-B", "130000");
        _sut.Evaluate([a, b]);
        Assert.True(a.HasDuplicatePcName);

        var groups = _sut.Evaluate([a]);

        Assert.Empty(groups);
        Assert.False(a.HasDuplicatePcName);
        Assert.Equal(0, a.DuplicatePcNameMachineCount);
    }

    [Fact]
    public void Evaluate_MultipleGroups_OrderedByPcName()
    {
        var pcs = new[]
        {
            Pc("PC-09", "SN-1", "100000"),
            Pc("PC-09", "SN-2", "110000"),
            Pc("PC-02", "SN-3", "120000"),
            Pc("PC-02", "SN-4", "130000"),
            Pc("PC-05", "SN-5", "140000"),
        };

        var groups = _sut.Evaluate(pcs);

        Assert.Equal(new[] { "PC-02", "PC-09" }, groups.Select(g => g.PcName));
        Assert.False(pcs[4].HasDuplicatePcName);
    }
}
