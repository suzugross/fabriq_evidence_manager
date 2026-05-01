using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FabriqEvidenceManager.Helpers;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// pc_information 内のログファイルおよび bitlocker ファイルをパースし、
/// シリアルナンバー・MACアドレス・BitLocker回復キー等の情報を抽出する。
/// 全ファイルは BOM付き UTF-8 として読み込む。
/// </summary>
public sealed partial class EvidenceParserService : IEvidenceParserService
{
    // ---- Regex (ソースジェネレータ) ----

    /// <summary>MACアドレスパターン: XX-XX-XX-XX-XX-XX</summary>
    [GeneratedRegex(@"([0-9A-Fa-f]{2}(?:[-:][0-9A-Fa-f]{2}){5})")]
    private static partial Regex MacAddressPattern();

    /// <summary>BitLocker Identifier (GUID): {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}</summary>
    [GeneratedRegex(@"Identifier:\s*\{?([0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})\}?")]
    private static partial Regex BitLockerIdentifierPattern();

    /// <summary>BitLocker Recovery Password: 6桁×8 (ハイフン区切り)</summary>
    [GeneratedRegex(@"Recovery Password:\s*(\d{6}-\d{6}-\d{6}-\d{6}-\d{6}-\d{6}-\d{6}-\d{6})")]
    private static partial Regex BitLockerRecoveryPasswordPattern();

    /// <summary>BitLocker Mount Point: X:</summary>
    [GeneratedRegex(@"Mount Point:\s*([A-Z]):")]
    private static partial Regex BitLockerMountPointPattern();

    /// <summary>10_SerialNumber.txt の "All Sources" 行: "&lt;label&gt;  : &lt;value&gt;  [&lt;tag&gt;]"</summary>
    [GeneratedRegex(@"^(?<label>.+?)\s+:\s+(?<value>.*?)\s+\[(?<tag>.+)\]\s*$")]
    private static partial Regex SerialSourceLinePattern();

    // ================================================================
    // 専用 Service (DI 注入)
    // ================================================================

    private readonly IWindowsLicenseParserService _windowsLicenseParser;
    private readonly IOfficeLicenseParserService _officeLicenseParser;
    private readonly ISecurityBaselineParserService _securityBaselineParser;
    private readonly ICertificatesParserService _certificatesParser;
    private readonly IBatteryReportParserService _batteryReportParser;
    private readonly IHardwareIdentifiersParserService _hardwareIdentifiersParser;
    private readonly IMemoryInventoryParserService _memoryInventoryParser;
    private readonly IEnvironmentVariablesParserService _environmentVariablesParser;
    private readonly IStartupItemsParserService _startupItemsParser;
    private readonly IPnpDevicesParserService _pnpDevicesParser;

    public EvidenceParserService(
        IWindowsLicenseParserService windowsLicenseParser,
        IOfficeLicenseParserService officeLicenseParser,
        ISecurityBaselineParserService securityBaselineParser,
        ICertificatesParserService certificatesParser,
        IBatteryReportParserService batteryReportParser,
        IHardwareIdentifiersParserService hardwareIdentifiersParser,
        IMemoryInventoryParserService memoryInventoryParser,
        IEnvironmentVariablesParserService environmentVariablesParser,
        IStartupItemsParserService startupItemsParser,
        IPnpDevicesParserService pnpDevicesParser)
    {
        _windowsLicenseParser = windowsLicenseParser;
        _officeLicenseParser = officeLicenseParser;
        _securityBaselineParser = securityBaselineParser;
        _certificatesParser = certificatesParser;
        _batteryReportParser = batteryReportParser;
        _hardwareIdentifiersParser = hardwareIdentifiersParser;
        _memoryInventoryParser = memoryInventoryParser;
        _environmentVariablesParser = environmentVariablesParser;
        _startupItemsParser = startupItemsParser;
        _pnpDevicesParser = pnpDevicesParser;
    }

    // ================================================================
    // PopulateDetails (ファイルI/Oオーケストレーション)
    // ================================================================

    /// <inheritdoc />
    public void PopulateDetails(PcEvidence pc)
    {
        // pc_information のパースは manifest 駆動 (kernel/EVIDENCE_MANIFEST.md schemaVersion=1)。
        // manifest 不在時は ManifestError 経由で UI に表示済みなのでここではスキップする。
        if (pc.PcInformationDirectoryPath is not null && pc.Manifest is not null)
        {
            var dir = pc.PcInformationDirectoryPath;
            foreach (var section in pc.Manifest.Sections)
            {
                // Failed / Skipped はパース対象外 (Success / Partial のみ)
                if (section.Status is SectionStatus.Failed or SectionStatus.Skipped)
                    continue;
                DispatchSection(pc, dir, section);
            }
        }

        // BitLocker 回復キー (manifest 外: bitlocker/ ディレクトリ)
        if (pc.BitLockerDirectoryPath is not null)
        {
            foreach (var bkFile in Directory.EnumerateFiles(pc.BitLockerDirectoryPath, "*.txt").OrderBy(f => f))
            {
                try
                {
                    var entry = ParseBitLockerKey(bkFile);
                    if (entry is not null) pc.BitLockerKeys.Add(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine($"[Parse Skip] {bkFile}: {ex.Message}");
                }
            }
        }

        ValidateEvidence(pc, null);
    }

    /// <summary>
    /// manifest セクション ID に応じて該当パーサを呼び出す。
    /// 既知 ID は EvidenceConstants.KnownSections と本 switch のケースが対応。
    /// 未知 ID は本 switch のどの case にもマッチせず、M5 の raw 表示パイプラインで扱う。
    /// </summary>
    private void DispatchSection(PcEvidence pc, string dir, ManifestSection section)
    {
        switch (section.Id)
        {
            case "02": TryParseList(dir, section, "_LocalUsers.csv", ParseLocalUsers, pc.LocalUsers); break;
            case "03": TryParseList(dir, section, "_LocalGroups.csv", ParseLocalGroups, pc.LocalGroups); break;
            case "04": TryParseList(dir, section, "_LocalGroupMembers.csv", ParseLocalGroupMembers, pc.LocalGroupMembers); break;
            case "05":
                TryParseSingle(dir, section, "_DomainStatus.txt", ParseDomainStatus, v => pc.DomainStatus = v);
                TryParseList(dir, section, "_UserProfiles.csv", ParseUserProfiles, pc.UserProfiles);
                break;
            case "8b":
                TryParseList(dir, section, "_Disks.csv", ParseDisks, pc.Disks);
                TryParseList(dir, section, "_Partitions.csv", ParsePartitions, pc.Partitions);
                break;
            case "09": TryParseList(dir, section, "_MacAddress.csv", ParseMacAddresses, pc.MacAddresses); break;
            case "10": ApplySerialNumberDetail(pc, dir, section); break;
            case "12":
                TryParseList(dir, section, "_FirewallProfiles.csv", ParseFirewallProfiles, pc.FirewallProfiles);
                TryParseList(dir, section, "_FirewallRules.csv", ParseFirewallRules, pc.FirewallRules);
                break;
            case "13": TryParseList(dir, section, "_OptionalFeatures.csv", ParseOptionalFeatures, pc.OptionalFeatures); break;
            case "15": TryParseSingle(dir, section, "_PowerSettings.txt", ParsePowerSettings, v => pc.PowerSettings = v); break;
            case "16": TryParseList(dir, section, "_WiFiProfiles.txt", ParseWiFiProfiles, pc.WiFiProfiles); break;
            case "17": TryParseList(dir, section, "_RestorePoints.csv", ParseRestorePoints, pc.RestorePoints); break;
            case "18": TryParseSingle(dir, section, "_DefenderStatus.txt", ParseDefenderStatus, v => pc.DefenderStatus = v); break;
            case "19": TryParseList(dir, section, "_WindowsUpdates.csv", ParseWindowsUpdates, pc.WindowsUpdates); break;
            case "21": TryParseSingle(dir, section, "_WindowsLicense.txt", _windowsLicenseParser.Parse, v => pc.WindowsLicense = v); break;
            case "22": ApplyOfficeLicenseDetail(pc, dir, section); break;
            case "23": TryParseSingle(dir, section, "_SecurityBaseline.txt", _securityBaselineParser.Parse, v => pc.SecurityBaseline = v); break;
            case "25": TryParseList(dir, section, "_Certificates.csv", _certificatesParser.Parse, pc.Certificates); break;
            case "26": TryParseSingle(dir, section, "_BatteryReport.html", _batteryReportParser.Parse, v => pc.BatteryReport = v); break;
            case "27": TryParseList(dir, section, "_EnvironmentVariables.csv", _environmentVariablesParser.Parse, pc.EnvironmentVariables); break;
            case "28": TryParseList(dir, section, "_StartupItems.csv", _startupItemsParser.Parse, pc.StartupItems); break;
            case "29":
                TryParseList(dir, section, "_MemorySlots.csv", _memoryInventoryParser.ParseSlots, pc.MemorySlots);
                TryParseList(dir, section, "_MemoryArraySummary.csv", _memoryInventoryParser.ParseArraySummary, pc.MemoryArraySummaries);
                break;
            case "30": TryParseList(dir, section, "_PnpDevices.csv", _pnpDevicesParser.Parse, pc.PnpDevices); break;
            case "31": TryParseSingle(dir, section, "_HardwareIdentifiers.txt", _hardwareIdentifiersParser.Parse, v => pc.HardwareIdentifiers = v); break;
            // 01/06/07/08/11/14/20: PopulateDetails で PcEvidence に直接格納する parse 対象なし
            //   (HostlistService / BaselineService / EvidenceVerificationService が on-demand で読む)
            default:
                // KnownSections に無い未知 ID は raw として捕捉 (M5: 前方互換)
                if (!EvidenceConstants.KnownSections.ContainsKey(section.Id))
                    CaptureUnknownSection(pc, dir, section);
                break;
        }
    }

    /// <summary>
    /// 未知 section の files[] 各々を読み込み、PcEvidence.UnknownSections に追加する。
    /// EVIDENCE_MANIFEST.md §4.1.2 「未知 section は raw 表示」準拠。
    /// </summary>
    private static void CaptureUnknownSection(PcEvidence pc, string dir, ManifestSection section)
    {
        foreach (var file in section.RegularFiles)
        {
            var path = Path.Combine(dir, file);
            if (!File.Exists(path)) continue;

            var fileType = file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? UnknownSectionFileType.Csv
                : UnknownSectionFileType.Text;

            try
            {
                pc.UnknownSections.Add(new UnknownSection
                {
                    Id = section.Id,
                    Title = section.Title,
                    FilePath = path,
                    FileType = fileType,
                    RawContent = File.ReadAllText(path, Encoding.UTF8),
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[Unknown Section Skip] {path}: {ex.Message}");
            }
        }
    }

    /// <inheritdoc />
    public void RevalidateEvidence(PcEvidence pc, EvidenceCheckOptions options)
        => ValidateEvidence(pc, options);

    /// <summary>
    /// パース結果を検証し、欠損がある場合は HasWarning / WarningMessage をセットする。
    /// options が null の場合は全項目チェック（初回パース時）。
    /// </summary>
    private static void ValidateEvidence(PcEvidence pc, EvidenceCheckOptions? options)
    {
        var warnings = new List<string>();

        // MACアドレス欠損
        if (options?.CheckEthernetMac != false && string.IsNullOrEmpty(pc.EthernetMac))
            warnings.Add("MACアドレス(有線)欠損");
        if (options?.CheckWifiMac != false && string.IsNullOrEmpty(pc.WifiMac))
            warnings.Add("MACアドレス(Wi-Fi)欠損");

        // BitLocker欠損
        if (options?.CheckBitLocker != false && pc.BitLockerKeys.Count == 0)
            warnings.Add("BitLockerキー未取得");

        // SN 取得不能 (10_SerialNumber.txt の全 canonical candidate が失敗)
        if (string.IsNullOrEmpty(pc.SerialNumber))
            warnings.Add("SN取得不能");

        // ディレクトリ/ファイル欠損
        if (options?.CheckPcInformation != false && pc.PcInformationDirectoryPath is null)
            warnings.Add("pc_information未検出");
        if (options?.CheckAutoCapture != false && pc.AutoCaptureDirectoryPath is null)
            warnings.Add("auto_capture未検出");
        if (options?.CheckChecklist != false && pc.ChecklistFilePath is null)
            warnings.Add("チェックリスト未検出");

        // Windows ライセンス認証 (CheckWindowsLicense ON のとき、未取得・未認証ともに警告)
        if (options?.CheckWindowsLicense != false)
        {
            if (pc.WindowsLicense is null)
                warnings.Add("Windowsライセンス未取得");
            else if (!pc.IsWindowsLicensed)
                warnings.Add("Windowsライセンス未認証");
        }

        // Office インストール有無 (CheckOfficeInstalled ON: 未取得・未インストールが警告)
        if (options?.CheckOfficeInstalled != false)
        {
            if (pc.OfficeLicense is null)
                warnings.Add("Office情報未取得");
            else if (!pc.OfficeLicense.IsOfficeInstalled)
                warnings.Add("Office未インストール");
        }

        // Office ライセンス認証 (CheckOfficeLicense ON: 認証失敗のみ警告。Partial は Caution 側で扱う)
        // CheckOfficeInstalled OFF + CheckOfficeLicense ON のとき未取得が漏れないように
        // OfficeLicense null は CheckOfficeInstalled の条件でカバー済みのため、ここでは
        // インストール済みのみ評価する。
        if (options?.CheckOfficeLicense != false
            && pc.OfficeLicense is { IsOfficeInstalled: true }
            && pc.OfficeLicenseEvaluation == OfficeLicenseEvaluation.AuthFailed)
        {
            warnings.Add("Officeライセンス認証失敗");
        }

        if (warnings.Count > 0)
        {
            pc.HasWarning = true;
            pc.WarningMessage = string.Join(", ", warnings);
        }
        else
        {
            pc.HasWarning = false;
            pc.WarningMessage = string.Empty;
        }
    }

    // ================================================================
    // シリアルナンバー
    // ================================================================

    /// <inheritdoc />
    public string ParseSerialNumber(string filePath)
        => ParseSerialNumberDetailed(filePath).CanonicalValue;

    /// <inheritdoc />
    public SerialNumberDetail ParseSerialNumberDetailed(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var canonicalValue = string.Empty;
        var canonicalSourceLabel = string.Empty;
        var rawSources = new List<(string Label, string Value, SerialSourceValidity Validity, string? Reason, bool Matches)>();
        var referenceUuid = string.Empty;
        var state = SerialBlockState.None;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // ブロックヘッダ "---- ... ----"
            if (trimmed.StartsWith("---- ", StringComparison.Ordinal) && trimmed.EndsWith(" ----", StringComparison.Ordinal))
            {
                if (trimmed.Contains("Canonical Serial Number")) state = SerialBlockState.Canonical;
                else if (trimmed.Contains("All Sources")) state = SerialBlockState.AllSources;
                else if (trimmed.Contains("Reference ID")) state = SerialBlockState.ReferenceId;
                else state = SerialBlockState.None;  // Selection Policy 等は捨てる
                continue;
            }

            // ファイル冒頭の "==== / PC Serial Number" は state=None でそのまま無視される

            switch (state)
            {
                case SerialBlockState.Canonical:
                    if (trimmed == "(Unretrievable)")
                    {
                        // canonicalValue は空のまま
                    }
                    else if (trimmed.StartsWith("(Source:", StringComparison.Ordinal))
                    {
                        var inner = trimmed.Substring("(Source:".Length).TrimEnd(')').Trim();
                        canonicalSourceLabel = inner.StartsWith("none", StringComparison.OrdinalIgnoreCase)
                            ? string.Empty
                            : inner;
                    }
                    else if (string.IsNullOrEmpty(canonicalValue))
                    {
                        canonicalValue = trimmed;
                    }
                    break;

                case SerialBlockState.AllSources:
                    var parsed = ParseSerialSourceLine(trimmed);
                    if (parsed is not null) rawSources.Add(parsed.Value);
                    break;

                case SerialBlockState.ReferenceId:
                    var refMatch = SerialSourceLinePattern().Match(trimmed);
                    if (refMatch.Success)
                    {
                        var v = refMatch.Groups["value"].Value.Trim();
                        if (v != "(empty)") referenceUuid = v;
                    }
                    break;
            }
        }

        // IsSelectedCanonical を採用ソースに付ける + IsCanonicalCandidate をラベル名から決定
        var sources = new List<SerialSourceEntry>(rawSources.Count);
        foreach (var raw in rawSources)
        {
            sources.Add(new SerialSourceEntry
            {
                Label = raw.Label,
                Value = raw.Value,
                Validity = raw.Validity,
                ValidityReason = raw.Reason,
                MatchesCanonical = raw.Matches,
                IsCanonicalCandidate = IsCanonicalCandidateLabel(raw.Label),
                IsSelectedCanonical = !string.IsNullOrEmpty(canonicalSourceLabel)
                    && string.Equals(raw.Label, canonicalSourceLabel, StringComparison.Ordinal),
            });
        }

        return new SerialNumberDetail
        {
            CanonicalValue = canonicalValue,
            CanonicalSourceLabel = canonicalSourceLabel,
            Sources = sources,
            ReferenceUuid = referenceUuid,
        };
    }

    /// <summary>"All Sources" ブロックの 1 行をパース。形式に合わなければ null。</summary>
    private static (string Label, string Value, SerialSourceValidity Validity, string? Reason, bool Matches)?
        ParseSerialSourceLine(string line)
    {
        var m = SerialSourceLinePattern().Match(line);
        if (!m.Success) return null;

        var label = m.Groups["label"].Value.Trim();
        var rawValue = m.Groups["value"].Value.Trim();
        var value = rawValue == "(empty)" ? string.Empty : rawValue;
        var tag = m.Groups["tag"].Value.Trim();

        var (validity, reason, matches) = ParseSerialTag(tag);
        return (label, value, validity, reason, matches);
    }

    private static (SerialSourceValidity Validity, string? Reason, bool Matches) ParseSerialTag(string tag)
    {
        if (tag.StartsWith("VALID, MATCH", StringComparison.Ordinal))
            return (SerialSourceValidity.Valid, null, true);
        if (tag.StartsWith("VALID, DIFFERENT", StringComparison.Ordinal))
            return (SerialSourceValidity.Valid, null, false);
        if (tag.StartsWith("VALID", StringComparison.Ordinal))
            return (SerialSourceValidity.Valid, null, false);
        if (tag.StartsWith("INVALID:", StringComparison.Ordinal))
            return (SerialSourceValidity.Invalid, tag.Substring("INVALID:".Length).Trim(), false);
        if (tag.StartsWith("INVALID", StringComparison.Ordinal))
            return (SerialSourceValidity.Invalid, null, false);
        if (tag.StartsWith("QUERY FAILED:", StringComparison.Ordinal))
            return (SerialSourceValidity.QueryFailed, tag.Substring("QUERY FAILED:".Length).Trim(), false);
        if (tag.StartsWith("QUERY FAILED", StringComparison.Ordinal))
            return (SerialSourceValidity.QueryFailed, null, false);
        return (SerialSourceValidity.Invalid, tag, false);  // 未知タグ
    }

    /// <summary>BaseBoard 以外は canonical 採用候補 (PowerShell 側 IsCanonicalCandidate と整合)</summary>
    private static bool IsCanonicalCandidateLabel(string label) => label switch
    {
        "Win32_BIOS.SerialNumber" => true,
        "Win32_ComputerSystemProduct.IdentifyingNumber" => true,
        "Win32_SystemEnclosure.SerialNumber" => true,
        "Win32_BaseBoard.SerialNumber" => false,        // record-only (motherboard SN)
        "Registry SystemSerialNumber" => true,
        _ => false,
    };

    private enum SerialBlockState { None, Canonical, AllSources, ReferenceId }

    /// <summary>
    /// §10 manifest dispatch 専用ヘルパ。
    /// CanonicalValue / Source / UUID / 全ソース trail を一括で PcEvidence に充填する。
    /// </summary>
    /// <summary>
    /// §22 Office License: 22_OfficeLicense.txt と 22_OfficeVnextLicenses.csv の
    /// 両方を section.RegularFiles から発見して OfficeLicenseParserService に渡す。
    /// CSV 不在時 (旧 evidence) は txt のみで動作 (パーサ側で空配列扱い)。
    /// </summary>
    private void ApplyOfficeLicenseDetail(PcEvidence pc, string dir, ManifestSection section)
    {
        var txtFile = section.RegularFiles.FirstOrDefault(
            f => f.EndsWith("_OfficeLicense.txt", StringComparison.OrdinalIgnoreCase));
        if (txtFile is null) return;
        var txtPath = Path.Combine(dir, txtFile);
        if (!File.Exists(txtPath)) return;

        var csvFile = section.RegularFiles.FirstOrDefault(
            f => f.EndsWith("_OfficeVnextLicenses.csv", StringComparison.OrdinalIgnoreCase));
        var csvPath = csvFile is null ? null : Path.Combine(dir, csvFile);

        try
        {
            pc.OfficeLicense = _officeLicenseParser.Parse(txtPath, csvPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[Parse Skip] {txtPath}: {ex.Message}");
        }
    }

    private void ApplySerialNumberDetail(PcEvidence pc, string dir, ManifestSection section)
    {
        var fileName = section.RegularFiles.FirstOrDefault(
            f => f.EndsWith("_SerialNumber.txt", StringComparison.OrdinalIgnoreCase));
        if (fileName is null) return;
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path)) return;

        try
        {
            var detail = ParseSerialNumberDetailed(path);
            pc.SerialNumber = detail.CanonicalValue;
            pc.SerialNumberSource = string.IsNullOrEmpty(detail.CanonicalSourceLabel)
                ? null
                : detail.CanonicalSourceLabel;
            pc.SerialReferenceUuid = detail.ReferenceUuid;
            foreach (var s in detail.Sources) pc.SerialSourceTrail.Add(s);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[Parse Skip] {path}: {ex.Message}");
        }
    }

    // ================================================================
    // MACアドレス
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<MacAddressEntry> ParseMacAddresses(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var entries = new List<MacAddressEntry>();

        // CSV形式: "Name","InterfaceDescription","MacAddress","Status"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 3)
                continue;

            var name = fields[0];
            var adapter = fields.Count > 1 ? fields[1] : string.Empty;
            var mac = fields.Count > 2 ? fields[2] : string.Empty;
            var status = fields.Count > 3 ? fields[3] : string.Empty;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(mac))
                continue;

            // MACアドレスの妥当性を正規表現で検証
            if (!MacAddressPattern().IsMatch(mac))
                continue;

            entries.Add(new MacAddressEntry
            {
                ConnectionName = name,
                AdapterName = adapter,
                PhysicalAddress = mac,
                Status = status,
            });
        }

        return entries;
    }

    /// <summary>"Key:      Value" 形式の行から Value 部分を抽出する。</summary>
    private static bool TryExtractValue(string line, string key, out string value)
    {
        value = string.Empty;
        if (!line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            return false;

        value = line[key.Length..].Trim();
        return true;
    }

    // ================================================================
    // BitLocker 回復キー
    // ================================================================

    /// <inheritdoc />
    public BitLockerEntry? ParseBitLockerKey(string filePath)
    {
        var text = ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // ドライブレター (ファイル名から: {pcName}_{drive}.txt)
        var driveLetter = ExtractDriveLetterFromFileName(filePath);

        // ファイル名から取得できない場合は本文の Mount Point から取得
        if (string.IsNullOrEmpty(driveLetter))
        {
            var mpMatch = BitLockerMountPointPattern().Match(text);
            driveLetter = mpMatch.Success ? mpMatch.Groups[1].Value : string.Empty;
        }

        // Identifier (GUID)
        var idMatch = BitLockerIdentifierPattern().Match(text);
        var keyId = idMatch.Success ? idMatch.Groups[1].Value : string.Empty;

        // Recovery Password (6桁×8)
        var pwMatch = BitLockerRecoveryPasswordPattern().Match(text);
        var recoveryPassword = pwMatch.Success ? pwMatch.Groups[1].Value : string.Empty;

        // 回復パスワードが取得できなければ実用価値なし
        if (string.IsNullOrEmpty(recoveryPassword))
            return null;

        return new BitLockerEntry
        {
            DriveLetter = driveLetter,
            KeyId = keyId,
            RecoveryPassword = recoveryPassword,
        };
    }

    /// <summary>
    /// ファイル名 "{pcName}_{drive}.txt" から末尾のドライブレターを抽出する。
    /// </summary>
    private static string ExtractDriveLetterFromFileName(string filePath)
    {
        var stem = Path.GetFileNameWithoutExtension(filePath);

        // 末尾が "_X" (X=英字1文字) のパターン
        if (stem.Length >= 2 && stem[^2] == '_' && char.IsLetter(stem[^1]))
            return stem[^1..].ToUpperInvariant();

        return string.Empty;
    }

    // ================================================================
    // ホスト名 (01_SystemInfo.txt)
    // ================================================================

    /// <inheritdoc />
    public string ParseHostname(string filePath)
    {
        foreach (var line in ReadAllLines(filePath))
        {
            if (TryExtractValue(line.Trim(), "Hostname:", out var hostname))
                return hostname;
        }
        return string.Empty;
    }

    // ================================================================
    // システム基本情報 (01_SystemInfo.txt)
    // ================================================================

    /// <inheritdoc />
    public SystemInfoData ParseSystemInfo(string filePath)
    {
        var lines = ReadAllLines(filePath);
        string hostname = "", osName = "", version = "", cpu = "", memory = "";

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // ヘッダ行・空行をスキップ
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("====") || trimmed.Contains("System Basic Info"))
                continue;

            if (TryExtractValue(trimmed, "Hostname:", out var h))
                hostname = h;
            else if (TryExtractValue(trimmed, "OS Name:", out var o))
                osName = o;
            else if (TryExtractValue(trimmed, "Version:", out var v))
                version = v;
            else if (TryExtractValue(trimmed, "CPU:", out var c))
                cpu = c;
            else if (TryExtractValue(trimmed, "Memory:", out var m))
                memory = m;
        }

        return new SystemInfoData
        {
            Hostname = hostname,
            OsName = osName,
            Version = version,
            Cpu = cpu,
            Memory = memory,
        };
    }

    // ================================================================
    // ネットワーク設定 (06_NetworkConfig.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<NetworkInterfaceInfo> ParseNetworkConfig(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<NetworkInterfaceInfo>();

        // CSV形式: "Interface","IPv4Address","SubnetMask","DefaultGateway","DNSServers"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 1 || string.IsNullOrWhiteSpace(fields[0]))
                continue;

            var interfaceName = fields[0];
            var ipv4 = fields.Count > 1 ? fields[1] : string.Empty;
            var subnet = fields.Count > 2 ? fields[2] : string.Empty;
            var gateway = fields.Count > 3 ? fields[3] : string.Empty;
            var dnsRaw = fields.Count > 4 ? fields[4] : string.Empty;

            // DNS: カンマ区切りの値を分割し、IPv6 を除外してソート
            var dnsList = dnsRaw
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(d => !d.Contains(':')) // IPv6 (fec0:...) を除外
                .Order()
                .ToList();

            results.Add(new NetworkInterfaceInfo
            {
                InterfaceName = interfaceName,
                IPv4Address = ipv4,
                SubnetMask = subnet,
                DefaultGateway = gateway,
                DnsServers = dnsList,
            });
        }

        return results;
    }

    // ================================================================
    // プリンタ一覧 (07_Printers.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<PrinterEntry> ParsePrinters(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<PrinterEntry>();

        // CSV形式: "Name","DriverName","PortName","Shared","PrinterStatus"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 1 || string.IsNullOrWhiteSpace(fields[0]))
                continue;

            results.Add(new PrinterEntry
            {
                Name = fields[0],
                Driver = fields.Count > 1 ? fields[1] : string.Empty,
                Port = fields.Count > 2 ? fields[2] : string.Empty,
            });
        }

        return results;
    }

    // ================================================================
    // BitLockerステータス (05_BitLocker.txt)
    // ================================================================

    /// <summary>Volume X: [Label] パターン</summary>
    [GeneratedRegex(@"^Volume\s+([A-Z]):\s*\[(\w+)\]")]
    private static partial Regex BitLockerVolumeHeaderPattern();

    /// <inheritdoc />
    public IReadOnlyList<BitLockerVolumeStatus> ParseBitLockerStatus(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<BitLockerVolumeStatus>();

        string? drive = null;
        string label = "", conversion = "", percentage = "", method = "", protection = "";

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // ヘッダ行スキップ
            if (trimmed.StartsWith("====") || trimmed.Contains("BitLocker Status"))
                continue;

            var volMatch = BitLockerVolumeHeaderPattern().Match(trimmed);
            if (volMatch.Success)
            {
                // 前のブロックをフラッシュ
                if (drive is not null)
                    results.Add(BuildVolumeStatus(drive, label, conversion, percentage, method, protection));

                drive = volMatch.Groups[1].Value;
                label = volMatch.Groups[2].Value;
                conversion = "";
                percentage = "";
                method = "";
                protection = "";
            }
            else if (TryExtractValue(trimmed, "Conversion Status:", out var cs))
                conversion = cs;
            else if (TryExtractValue(trimmed, "Encryption Percentage:", out var ep))
                percentage = ep;
            else if (TryExtractValue(trimmed, "Encryption Method:", out var em))
                method = em;
            else if (TryExtractValue(trimmed, "Protection Status:", out var ps))
                protection = ps;
        }

        // 最後のブロック
        if (drive is not null)
            results.Add(BuildVolumeStatus(drive, label, conversion, percentage, method, protection));

        return results;
    }

    private static BitLockerVolumeStatus BuildVolumeStatus(
        string drive, string label, string conversion, string percentage, string method, string protection) =>
        new()
        {
            DriveLetter = drive,
            VolumeLabel = label,
            ConversionStatus = conversion,
            EncryptionPercentage = percentage,
            EncryptionMethod = method,
            ProtectionStatus = protection,
        };

    // ================================================================
    // インストール済みアプリ (08_DesktopApps.csv / 08_StoreApps.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<InstalledAppEntry> ParseDesktopApps(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<InstalledAppEntry>();

        // 先頭行はヘッダ (Name,Version,Publisher,InstallDate)
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 1 || string.IsNullOrWhiteSpace(fields[0]))
                continue;

            results.Add(new InstalledAppEntry
            {
                Name = fields[0],
                Version = fields.Count > 1 ? fields[1] : string.Empty,
                Publisher = fields.Count > 2 ? fields[2] : string.Empty,
                InstallDate = fields.Count > 3 ? fields[3] : string.Empty,
                Source = InstalledAppSource.Desktop,
            });
        }

        return results;
    }

    /// <inheritdoc />
    public IReadOnlyList<InstalledAppEntry> ParseStoreApps(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<InstalledAppEntry>();

        // 先頭行はヘッダ (Name,Version,Publisher)
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 1 || string.IsNullOrWhiteSpace(fields[0]))
                continue;

            results.Add(new InstalledAppEntry
            {
                Name = fields[0],
                Version = fields.Count > 1 ? fields[1] : string.Empty,
                Publisher = fields.Count > 2 ? fields[2] : string.Empty,
                Source = InstalledAppSource.Store,
            });
        }

        return results;
    }

    // ================================================================
    // ローカルユーザー (02_LocalUsers.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<LocalUserEntry> ParseLocalUsers(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<LocalUserEntry>();

        // "Name","Enabled","FullName","Description","SID","LastLogon","PasswordLastSet","PasswordRequired","PasswordExpires","AccountExpires","PrincipalSource"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 1 || string.IsNullOrWhiteSpace(fields[0]))
                continue;

            results.Add(new LocalUserEntry
            {
                Name = fields[0],
                Enabled = fields.Count > 1 && bool.TryParse(fields[1], out var e) && e,
                FullName = fields.Count > 2 ? fields[2] : string.Empty,
                Description = fields.Count > 3 ? fields[3] : string.Empty,
                SID = fields.Count > 4 ? fields[4] : string.Empty,
                LastLogon = fields.Count > 5 ? fields[5] : string.Empty,
                PasswordLastSet = fields.Count > 6 ? fields[6] : string.Empty,
                PasswordRequired = fields.Count > 7 && bool.TryParse(fields[7], out var pr) && pr,
                PrincipalSource = fields.Count > 10 ? fields[10] : string.Empty,
            });
        }

        return results;
    }

    // ================================================================
    // ローカルグループ (03_LocalGroups.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<LocalGroupEntry> ParseLocalGroups(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<LocalGroupEntry>();

        // "Name","Description","SID"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 1 || string.IsNullOrWhiteSpace(fields[0]))
                continue;

            results.Add(new LocalGroupEntry
            {
                Name = fields[0],
                Description = fields.Count > 1 ? fields[1] : string.Empty,
                SID = fields.Count > 2 ? fields[2] : string.Empty,
            });
        }

        return results;
    }

    // ================================================================
    // グループメンバー (04_LocalGroupMembers.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<LocalGroupMemberEntry> ParseLocalGroupMembers(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<LocalGroupMemberEntry>();

        // "GroupName","MemberName","ObjectClass","PrincipalSource"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 2 || string.IsNullOrWhiteSpace(fields[0]))
                continue;

            results.Add(new LocalGroupMemberEntry
            {
                GroupName = fields[0],
                MemberName = fields[1],
                ObjectClass = fields.Count > 2 ? fields[2] : string.Empty,
                PrincipalSource = fields.Count > 3 ? fields[3] : string.Empty,
            });
        }

        return results;
    }

    // ================================================================
    // ドメイン参加状態 (05_DomainStatus.txt)
    // ================================================================

    /// <inheritdoc />
    public DomainStatusData ParseDomainStatus(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string currentSection = "General";
        var currentDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        sections[currentSection] = currentDict;

        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();

            // ヘッダー行・区切り行をスキップ
            if (string.IsNullOrEmpty(trimmed)
                || trimmed.StartsWith("====")
                || trimmed.StartsWith("----"))
                continue;

            // dsregcmd セクション区切り: +------...
            if (trimmed.StartsWith("+------"))
            {
                // 次の行がセクション名 (| Section Name |)
                if (i + 1 < lines.Length)
                {
                    var nextLine = lines[i + 1].Trim();
                    if (nextLine.StartsWith('|') && nextLine.EndsWith('|'))
                    {
                        currentSection = nextLine.Trim('|', ' ');
                        currentDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        sections[currentSection] = currentDict;
                        i += 2; // セクション名行とその次の +------ 行をスキップ
                        continue;
                    }
                }
                continue;
            }

            // Key : Value 形式 (dsregcmd セクション: "AzureAdJoined : NO")
            var colonIdx = trimmed.IndexOf(" : ", StringComparison.Ordinal);
            if (colonIdx > 0)
            {
                var key = trimmed[..colonIdx].Trim();
                var value = trimmed[(colonIdx + 3)..].Trim();
                currentDict[key] = value;
                continue;
            }

            // Key:   Value 形式 (ヘッダーセクション: "PartOfDomain:   False")
            var simpleColon = trimmed.IndexOf(':');
            if (simpleColon > 0 && simpleColon + 1 < trimmed.Length
                && char.IsWhiteSpace(trimmed[simpleColon + 1]))
            {
                var key = trimmed[..simpleColon].Trim();
                var value = trimmed[(simpleColon + 1)..].Trim();
                currentDict[key] = value;
            }
        }

        // 主要フィールドを抽出 — ヘッダーセクション (General)
        bool partOfDomain = false;
        string domain = "", domainRole = "", currentUser = "";

        if (sections.TryGetValue("General", out var general))
        {
            if (general.TryGetValue("PartOfDomain", out var pod))
                partOfDomain = pod.Equals("True", StringComparison.OrdinalIgnoreCase);
            if (general.TryGetValue("Domain", out var d))
                domain = d;
            if (general.TryGetValue("DomainRole", out var dr))
                domainRole = dr;
            if (general.TryGetValue("Current User", out var cu))
                currentUser = cu;
        }

        // 主要フィールドを抽出 — dsregcmd セクション
        bool azureAdJoined = false, domainJoined = false;
        string domainName = "", tenantName = "";

        if (sections.TryGetValue("Device State", out var deviceState))
        {
            if (deviceState.TryGetValue("AzureAdJoined", out var aaj))
                azureAdJoined = aaj.Equals("YES", StringComparison.OrdinalIgnoreCase);
            if (deviceState.TryGetValue("DomainJoined", out var dj))
                domainJoined = dj.Equals("YES", StringComparison.OrdinalIgnoreCase);
            if (deviceState.TryGetValue("DomainName", out var dn))
                domainName = dn;
        }

        if (sections.TryGetValue("Tenant Details", out var tenantDetails))
        {
            if (tenantDetails.TryGetValue("TenantName", out var tn))
                tenantName = tn;
        }

        return new DomainStatusData
        {
            PartOfDomain = partOfDomain,
            Domain = domain,
            DomainRole = domainRole,
            CurrentUser = currentUser,
            AzureAdJoined = azureAdJoined,
            DomainJoined = domainJoined,
            DomainName = domainName,
            TenantName = tenantName,
            Sections = sections,
        };
    }

    // ================================================================
    // FWプロファイル (12_FirewallProfiles.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<FirewallProfileEntry> ParseFirewallProfiles(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<FirewallProfileEntry>();

        // "Name","Enabled","DefaultInboundAction","DefaultOutboundAction","LogFileName"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 1 || string.IsNullOrWhiteSpace(fields[0]))
                continue;

            results.Add(new FirewallProfileEntry
            {
                Name = fields[0],
                Enabled = fields.Count > 1 && bool.TryParse(fields[1], out var e) && e,
                DefaultInboundAction = fields.Count > 2 ? fields[2] : string.Empty,
                DefaultOutboundAction = fields.Count > 3 ? fields[3] : string.Empty,
                LogFileName = fields.Count > 4 ? fields[4] : string.Empty,
            });
        }

        return results;
    }

    // ================================================================
    // FWルール (12_FirewallRules.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<FirewallRuleEntry> ParseFirewallRules(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<FirewallRuleEntry>();

        // "DisplayName","Enabled","Direction","Action","Profile"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 1 || string.IsNullOrWhiteSpace(fields[0]))
                continue;

            results.Add(new FirewallRuleEntry
            {
                DisplayName = fields[0],
                Enabled = fields.Count > 1 && bool.TryParse(fields[1], out var e) && e,
                Direction = fields.Count > 2 ? fields[2] : string.Empty,
                Action = fields.Count > 3 ? fields[3] : string.Empty,
                Profile = fields.Count > 4 ? fields[4] : string.Empty,
            });
        }

        return results;
    }

    // ================================================================
    // オプション機能 (13_OptionalFeatures.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<OptionalFeatureEntry> ParseOptionalFeatures(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<OptionalFeatureEntry>();

        // "FeatureName","State"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 1 || string.IsNullOrWhiteSpace(fields[0]))
                continue;

            results.Add(new OptionalFeatureEntry
            {
                FeatureName = fields[0],
                State = fields.Count > 1 ? fields[1] : string.Empty,
            });
        }

        return results;
    }

    // ================================================================
    // CSV パーサーヘルパー
    // ================================================================

    /// <summary>
    /// ダブルクォート付きCSV行をフィールドに分割する。
    /// アプリ名にカンマが含まれる場合にも対応。
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // エスケープされたダブルクォート ("") かどうか
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++; // 次の " をスキップ
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        fields.Add(sb.ToString());
        return fields;
    }

    // ================================================================
    // ファイルI/O (BOM付きUTF-8)
    // ================================================================

    private static string[] ReadAllLines(string filePath)
        => File.ReadAllLines(filePath, Encoding.UTF8);

    private static string ReadAllText(string filePath)
        => File.ReadAllText(filePath, Encoding.UTF8);

    /// <summary>
    /// manifest section の files[] から指定 suffix にマッチするファイルを 1 つ選び、
    /// パース結果を destination に追加する汎用ヘルパー。
    /// </summary>
    private static void TryParseList<T>(string dir, ManifestSection section, string fileSuffix,
        Func<string, IReadOnlyList<T>> parser, List<T> destination)
    {
        var fileName = section.RegularFiles.FirstOrDefault(
            f => f.EndsWith(fileSuffix, StringComparison.OrdinalIgnoreCase));
        if (fileName is null) return;
        var filePath = Path.Combine(dir, fileName);
        if (!File.Exists(filePath)) return;

        try
        {
            foreach (var item in parser(filePath)) destination.Add(item);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[Parse Skip] {filePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// manifest section の files[] から指定 suffix にマッチするファイルを 1 つ選び、
    /// パース結果を setter で保存する汎用ヘルパー (nullable プロパティ用)。
    /// </summary>
    private static void TryParseSingle<T>(string dir, ManifestSection section, string fileSuffix,
        Func<string, T> parser, Action<T> setter) where T : class
    {
        var fileName = section.RegularFiles.FirstOrDefault(
            f => f.EndsWith(fileSuffix, StringComparison.OrdinalIgnoreCase));
        if (fileName is null) return;
        var filePath = Path.Combine(dir, fileName);
        if (!File.Exists(filePath)) return;

        try
        {
            setter(parser(filePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[Parse Skip] {filePath}: {ex.Message}");
        }
    }

    // ================================================================
    // ユーザープロファイル (05_UserProfiles.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<UserProfileEntry> ParseUserProfiles(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<UserProfileEntry>();

        // "LocalPath","SID","LastUseTime","Loaded"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = ParseCsvLine(line);
            if (fields.Count < 1 || string.IsNullOrWhiteSpace(fields[0])) continue;

            results.Add(new UserProfileEntry
            {
                LocalPath = fields[0],
                SID = fields.Count > 1 ? fields[1] : string.Empty,
                LastUseTime = fields.Count > 2 ? fields[2] : string.Empty,
                Loaded = fields.Count > 3 && bool.TryParse(fields[3], out var l) && l,
            });
        }

        return results;
    }

    // ================================================================
    // 物理ディスク (08b_Disks.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<DiskEntry> ParseDisks(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<DiskEntry>();

        // "Number","FriendlyName","SerialNumber","SizeGB","PartitionStyle","HealthStatus","OperationalStatus"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = ParseCsvLine(line);
            if (fields.Count < 2) continue;

            results.Add(new DiskEntry
            {
                Number = int.TryParse(fields[0], out var n) ? n : 0,
                FriendlyName = fields[1],
                SerialNumber = fields.Count > 2 ? fields[2] : string.Empty,
                SizeGB = fields.Count > 3 ? fields[3] : string.Empty,
                PartitionStyle = fields.Count > 4 ? fields[4] : string.Empty,
                HealthStatus = fields.Count > 5 ? fields[5] : string.Empty,
                OperationalStatus = fields.Count > 6 ? fields[6] : string.Empty,
            });
        }

        return results;
    }

    // ================================================================
    // パーティション (08b_Partitions.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<PartitionEntry> ParsePartitions(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<PartitionEntry>();

        // "DiskNumber","PartitionNumber","DriveLetter","SizeGB","Type","IsSystem","IsBoot","IsActive"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = ParseCsvLine(line);
            if (fields.Count < 5) continue;

            results.Add(new PartitionEntry
            {
                DiskNumber = int.TryParse(fields[0], out var dn) ? dn : 0,
                PartitionNumber = int.TryParse(fields[1], out var pn) ? pn : 0,
                DriveLetter = fields[2].Trim(),
                SizeGB = fields[3],
                Type = fields[4],
                IsSystem = fields.Count > 5 && bool.TryParse(fields[5], out var s) && s,
                IsBoot = fields.Count > 6 && bool.TryParse(fields[6], out var b) && b,
                IsActive = fields.Count > 7 && bool.TryParse(fields[7], out var a) && a,
            });
        }

        return results;
    }

    // ================================================================
    // 電源設定 (15_PowerSettings.txt)
    // ================================================================

    /// <inheritdoc />
    public PowerSettingsData ParsePowerSettings(string filePath)
    {
        var text = ReadAllText(filePath);
        var activePlan = string.Empty;

        // "---- Power Plans ----" セクションから "[ACTIVE]" のプラン名を抽出
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Contains("[ACTIVE]", StringComparison.OrdinalIgnoreCase))
            {
                // "  バランス [ACTIVE]" → "バランス"
                var idx = trimmed.IndexOf("[ACTIVE]", StringComparison.OrdinalIgnoreCase);
                activePlan = trimmed[..idx].Trim();
                break;
            }
        }

        return new PowerSettingsData
        {
            ActivePlanName = activePlan,
            RawText = text,
        };
    }

    // ================================================================
    // WiFiプロファイル (16_WiFiProfiles.txt)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<string> ParseWiFiProfiles(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var profiles = new List<string>();

        // "すべてのユーザー プロファイル     : SSID名" パターンを抽出
        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // 日本語環境: "すべてのユーザー プロファイル     : ssid"
            // 英語環境: "All User Profile     : ssid"
            var colonIdx = trimmed.IndexOf(':');
            if (colonIdx <= 0 || colonIdx >= trimmed.Length - 1) continue;

            var key = trimmed[..colonIdx].Trim();
            if (key.Contains("プロファイル", StringComparison.OrdinalIgnoreCase)
                || key.Contains("Profile", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed[(colonIdx + 1)..].Trim();
                if (!string.IsNullOrEmpty(value) && !value.StartsWith('<'))
                    profiles.Add(value);
            }
        }

        return profiles;
    }

    // ================================================================
    // 復元ポイント (17_RestorePoints.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<RestorePointEntry> ParseRestorePoints(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<RestorePointEntry>();

        // "SequenceNumber","Description","RestorePointType","CreationTime"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = ParseCsvLine(line);
            if (fields.Count < 2) continue;

            results.Add(new RestorePointEntry
            {
                SequenceNumber = int.TryParse(fields[0], out var sn) ? sn : 0,
                Description = fields[1],
                RestorePointType = fields.Count > 2 ? fields[2] : string.Empty,
                CreationTime = fields.Count > 3 ? fields[3] : string.Empty,
            });
        }

        return results;
    }

    // ================================================================
    // Windows Defender 状態 (18_DefenderStatus.txt)
    // ================================================================

    /// <inheritdoc />
    public DefenderStatusData ParseDefenderStatus(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("====")) continue;

            var colonIdx = trimmed.IndexOf(':');
            if (colonIdx <= 0 || colonIdx >= trimmed.Length - 1) continue;

            var key = trimmed[..colonIdx].Trim();
            var value = trimmed[(colonIdx + 1)..].Trim();
            kv[key] = value;
        }

        return new DefenderStatusData
        {
            AMServiceEnabled = B("AMServiceEnabled"),
            AntispywareEnabled = B("AntispywareEnabled"),
            AntivirusEnabled = B("AntivirusEnabled"),
            RealTimeProtectionEnabled = B("RealTimeProtectionEnabled"),
            BehaviorMonitorEnabled = B("BehaviorMonitorEnabled"),
            IoavProtectionEnabled = B("IoavProtectionEnabled"),
            NISEnabled = B("NISEnabled"),
            OnAccessProtectionEnabled = B("OnAccessProtectionEnabled"),
            AMEngineVersion = S("AMEngineVersion"),
            AMProductVersion = S("AMProductVersion"),
            AntivirusSignatureVersion = S("AntivirusSignatureVersion"),
            AntivirusSignatureLastUpdated = S("AntivirusSignatureLastUpdated"),
            QuickScanEndTime = S("QuickScanEndTime"),
            FullScanEndTime = S("FullScanEndTime"),
        };

        bool B(string key) => kv.TryGetValue(key, out var v) && bool.TryParse(v, out var b) && b;
        string S(string key) => kv.TryGetValue(key, out var v) ? v : string.Empty;
    }

    // ================================================================
    // Windows Update (19_WindowsUpdates.csv)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<WindowsUpdateEntry> ParseWindowsUpdates(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<WindowsUpdateEntry>();

        // "HotFixID","Description","InstalledBy","InstalledOn"
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = ParseCsvLine(line);
            if (fields.Count < 1 || string.IsNullOrWhiteSpace(fields[0])) continue;

            results.Add(new WindowsUpdateEntry
            {
                HotFixID = fields[0],
                Description = fields.Count > 1 ? fields[1] : string.Empty,
                InstalledBy = fields.Count > 2 ? fields[2] : string.Empty,
                InstalledOn = fields.Count > 3 ? fields[3] : string.Empty,
            });
        }

        return results;
    }

}
