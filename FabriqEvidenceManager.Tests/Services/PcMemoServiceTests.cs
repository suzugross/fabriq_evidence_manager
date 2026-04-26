using System;
using System.IO;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

public class PcMemoServiceTests : IDisposable
{
    private readonly PcMemoService _sut = new();
    private readonly string _tempDir;

    public PcMemoServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FabriqEvidenceManager.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* test cleanup best-effort */ }
    }

    [Fact]
    public void Load_NoFile_ReturnsNull()
    {
        var memo = _sut.Load(_tempDir);

        Assert.Null(memo);
    }

    [Fact]
    public void Save_CreatesFileAndCanBeReadBack()
    {
        var memo = new PcMemo
        {
            Text = "代替機。S/N が hostlist と一致しないが顧客承認済み (TICKET-1234)。\n再キッティング不要。",
            LastUpdatedAt = new DateTimeOffset(2026, 4, 26, 15, 30, 0, TimeSpan.FromHours(9)),
            LastUpdatedBy = "test_admin",
        };

        _sut.Save(_tempDir, memo);

        var path = Path.Combine(_tempDir, PcMemoService.MemoFileName);
        Assert.True(File.Exists(path));

        var loaded = _sut.Load(_tempDir);
        Assert.NotNull(loaded);
        Assert.Equal(memo.Text, loaded!.Text);
        Assert.Equal(memo.LastUpdatedAt, loaded.LastUpdatedAt);
        Assert.Equal(memo.LastUpdatedBy, loaded.LastUpdatedBy);
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        var first = new PcMemo { Text = "first", LastUpdatedBy = "a" };
        var second = new PcMemo { Text = "second", LastUpdatedBy = "b" };

        _sut.Save(_tempDir, first);
        _sut.Save(_tempDir, second);

        var loaded = _sut.Load(_tempDir);
        Assert.NotNull(loaded);
        Assert.Equal("second", loaded!.Text);
        Assert.Equal("b", loaded.LastUpdatedBy);
    }

    [Fact]
    public void Delete_RemovesFile()
    {
        _sut.Save(_tempDir, new PcMemo { Text = "to be deleted" });
        Assert.True(File.Exists(Path.Combine(_tempDir, PcMemoService.MemoFileName)));

        _sut.Delete(_tempDir);

        Assert.False(File.Exists(Path.Combine(_tempDir, PcMemoService.MemoFileName)));
        Assert.Null(_sut.Load(_tempDir));
    }
}
