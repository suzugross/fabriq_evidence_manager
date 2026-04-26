using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

public class WindowsLicenseParserServiceTests
{
    private readonly WindowsLicenseParserService _sut = new();

    [Fact]
    public void Parse_KmsVolumeClient_ExtractsProductFields()
    {
        var path = Path.Combine(TestPaths.GetTestDataDirectory("Licenses"), "windows_license_kms_volume.txt");

        var result = _sut.Parse(path);

        Assert.Single(result.Products);
        var product = result.Products[0];
        Assert.Equal("Windows(R), Professional edition", product.Name);
        Assert.Equal("Professional", product.LicenseFamily);
        Assert.Equal("Volume:GVLK", product.ProductKeyChannel);
        Assert.Equal("T83GX", product.PartialProductKey);
        Assert.Equal(5, product.LicenseStatusCode);
        Assert.Equal("Notification Mode", product.LicenseStatusText);
    }

    [Fact]
    public void Parse_KmsVolumeClient_ExtractsServiceFields()
    {
        var path = Path.Combine(TestPaths.GetTestDataDirectory("Licenses"), "windows_license_kms_volume.txt");

        var result = _sut.Parse(path);

        Assert.NotNull(result.Service);
        Assert.Equal("ea22380e-56e8-4fb3-93fd-5224be9ce264", result.Service!.ClientMachineID);
        Assert.Equal("1688", result.Service.KeyManagementServicePort);
    }

    [Fact]
    public void Parse_NoInstalledProduct_ReturnsEmptyProductList()
    {
        var path = Path.Combine(TestPaths.GetTestDataDirectory("Licenses"), "windows_license_no_product.txt");

        var result = _sut.Parse(path);

        Assert.Empty(result.Products);
        Assert.NotNull(result.Service);   // Service ブロックは正常パース
    }

    [Fact]
    public void Parse_UnknownStatusCode_FallsBackToUnknownText()
    {
        // LicenseStatusMap に無い code (99) は LicenseStatusText="Unknown" にフォールバック
        var path = Path.Combine(TestPaths.GetTestDataDirectory("Licenses"), "windows_license_unknown_status.txt");

        var result = _sut.Parse(path);

        Assert.Single(result.Products);
        var product = result.Products[0];
        Assert.Equal(99, product.LicenseStatusCode);
        Assert.Equal("Unknown", product.LicenseStatusText);
    }
}
