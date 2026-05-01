using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

public class ManifestReaderServiceTests
{
    private readonly ManifestReaderService _sut = new();

    [Fact]
    public void Read_ValidV1Manifest_ParsesAllRequiredFields()
    {
        var dir = TestPaths.GetTestDataDirectory("Manifests", "valid_v1");

        var manifest = _sut.Read(dir);

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("fabriq-evidence-manifest", manifest.ManifestType);
        Assert.Equal("1.3.0", manifest.EvidenceConfigVersion);
        Assert.Equal("2.2.2", manifest.FabriqKernelVersion);
        Assert.Equal("TEST-PC-01", manifest.ComputerName);
        Assert.Equal("TEST-SERIAL-X1", manifest.HardwareUniqueId);
        Assert.Equal("TEST-PC-01", manifest.SelectedNewPcName);
        Assert.Equal("test_user", manifest.WorkerName);
        Assert.Equal(2, manifest.Sections.Count);
        Assert.Equal(2, manifest.Summary.SectionCount);
        Assert.Equal(1, manifest.Summary.SuccessCount);
        Assert.Equal(1, manifest.Summary.SkippedCount);
    }

    [Fact]
    public void Read_ValidV1Manifest_ParsesSectionStatusEnum()
    {
        var dir = TestPaths.GetTestDataDirectory("Manifests", "valid_v1");

        var manifest = _sut.Read(dir);

        var section01 = manifest.Sections.First(s => s.Id == "01");
        Assert.Equal(SectionStatus.Success, section01.Status);
        Assert.Null(section01.Reason);    // 空文字列は null に正規化される

        var section14 = manifest.Sections.First(s => s.Id == "14");
        Assert.Equal(SectionStatus.Skipped, section14.Status);
        Assert.Equal("Client OS detected (Server-only section)", section14.Reason);
    }

    [Fact]
    public void Read_UnsupportedSchemaVersion2_ThrowsUnsupportedSchemaException()
    {
        var dir = TestPaths.GetTestDataDirectory("Manifests", "unsupported_schema_v2");

        var ex = Assert.Throws<UnsupportedManifestSchemaException>(() => _sut.Read(dir));

        Assert.Equal(2, ex.DetectedSchemaVersion);
    }

    [Fact]
    public void Read_NonExistentDirectory_ThrowsManifestNotFoundException()
    {
        var dir = TestPaths.GetTestDataDirectory("Manifests", "does_not_exist");

        var ex = Assert.Throws<ManifestNotFoundException>(() => _sut.Read(dir));

        Assert.Contains("manifest.json", ex.Path);
    }

    [Fact]
    public void Read_InvalidJson_ThrowsManifestParseException()
    {
        var dir = TestPaths.GetTestDataDirectory("Manifests", "invalid_json");

        Assert.Throws<ManifestParseException>(() => _sut.Read(dir));
    }

    [Fact]
    public void Read_UnknownStatusEnum_DefaultsToFailed()
    {
        // EVIDENCE_MANIFEST.md §4.1.3: 未知 status 値は Failed 扱いで安全側に倒す
        var dir = TestPaths.GetTestDataDirectory("Manifests", "unknown_status");

        var manifest = _sut.Read(dir);

        Assert.Single(manifest.Sections);
        Assert.Equal(SectionStatus.Failed, manifest.Sections[0].Status);
    }

    [Fact]
    public void Read_V1_6_0Manifest_ParsesAllSectionsIncludingNewIds()
    {
        // evidence_config v1.6.0 / kernel 3.0.0 で追加された §24,27,28,29,30,31 を含む
        // 32 セクション manifest が schemaVersion=1 互換で読めることを保証する。
        var dir = TestPaths.GetTestDataDirectory("Manifests", "valid_v1_6");

        var manifest = _sut.Read(dir);

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("1.6.0", manifest.EvidenceConfigVersion);
        Assert.Equal("3.0.0", manifest.FabriqKernelVersion);
        Assert.Equal(32, manifest.Summary.SectionCount);
        Assert.Equal(31, manifest.Summary.SuccessCount);
        Assert.Equal(1, manifest.Summary.SkippedCount);

        foreach (var id in new[] { "24", "27", "28", "29", "30", "31" })
        {
            var section = manifest.Sections.FirstOrDefault(s => s.Id == id);
            Assert.NotNull(section);
            Assert.Equal(SectionStatus.Success, section!.Status);
            Assert.NotEmpty(section.RegularFiles);
        }
    }
}
