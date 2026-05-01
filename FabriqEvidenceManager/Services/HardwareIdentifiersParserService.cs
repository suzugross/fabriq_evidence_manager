using System.Collections.Generic;
using System.IO;
using System.Text;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// 31_HardwareIdentifiers.txt をパースする。
/// evidence_config v1.6.0+ §31 (kernel 3.0.0+) の 4 ブロック構成:
///   1. ---- Win32_ComputerSystem ----        (機種ファミリ / メモリ / CPU 数)
///   2. ---- Win32_ComputerSystemProduct ---- (SN candidate / SMBIOS UUID)
///   3. ---- Win32_BaseBoard ----             (マザーボード SN; record-only)
///   4. ---- Win32_SystemEnclosure ----       (SN candidate / シャーシ種別 / Asset Tag)
///
/// 各 probe は §23 同様 inner try/catch で個別退避されるため、ブロックヘッダ直下が
/// "(Win32_X query failed: ...)" のような fallback marker の場合、そのブロックの
/// 集約は null で返す（PCの §31 自体は Success のことがある）。
/// </summary>
public sealed class HardwareIdentifiersParserService : IHardwareIdentifiersParserService
{
    private enum BlockState { None, ComputerSystem, ComputerSystemProduct, BaseBoard, SystemEnclosure }

    public HardwareIdentifiersData Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);

        var csFields  = new Dictionary<string, string>(System.StringComparer.Ordinal);
        var cspFields = new Dictionary<string, string>(System.StringComparer.Ordinal);
        var bbFields  = new Dictionary<string, string>(System.StringComparer.Ordinal);
        var seFields  = new Dictionary<string, string>(System.StringComparer.Ordinal);

        var csFailed = false;
        var cspFailed = false;
        var bbFailed = false;
        var seFailed = false;

        var state = BlockState.None;

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.TrimStart();

            if (trimmed.StartsWith("---- ", System.StringComparison.Ordinal))
            {
                // 前方一致衝突回避: "Win32_ComputerSystemProduct" を先に判定する。
                if (trimmed.Contains("Win32_ComputerSystemProduct"))
                    state = BlockState.ComputerSystemProduct;
                else if (trimmed.Contains("Win32_ComputerSystem"))
                    state = BlockState.ComputerSystem;
                else if (trimmed.Contains("Win32_BaseBoard"))
                    state = BlockState.BaseBoard;
                else if (trimmed.Contains("Win32_SystemEnclosure"))
                    state = BlockState.SystemEnclosure;
                else
                    state = BlockState.None;
                continue;
            }

            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            // fallback marker 検出: "(Win32_X query failed: ...)"
            // ":" を含むため SplitKeyValue で誤って key:value 化されないよう先に弾く。
            if (trimmed.StartsWith('('))
            {
                switch (state)
                {
                    case BlockState.ComputerSystem:        csFailed = true; break;
                    case BlockState.ComputerSystemProduct: cspFailed = true; break;
                    case BlockState.BaseBoard:             bbFailed = true; break;
                    case BlockState.SystemEnclosure:       seFailed = true; break;
                }
                continue;
            }

            var kv = SplitKeyValue(rawLine);
            if (kv is null) continue;
            var (key, value) = kv.Value;

            switch (state)
            {
                case BlockState.ComputerSystem:        csFields[key] = value; break;
                case BlockState.ComputerSystemProduct: cspFields[key] = value; break;
                case BlockState.BaseBoard:             bbFields[key] = value; break;
                case BlockState.SystemEnclosure:       seFields[key] = value; break;
            }
        }

        return new HardwareIdentifiersData
        {
            ComputerSystem        = csFailed  || csFields.Count == 0  ? null : BuildComputerSystem(csFields),
            ComputerSystemProduct = cspFailed || cspFields.Count == 0 ? null : BuildComputerSystemProduct(cspFields),
            BaseBoard             = bbFailed  || bbFields.Count == 0  ? null : BuildBaseBoard(bbFields),
            SystemEnclosure       = seFailed  || seFields.Count == 0  ? null : BuildSystemEnclosure(seFields),
        };
    }

    private static (string key, string value)? SplitKeyValue(string line)
    {
        var idx = line.IndexOf(':');
        if (idx < 0) return null;
        return (line[..idx].Trim(), line[(idx + 1)..].Trim());
    }

    private static ComputerSystemInfo BuildComputerSystem(Dictionary<string, string> f) => new()
    {
        Manufacturer              = f.GetValueOrDefault("Manufacturer", string.Empty),
        Model                     = f.GetValueOrDefault("Model", string.Empty),
        SystemFamily              = f.GetValueOrDefault("SystemFamily", string.Empty),
        SystemSKUNumber           = f.GetValueOrDefault("SystemSKUNumber", string.Empty),
        TotalPhysicalMem          = f.GetValueOrDefault("TotalPhysicalMem", string.Empty),
        NumberOfProcessors        = ParseIntNullable(f, "NumberOfProcessors"),
        NumberOfLogicalProcessors = ParseIntNullable(f, "NumberOfLogicalProcessors"),
    };

    private static ComputerSystemProductInfo BuildComputerSystemProduct(Dictionary<string, string> f) => new()
    {
        Vendor            = f.GetValueOrDefault("Vendor", string.Empty),
        Name              = f.GetValueOrDefault("Name", string.Empty),
        Version           = f.GetValueOrDefault("Version", string.Empty),
        IdentifyingNumber = f.GetValueOrDefault("IdentifyingNumber", string.Empty),
        Uuid              = f.GetValueOrDefault("UUID", string.Empty),
        SkuNumber         = f.GetValueOrDefault("SKUNumber", string.Empty),
    };

    private static BaseBoardInfo BuildBaseBoard(Dictionary<string, string> f) => new()
    {
        Manufacturer = f.GetValueOrDefault("Manufacturer", string.Empty),
        Product      = f.GetValueOrDefault("Product", string.Empty),
        Version      = f.GetValueOrDefault("Version", string.Empty),
        SerialNumber = f.GetValueOrDefault("SerialNumber", string.Empty),
        Tag          = f.GetValueOrDefault("Tag", string.Empty),
    };

    private static SystemEnclosureInfo BuildSystemEnclosure(Dictionary<string, string> f) => new()
    {
        Manufacturer    = f.GetValueOrDefault("Manufacturer", string.Empty),
        Model           = f.GetValueOrDefault("Model", string.Empty),
        ChassisTypes    = f.GetValueOrDefault("ChassisTypes", string.Empty),
        SerialNumber    = f.GetValueOrDefault("SerialNumber", string.Empty),
        SmbiosAssetTag  = f.GetValueOrDefault("SMBIOSAssetTag", string.Empty),
        AssetTag        = f.GetValueOrDefault("AssetTag", string.Empty),
        SecurityStatus  = ParseIntNullable(f, "SecurityStatus"),
        LockPresent     = ParseBoolNullable(f, "LockPresent"),
    };

    private static bool? ParseBoolNullable(Dictionary<string, string> f, string key)
        => f.TryGetValue(key, out var v) && bool.TryParse(v, out var parsed) ? parsed : null;

    private static int? ParseIntNullable(Dictionary<string, string> f, string key)
        => f.TryGetValue(key, out var v) && int.TryParse(v, out var parsed) ? parsed : null;
}
