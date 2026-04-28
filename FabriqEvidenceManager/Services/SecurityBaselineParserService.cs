using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// 23_SecurityBaseline.txt をパースする。
/// evidence_config.ps1 §23 (v1.4.0+) の 5 ブロック構成:
///   1. ---- TPM ----                                               (Get-Tpm)
///   2. ---- Secure Boot ----                                       (Confirm-SecureBootUEFI)
///   3. ---- Virtualization-Based Security (Win32_DeviceGuard) ---- (Win32_DeviceGuard)
///   4. ---- LSA Protection (RunAsPPL) ----                         (HKLM Registry)
///   5. ---- BIOS / Firmware ----                                   (Win32_BIOS)
///
/// 各 probe は inner try/catch で個別退避されるため、ブロックヘッダ直下が
/// "(Get-Tpm not available: ...)" のような fallback marker の場合、その
/// ブロックの集約は null で返す（PCの §23 自体は Success のことがある）。
/// </summary>
public sealed partial class SecurityBaselineParserService : ISecurityBaselineParserService
{
    [GeneratedRegex(@"^(.+?)\s*\((-?\d+)\)\s*$")]
    private static partial Regex TextWithCodePattern();

    private enum BlockState { None, Tpm, SecureBoot, Vbs, Lsa, Bios }

    public SecurityBaselineData Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);

        // 各ブロックの状態を fields マップ + fallback フラグで保持する。
        var tpmFields = new Dictionary<string, string>(StringComparer.Ordinal);
        var sbFields  = new Dictionary<string, string>(StringComparer.Ordinal);
        var vbsFields = new Dictionary<string, string>(StringComparer.Ordinal);
        var lsaFields = new Dictionary<string, string>(StringComparer.Ordinal);
        var biosFields = new Dictionary<string, string>(StringComparer.Ordinal);

        var tpmFailed = false;
        var sbFailed = false;
        var vbsFailed = false;
        var lsaFailed = false;
        var biosFailed = false;

        var state = BlockState.None;

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.TrimStart();

            if (trimmed.StartsWith("---- ", StringComparison.Ordinal))
            {
                if (trimmed.Contains("Virtualization-Based Security"))
                    state = BlockState.Vbs;
                else if (trimmed.Contains("LSA Protection"))
                    state = BlockState.Lsa;
                else if (trimmed.Contains("BIOS"))
                    state = BlockState.Bios;
                else if (trimmed.Contains("Secure Boot"))
                    state = BlockState.SecureBoot;
                else if (trimmed.Contains("TPM"))
                    state = BlockState.Tpm;
                else
                    state = BlockState.None;
                continue;
            }

            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            // fallback marker 検出 ("(Get-Tpm not available: ...)" / "(Win32_BIOS query failed: ...)" 等)
            // ":" を含むため SplitKeyValue で誤って key:value 化されないよう先に弾く。
            if (trimmed.StartsWith('('))
            {
                switch (state)
                {
                    case BlockState.Tpm:        tpmFailed = true; break;
                    case BlockState.SecureBoot: sbFailed = true; break;
                    case BlockState.Vbs:        vbsFailed = true; break;
                    case BlockState.Lsa:        lsaFailed = true; break;
                    case BlockState.Bios:       biosFailed = true; break;
                }
                continue;
            }

            var kv = SplitKeyValue(rawLine);
            if (kv is null) continue;
            var (key, value) = kv.Value;

            switch (state)
            {
                case BlockState.Tpm:        tpmFields[key] = value; break;
                case BlockState.SecureBoot: sbFields[key] = value; break;
                case BlockState.Vbs:        vbsFields[key] = value; break;
                case BlockState.Lsa:        lsaFields[key] = value; break;
                case BlockState.Bios:       biosFields[key] = value; break;
            }
        }

        return new SecurityBaselineData
        {
            Tpm = tpmFailed || tpmFields.Count == 0 ? null : BuildTpm(tpmFields),
            SecureBootEnabled = sbFailed ? null : ParseSecureBoot(sbFields),
            Vbs = vbsFailed || vbsFields.Count == 0 ? null : BuildVbs(vbsFields),
            Lsa = lsaFailed || lsaFields.Count == 0 ? null : BuildLsa(lsaFields),
            Bios = biosFailed || biosFields.Count == 0 ? null : BuildBios(biosFields),
        };
    }

    private static (string key, string value)? SplitKeyValue(string line)
    {
        var idx = line.IndexOf(':');
        if (idx < 0) return null;
        return (line[..idx].Trim(), line[(idx + 1)..].Trim());
    }

    private static TpmInfo BuildTpm(Dictionary<string, string> f) => new()
    {
        Present     = ParseBoolNullable(f, "TpmPresent"),
        Ready       = ParseBoolNullable(f, "TpmReady"),
        Enabled     = ParseBoolNullable(f, "TpmEnabled"),
        Activated   = ParseBoolNullable(f, "TpmActivated"),
        Owned       = ParseBoolNullable(f, "TpmOwned"),
        ManufacturerId      = f.GetValueOrDefault("ManufacturerId", string.Empty),
        ManufacturerVersion = f.GetValueOrDefault("ManufacturerVersion", string.Empty).Trim(),
        ManagedAuthLevel    = f.GetValueOrDefault("ManagedAuthLevel", string.Empty),
        OwnerAuth           = f.GetValueOrDefault("OwnerAuth", string.Empty),
        LockedOut    = ParseBoolNullable(f, "LockedOut"),
        LockoutCount = ParseIntNullable(f, "LockoutCount"),
        LockoutMax   = ParseIntNullable(f, "LockoutMax"),
    };

    private static bool? ParseSecureBoot(Dictionary<string, string> f)
    {
        if (f.TryGetValue("SecureBootEnabled", out var v) && bool.TryParse(v, out var parsed))
            return parsed;
        return null;
    }

    private static VbsInfo BuildVbs(Dictionary<string, string> f)
    {
        var (vbsText, vbsCode) = ParseTextWithCode(f.GetValueOrDefault("VirtualizationBasedSecurityStatus", string.Empty));
        var (ciText, ciCode)   = ParseTextWithCode(f.GetValueOrDefault("CodeIntegrityPolicyEnforcementStatus", string.Empty));
        var (umciText, umciCode) = ParseTextWithCode(f.GetValueOrDefault("UsermodeCodeIntegrityPolicyEnforcementStatus", string.Empty));

        return new VbsInfo
        {
            VbsStatusText = vbsText,
            VbsStatusCode = vbsCode,
            SecurityServicesRunning = ParseStringList(f.GetValueOrDefault("SecurityServicesRunning", string.Empty)),
            SecurityServicesConfigured = ParseStringList(f.GetValueOrDefault("SecurityServicesConfigured", string.Empty)),
            CodeIntegrityEnforcementText = ciText,
            CodeIntegrityEnforcementCode = ciCode,
            UsermodeCodeIntegrityEnforcementText = umciText,
            UsermodeCodeIntegrityEnforcementCode = umciCode,
            AvailableSecurityProperties = ParseIntList(f.GetValueOrDefault("AvailableSecurityProperties", string.Empty)),
            RequiredSecurityProperties = ParseIntList(f.GetValueOrDefault("RequiredSecurityProperties", string.Empty)),
        };
    }

    private static LsaProtectionInfo BuildLsa(Dictionary<string, string> f)
    {
        var raw = f.GetValueOrDefault("RunAsPPL", string.Empty);
        return new LsaProtectionInfo
        {
            RunAsPpl = MapLsaRunAsPpl(raw),
            RunAsPplRawText = raw,
            RunAsPplBoot = ParseIntNullable(f, "RunAsPPLBoot"),
        };
    }

    /// <summary>
    /// evidence_config §23d が出力する 5 種類の RunAsPPL テキストを enum にマップする。
    ///   "Off"                          → Off
    ///   "Off (registry value absent)"  → Off
    ///   "On (PPL)"                     → On
    ///   "On + UEFI Lock (PPL)"         → OnWithUefiLock
    ///   "Unknown (...)" / 不明         → Unknown
    /// </summary>
    private static LsaRunAsPplState MapLsaRunAsPpl(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return LsaRunAsPplState.Unknown;

        // UEFI Lock 判定を Off/On 判定より先に
        if (raw.Contains("UEFI Lock", StringComparison.OrdinalIgnoreCase))
            return LsaRunAsPplState.OnWithUefiLock;
        if (raw.StartsWith("Off", StringComparison.OrdinalIgnoreCase))
            return LsaRunAsPplState.Off;
        if (raw.StartsWith("On", StringComparison.OrdinalIgnoreCase))
            return LsaRunAsPplState.On;

        return LsaRunAsPplState.Unknown;
    }

    private static BiosInfo BuildBios(Dictionary<string, string> f) => new()
    {
        Manufacturer       = f.GetValueOrDefault("Manufacturer", string.Empty),
        SmbiosBiosVersion  = f.GetValueOrDefault("SMBIOSBIOSVersion", string.Empty),
        ReleaseDate        = f.GetValueOrDefault("ReleaseDate", string.Empty),
        BiosVersion        = f.GetValueOrDefault("BIOSVersion", string.Empty),
        SystemBiosMajor    = ParseIntNullable(f, "SystemBIOSMajor"),
        SystemBiosMinor    = ParseIntNullable(f, "SystemBIOSMinor"),
        SmbiosMajor        = ParseIntNullable(f, "SMBIOSMajor"),
        SmbiosMinor        = ParseIntNullable(f, "SMBIOSMinor"),
    };

    /// <summary>"Running (2)" / "Off (0)" 等を (text, code) に分解する。code が無ければ null。</summary>
    private static (string text, int? code) ParseTextWithCode(string value)
    {
        if (string.IsNullOrEmpty(value)) return (string.Empty, null);
        var m = TextWithCodePattern().Match(value);
        if (m.Success && int.TryParse(m.Groups[2].Value, out var code))
            return (m.Groups[1].Value.Trim(), code);
        return (value.Trim(), null);
    }

    /// <summary>"HVCI" / "Credential Guard, HVCI" / "" を string list に分解する。</summary>
    private static IReadOnlyList<string> ParseStringList(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>"1, 3, 4" / "" を int list に分解する。parse 失敗要素はスキップ。</summary>
    private static IReadOnlyList<int> ParseIntList(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? (int?)n : null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .ToList();
    }

    private static bool? ParseBoolNullable(Dictionary<string, string> f, string key)
        => f.TryGetValue(key, out var v) && bool.TryParse(v, out var parsed) ? parsed : null;

    private static int? ParseIntNullable(Dictionary<string, string> f, string key)
        => f.TryGetValue(key, out var v) && int.TryParse(v, out var parsed) ? parsed : null;
}
