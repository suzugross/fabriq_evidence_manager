using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

public class SecurityBaselineParserServiceTests
{
    private readonly SecurityBaselineParserService _sut = new();

    [Fact]
    public void Parse_FullModernPc_ExtractsAllFiveBlocks()
    {
        // 実フィクスチャ (ASUSTeK / TPM Ready / SecureBoot=False / VBS Running / HVCI / LSA OnWithUefiLock)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("SecurityBaseline"), "full_modern_pc.txt");

        var result = _sut.Parse(path);

        // ---- TPM ----
        Assert.NotNull(result.Tpm);
        Assert.True(result.Tpm!.Present);
        Assert.True(result.Tpm.Ready);
        Assert.True(result.Tpm.Enabled);
        Assert.Equal("1398033696", result.Tpm.ManufacturerId);
        Assert.Equal("(absent)", result.Tpm.OwnerAuth);
        Assert.Equal(0, result.Tpm.LockoutCount);
        Assert.Equal(31, result.Tpm.LockoutMax);

        // ---- Secure Boot ----
        Assert.False(result.SecureBootEnabled);

        // ---- VBS ----
        Assert.NotNull(result.Vbs);
        Assert.Equal("Running", result.Vbs!.VbsStatusText);
        Assert.Equal(2, result.Vbs.VbsStatusCode);
        Assert.True(result.Vbs.IsVbsRunning);
        Assert.Single(result.Vbs.SecurityServicesRunning);
        Assert.Equal("HVCI", result.Vbs.SecurityServicesRunning[0]);
        Assert.True(result.Vbs.HvciRunning);
        Assert.False(result.Vbs.CredentialGuardRunning);
        Assert.Equal("Enforced", result.Vbs.CodeIntegrityEnforcementText);
        Assert.Equal(2, result.Vbs.CodeIntegrityEnforcementCode);
        Assert.Equal(new[] { 1, 3, 4, 5, 7, 8 }, result.Vbs.AvailableSecurityProperties);

        // ---- LSA ----
        Assert.NotNull(result.Lsa);
        Assert.Equal(LsaRunAsPplState.OnWithUefiLock, result.Lsa!.RunAsPpl);
        Assert.Equal("On + UEFI Lock (PPL)", result.Lsa.RunAsPplRawText);
        Assert.Equal(2, result.Lsa.RunAsPplBoot);

        // ---- BIOS ----
        Assert.NotNull(result.Bios);
        Assert.Contains("ASUSTeK", result.Bios!.Manufacturer);
        Assert.Equal("B1402CVA.313", result.Bios.SmbiosBiosVersion);
        Assert.Contains("02/06/2025", result.Bios.ReleaseDate);
        Assert.Equal(3, result.Bios.SystemBiosMajor);
        Assert.Equal(13, result.Bios.SystemBiosMinor);
    }

    [Fact]
    public void Parse_PartialFailures_FallbackBlocksReturnNull()
    {
        var path = Path.Combine(TestPaths.GetTestDataDirectory("SecurityBaseline"), "partial_failures.txt");

        var result = _sut.Parse(path);

        // fallback marker により null になるブロック
        Assert.Null(result.Tpm);
        Assert.Null(result.SecureBootEnabled);
        Assert.Null(result.Bios);

        // VBS / LSA は値が入る (非 fallback)
        Assert.NotNull(result.Vbs);
        Assert.Equal("Not running", result.Vbs!.VbsStatusText);
        Assert.Equal(0, result.Vbs.VbsStatusCode);
        Assert.False(result.Vbs.IsVbsRunning);
        Assert.False(result.Vbs.HvciRunning);
        Assert.Empty(result.Vbs.SecurityServicesRunning);
        Assert.Empty(result.Vbs.AvailableSecurityProperties);
        Assert.Equal("Off", result.Vbs.CodeIntegrityEnforcementText);

        Assert.NotNull(result.Lsa);
        Assert.Equal(LsaRunAsPplState.Off, result.Lsa!.RunAsPpl);
        Assert.Equal("Off (registry value absent)", result.Lsa.RunAsPplRawText);
    }

    [Theory]
    [InlineData("Off", LsaRunAsPplState.Off)]
    [InlineData("Off (registry value absent)", LsaRunAsPplState.Off)]
    [InlineData("On (PPL)", LsaRunAsPplState.On)]
    [InlineData("On + UEFI Lock (PPL)", LsaRunAsPplState.OnWithUefiLock)]
    [InlineData("Unknown (5)", LsaRunAsPplState.Unknown)]
    [InlineData("", LsaRunAsPplState.Unknown)]
    public void MapLsaRunAsPpl_HandlesAllKnownTexts(string raw, LsaRunAsPplState expected)
    {
        // RunAsPPL テキストの 5 種類とエッジケースを 1 件のフィクスチャで検証する代わりに
        // public な MapLsaRunAsPpl は private のため、最小フィクスチャを動的合成して回す。
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, $"---- LSA Protection (RunAsPPL) ----\nRunAsPPL:             {raw}\n");
            var result = _sut.Parse(tmp);
            Assert.NotNull(result.Lsa);
            Assert.Equal(expected, result.Lsa!.RunAsPpl);
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
