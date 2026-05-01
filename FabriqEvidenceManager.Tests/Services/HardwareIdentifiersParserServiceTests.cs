using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

public class HardwareIdentifiersParserServiceTests
{
    private readonly HardwareIdentifiersParserService _sut = new();

    [Fact]
    public void Parse_FullModernPc_ExtractsAllFourBlocks()
    {
        // 実フィクスチャ (ASUS EXPERTBOOK B1402CVA / 4 ブロック全成功)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("HardwareIdentifiers"), "full_modern_pc.txt");

        var result = _sut.Parse(path);

        // ---- Win32_ComputerSystem ----
        Assert.NotNull(result.ComputerSystem);
        Assert.Equal("ASUSTeK COMPUTER INC.", result.ComputerSystem!.Manufacturer);
        Assert.Equal("ASUS EXPERTBOOK B1402CVA_B1402CVA", result.ComputerSystem.Model);
        Assert.Equal("ASUS EXPERTBOOK", result.ComputerSystem.SystemFamily);
        Assert.Equal(string.Empty, result.ComputerSystem.SystemSKUNumber);
        Assert.Equal("15.63 GB", result.ComputerSystem.TotalPhysicalMem);
        Assert.Equal(1, result.ComputerSystem.NumberOfProcessors);
        Assert.Equal(12, result.ComputerSystem.NumberOfLogicalProcessors);

        // ---- Win32_ComputerSystemProduct ----
        Assert.NotNull(result.ComputerSystemProduct);
        Assert.Equal("ASUSTeK COMPUTER INC.", result.ComputerSystemProduct!.Vendor);
        Assert.Equal("ASUS EXPERTBOOK B1402CVA_B1402CVA", result.ComputerSystemProduct.Name);
        Assert.Equal("1.0", result.ComputerSystemProduct.Version);
        Assert.Equal("T2NXCV06Y22208C", result.ComputerSystemProduct.IdentifyingNumber);
        Assert.Equal("99ACFEAA-AB7C-8C4D-8E4D-35A66EAEFE05", result.ComputerSystemProduct.Uuid);
        Assert.Equal(string.Empty, result.ComputerSystemProduct.SkuNumber);

        // ---- Win32_BaseBoard ----
        Assert.NotNull(result.BaseBoard);
        Assert.Equal("ASUSTeK COMPUTER INC.", result.BaseBoard!.Manufacturer);
        Assert.Equal("B1402CVA", result.BaseBoard.Product);
        Assert.Equal("1.0", result.BaseBoard.Version);
        Assert.Equal("T206NXCV001M52MB", result.BaseBoard.SerialNumber);
        Assert.Equal("Base Board", result.BaseBoard.Tag);

        // ---- Win32_SystemEnclosure ----
        Assert.NotNull(result.SystemEnclosure);
        Assert.Equal("ASUSTeK COMPUTER INC.", result.SystemEnclosure!.Manufacturer);
        Assert.Equal(string.Empty, result.SystemEnclosure.Model);
        Assert.Equal("10 (Notebook)", result.SystemEnclosure.ChassisTypes);
        Assert.Equal("T2NXCV06Y22208C", result.SystemEnclosure.SerialNumber);
        Assert.Equal("No Asset Tag", result.SystemEnclosure.SmbiosAssetTag);
        Assert.Equal(string.Empty, result.SystemEnclosure.AssetTag);
        Assert.Equal(3, result.SystemEnclosure.SecurityStatus);
        Assert.False(result.SystemEnclosure.LockPresent);
    }

    [Fact]
    public void Parse_FullModernPc_SnCandidatesMatchAcrossBlocks()
    {
        // §10 SerialNumber の canonical (Win32_BIOS.SerialNumber) と
        // §31 が独立に保持する SN candidate (CSProduct.IdentifyingNumber, SystemEnclosure.SerialNumber)
        // が一致することを保証する (audit クロスチェック前提)。
        // BaseBoard.SerialNumber は record-only であり一致を保証しない。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("HardwareIdentifiers"), "full_modern_pc.txt");

        var result = _sut.Parse(path);

        Assert.Equal(result.ComputerSystemProduct!.IdentifyingNumber, result.SystemEnclosure!.SerialNumber);
    }

    [Fact]
    public void Parse_PartialFailures_FailedBlocksAreNullSucceededBlocksParsed()
    {
        // §31 の inner try/catch 退避動作: 一部 block の "(Win32_X query failed: ...)" 行で
        // その block 集約は null になり、他 block は正常パースされる。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("HardwareIdentifiers"), "partial_failures.txt");

        var result = _sut.Parse(path);

        Assert.Null(result.ComputerSystem);
        Assert.Null(result.BaseBoard);

        Assert.NotNull(result.ComputerSystemProduct);
        Assert.Equal("TESTSN-00001", result.ComputerSystemProduct!.IdentifyingNumber);
        Assert.Equal("11111111-2222-3333-4444-555555555555", result.ComputerSystemProduct.Uuid);

        Assert.NotNull(result.SystemEnclosure);
        Assert.Equal("3 (Desktop)", result.SystemEnclosure!.ChassisTypes);
        Assert.Equal("TESTSN-00001", result.SystemEnclosure.SerialNumber);
        Assert.Equal(3, result.SystemEnclosure.SecurityStatus);
        Assert.False(result.SystemEnclosure.LockPresent);
    }

    [Fact]
    public void Parse_PartialFailures_BlockHeaderPrefixCollisionResolved()
    {
        // "Win32_ComputerSystem" は "Win32_ComputerSystemProduct" の prefix。
        // ヘッダ判定でこれを取り違えると CSProduct のフィールドが ComputerSystem 側に
        // 流入してしまう。partial_failures fixture では ComputerSystem ブロック自身が
        // failed のため、ComputerSystem は必ず null である必要がある。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("HardwareIdentifiers"), "partial_failures.txt");

        var result = _sut.Parse(path);

        Assert.Null(result.ComputerSystem);
        Assert.NotNull(result.ComputerSystemProduct);
    }
}
