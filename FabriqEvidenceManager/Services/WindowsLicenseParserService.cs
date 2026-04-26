using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// 21_WindowsLicense.txt をパースする。
/// evidence_config.ps1 §21 の 3 ブロック構成に対応:
///   1. ---- SoftwareLicensingProduct (per-product) ----   各プロダクト 6〜9 行 (空行で区切り)
///   2. ---- SoftwareLicensingService (machine-wide) ----  6 フィールド
///   3. ---- slmgr /dlv (raw) ----                          残り全行を raw で保存
/// </summary>
public sealed partial class WindowsLicenseParserService : IWindowsLicenseParserService
{
    private static readonly IReadOnlyDictionary<int, string> LicenseStatusMap =
        new Dictionary<int, string>
        {
            [0] = "Unlicensed",
            [1] = "Licensed",
            [2] = "OOB Grace Period",
            [3] = "Out of Tolerance Grace",
            [4] = "Non-Genuine Grace",
            [5] = "Notification Mode",
            [6] = "Extended Grace Period",
        };

    [GeneratedRegex(@"^(.+?)\s*\((\d+)\)\s*$")]
    private static partial Regex LicenseStatusPattern();

    private enum BlockState { None, Product, Service, SlmgrRaw }

    public WindowsLicenseData Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);

        var products = new List<WindowsLicensingProduct>();
        ProductBuilder? current = null;
        var serviceFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var serviceFailed = false;
        var slmgrRaw = new StringBuilder();
        var state = BlockState.None;

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.TrimStart();

            if (trimmed.StartsWith("---- ", StringComparison.Ordinal))
            {
                if (current is not null) { products.Add(current.Build()); current = null; }

                if (trimmed.Contains("SoftwareLicensingProduct"))
                    state = BlockState.Product;
                else if (trimmed.Contains("SoftwareLicensingService"))
                    state = BlockState.Service;
                else if (trimmed.Contains("slmgr /dlv"))
                    state = BlockState.SlmgrRaw;
                else
                    state = BlockState.None;
                continue;
            }

            if (state == BlockState.SlmgrRaw)
            {
                slmgrRaw.AppendLine(rawLine);
                continue;
            }

            if (string.IsNullOrWhiteSpace(rawLine))
            {
                if (state == BlockState.Product && current is not null)
                {
                    products.Add(current.Build());
                    current = null;
                }
                continue;
            }

            var kv = SplitKeyValue(rawLine);
            if (kv is null) continue;
            var (key, value) = kv.Value;

            if (state == BlockState.Product)
            {
                if (key == "Name")
                {
                    if (current is not null) products.Add(current.Build());
                    current = new ProductBuilder { Name = value };
                }
                else if (current is not null)
                {
                    ApplyProductField(current, key, value);
                }
            }
            else if (state == BlockState.Service)
            {
                if (key.StartsWith("[WARN]", StringComparison.Ordinal))
                    serviceFailed = true;
                else
                    serviceFields[key] = value;
            }
        }

        if (current is not null) products.Add(current.Build());

        var service = (serviceFailed || serviceFields.Count == 0)
            ? null
            : new WindowsLicensingService
            {
                ClientMachineID = ServiceField(serviceFields, "ClientMachineID"),
                KeyManagementServiceMachine = ServiceField(serviceFields, "KeyManagementServiceMachine"),
                KeyManagementServicePort = ServiceField(serviceFields, "KeyManagementServicePort"),
                OA3xOriginalProductKey = ServiceField(serviceFields, "OA3xOriginalProductKey"),
                OA3xOriginalProductKeyDescription = ServiceField(serviceFields, "OA3xOriginalProductKeyDescription"),
                PolicyCacheRefreshRequired = ServiceField(serviceFields, "PolicyCacheRefreshRequired"),
            };

        return new WindowsLicenseData
        {
            Products = products,
            Service = service,
            SlmgrDlvRaw = slmgrRaw.ToString().TrimEnd('\r', '\n'),
        };
    }

    private static string ServiceField(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var v) ? v : string.Empty;

    private static (string key, string value)? SplitKeyValue(string line)
    {
        var idx = line.IndexOf(':');
        if (idx < 0) return null;
        return (line[..idx].Trim(), line[(idx + 1)..].Trim());
    }

    private static void ApplyProductField(ProductBuilder p, string key, string value)
    {
        switch (key)
        {
            case "Description": p.Description = value; break;
            case "LicenseStatus": ApplyLicenseStatus(p, value); break;
            case "PartialProductKey": p.PartialProductKey = value; break;
            case "LicenseFamily": p.LicenseFamily = value; break;
            case "ProductKeyChannel": p.ProductKeyChannel = value; break;
            case "GracePeriodRemaining":
                var sp = value.IndexOf(' ');
                var token = sp > 0 ? value[..sp] : value;
                if (int.TryParse(token, out var minutes)) p.GracePeriodMinutes = minutes;
                break;
            case "KMS (configured)": p.KmsConfigured = value; break;
            case "KMS (discovered)": p.KmsDiscovered = value; break;
        }
    }

    private static void ApplyLicenseStatus(ProductBuilder p, string value)
    {
        var m = LicenseStatusPattern().Match(value);
        if (m.Success && int.TryParse(m.Groups[2].Value, out var code))
        {
            p.LicenseStatusCode = code;
            p.LicenseStatusText = LicenseStatusMap.TryGetValue(code, out var text) ? text : "Unknown";
        }
        else
        {
            p.LicenseStatusText = value;
        }
    }

    private sealed class ProductBuilder
    {
        public string Name = string.Empty;
        public string Description = string.Empty;
        public int LicenseStatusCode;
        public string LicenseStatusText = string.Empty;
        public string PartialProductKey = string.Empty;
        public string LicenseFamily = string.Empty;
        public string ProductKeyChannel = string.Empty;
        public int? GracePeriodMinutes;
        public string? KmsConfigured;
        public string? KmsDiscovered;

        public WindowsLicensingProduct Build() => new()
        {
            Name = Name,
            Description = Description,
            LicenseStatusCode = LicenseStatusCode,
            LicenseStatusText = LicenseStatusText,
            PartialProductKey = PartialProductKey,
            LicenseFamily = LicenseFamily,
            ProductKeyChannel = ProductKeyChannel,
            GracePeriodMinutes = GracePeriodMinutes,
            KmsConfigured = KmsConfigured,
            KmsDiscovered = KmsDiscovered,
        };
    }
}
