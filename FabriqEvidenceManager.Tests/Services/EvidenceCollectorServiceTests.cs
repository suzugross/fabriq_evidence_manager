using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FabriqEvidenceManager.Helpers;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

/// <summary>
/// 納品データの PC 別フォルダ名を検証する。
/// 同じ計画 PC 名の PC が同じフォルダに上書き混在しないこと (回帰防止)。
/// </summary>
public class EvidenceCollectorServiceTests : IDisposable
{
    private readonly EvidenceCollectorService _sut = new();
    private readonly string _tempDir;
    private readonly string _outputDir;

    public EvidenceCollectorServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FabriqEvidenceManager.Tests", Guid.NewGuid().ToString("N"));
        _outputDir = Path.Combine(_tempDir, "out");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* test cleanup best-effort */ }
    }

    /// <summary>pc_information に識別用ファイルを 1 つ置いた PC を作る。</summary>
    private PcEvidence MakePc(string pcName, string serial, string collectionDate, string markerFileName)
    {
        var pcInfoDir = Path.Combine(_tempDir, "src", Guid.NewGuid().ToString("N"), EvidenceConstants.DirPcInformation);
        Directory.CreateDirectory(pcInfoDir);
        File.WriteAllText(Path.Combine(pcInfoDir, markerFileName), markerFileName);

        return new PcEvidence
        {
            PcName = pcName,
            SerialNumber = serial,
            CollectionDate = collectionDate,
            PcInformationDirectoryPath = pcInfoDir,
        };
    }

    private string[] OutputFolderNames() =>
        Directory.GetDirectories(_outputDir).Select(d => Path.GetFileName(d)!).OrderBy(n => n).ToArray();

    [Fact]
    public async Task CollectAsync_WithSerial_UsesNameSerialDate()
    {
        var pc = MakePc("PC-01", "SN-A", "2026_10_23_100000", "a.txt");

        await _sut.CollectAsync([pc], _outputDir);

        Assert.Equal(new[] { "PC-01_SN-A_2026_10_23_100000" }, OutputFolderNames());
    }

    [Fact]
    public async Task CollectAsync_SameNameUnknownSerial_SeparateFolders()
    {
        // 以前はシリアル不明時のフォルダ名が "PC-01" のみで、2 台分のファイルが同じフォルダに混在していた
        var a = MakePc("PC-01", "", "2026_10_23_100000", "from_a.txt");
        var b = MakePc("PC-01", "", "2026_10_23_110000", "from_b.txt");

        await _sut.CollectAsync([a, b], _outputDir);

        Assert.Equal(new[] { "PC-01_2026_10_23_100000", "PC-01_2026_10_23_110000" }, OutputFolderNames());
        AssertOnlyMarker("PC-01_2026_10_23_100000", "from_a.txt");
        AssertOnlyMarker("PC-01_2026_10_23_110000", "from_b.txt");
    }

    [Fact]
    public async Task CollectAsync_IdenticalFolderName_AppendsCounter()
    {
        // 名前・シリアル・日時がすべて同じでも上書きせず連番で分ける (最終防衛線)
        var a = MakePc("PC-01", "SN-A", "2026_10_23_100000", "from_a.txt");
        var b = MakePc("PC-01", "SN-A", "2026_10_23_100000", "from_b.txt");

        await _sut.CollectAsync([a, b], _outputDir);

        Assert.Equal(new[] { "PC-01_SN-A_2026_10_23_100000", "PC-01_SN-A_2026_10_23_100000_2" }, OutputFolderNames());
        AssertOnlyMarker("PC-01_SN-A_2026_10_23_100000", "from_a.txt");
        AssertOnlyMarker("PC-01_SN-A_2026_10_23_100000_2", "from_b.txt");
    }

    private void AssertOnlyMarker(string pcFolderName, string expectedMarker)
    {
        var files = Directory.GetFiles(Path.Combine(_outputDir, pcFolderName, EvidenceConstants.DirPcInformation))
            .Select(Path.GetFileName)
            .ToArray();
        Assert.Equal(new[] { expectedMarker }, files);
    }
}
