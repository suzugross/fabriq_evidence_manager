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

    // ================================================================
    // PopulateDetails (ファイルI/Oオーケストレーション)
    // ================================================================

    /// <inheritdoc />
    public void PopulateDetails(PcEvidence pc)
    {
        // pc_information 配下のログファイルをパース
        if (pc.PcInformationDirectoryPath is not null)
        {
            // シリアルナンバー — 失敗しても空値のまま続行
            var serialFile = Path.Combine(pc.PcInformationDirectoryPath, EvidenceConstants.FileSerialNumber);
            if (File.Exists(serialFile))
            {
                try
                {
                    pc.SerialNumber = ParseSerialNumber(serialFile);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // パース失敗: SerialNumber は初期値 (空文字列) のまま
                    System.Diagnostics.Debug.WriteLine($"[Parse Skip] {serialFile}: {ex.Message}");
                }
            }

            // MACアドレス — 失敗しても空リストのまま続行
            var macFile = Path.Combine(pc.PcInformationDirectoryPath, EvidenceConstants.FileMacAddress);
            if (File.Exists(macFile))
            {
                try
                {
                    foreach (var entry in ParseMacAddresses(macFile))
                        pc.MacAddresses.Add(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine($"[Parse Skip] {macFile}: {ex.Message}");
                }
            }

            // ローカルユーザー
            TryParseAndPopulate(pc.PcInformationDirectoryPath, EvidenceConstants.FileLocalUsers,
                ParseLocalUsers, pc.LocalUsers);

            // ローカルグループ
            TryParseAndPopulate(pc.PcInformationDirectoryPath, EvidenceConstants.FileLocalGroups,
                ParseLocalGroups, pc.LocalGroups);

            // グループメンバー
            TryParseAndPopulate(pc.PcInformationDirectoryPath, EvidenceConstants.FileLocalGroupMembers,
                ParseLocalGroupMembers, pc.LocalGroupMembers);

            // ドメイン参加状態
            var domainFile = Path.Combine(pc.PcInformationDirectoryPath, EvidenceConstants.FileDomainStatus);
            if (File.Exists(domainFile))
            {
                try
                {
                    var domainStatus = ParseDomainStatus(domainFile);

                    // ドメインユーザー (実ファイル優先、なければ sample にフォールバック)
                    var domainUsersFile = ResolveFileWithFallback(pc.PcInformationDirectoryPath,
                        EvidenceConstants.FileDomainUsers, EvidenceConstants.FileDomainUsersSample);
                    if (domainUsersFile is not null)
                        domainStatus.DomainUsers.AddRange(ParseNameOnlyCsv(domainUsersFile));

                    // ドメイングループ
                    var domainGroupsFile = ResolveFileWithFallback(pc.PcInformationDirectoryPath,
                        EvidenceConstants.FileDomainGroups, EvidenceConstants.FileDomainGroupsSample);
                    if (domainGroupsFile is not null)
                        domainStatus.DomainGroups.AddRange(ParseNameOnlyCsv(domainGroupsFile));

                    pc.DomainStatus = domainStatus;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { System.Diagnostics.Debug.WriteLine($"[Parse Skip] {domainFile}: {ex.Message}"); }
            }

            // FWプロファイル
            TryParseAndPopulate(pc.PcInformationDirectoryPath, EvidenceConstants.FileFirewallProfiles,
                ParseFirewallProfiles, pc.FirewallProfiles);

            // FWルール
            TryParseAndPopulate(pc.PcInformationDirectoryPath, EvidenceConstants.FileFirewallRules,
                ParseFirewallRules, pc.FirewallRules);

            // オプション機能
            TryParseAndPopulate(pc.PcInformationDirectoryPath, EvidenceConstants.FileOptionalFeatures,
                ParseOptionalFeatures, pc.OptionalFeatures);
        }

        // BitLocker 回復キー — 個別ファイルごとにスキップ
        if (pc.BitLockerDirectoryPath is not null)
        {
            foreach (var bkFile in Directory.EnumerateFiles(pc.BitLockerDirectoryPath, "*.txt").OrderBy(f => f))
            {
                try
                {
                    var entry = ParseBitLockerKey(bkFile);
                    if (entry is not null)
                        pc.BitLockerKeys.Add(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine($"[Parse Skip] {bkFile}: {ex.Message}");
                }
            }
        }

        // ---- エビデンス欠損検知 (初回は全項目チェック) ----
        ValidateEvidence(pc, null);
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

        // ディレクトリ/ファイル欠損
        if (options?.CheckPcInformation != false && pc.PcInformationDirectoryPath is null)
            warnings.Add("pc_information未検出");
        if (options?.CheckAutoCapture != false && pc.AutoCaptureDirectoryPath is null)
            warnings.Add("auto_capture未検出");
        if (options?.CheckChecklist != false && pc.ChecklistFilePath is null)
            warnings.Add("チェックリスト未検出");

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
    {
        var lines = ReadAllLines(filePath);

        // ヘッダ (====) と空行をスキップし、最初の実データ行を返す
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;
            if (trimmed.StartsWith("====") || trimmed.Contains("Serial Number", StringComparison.OrdinalIgnoreCase))
                continue;
            return trimmed;
        }

        return string.Empty;
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
    /// pc_information 配下のファイルをパースし、結果をコレクションに追加する汎用ヘルパー。
    /// ファイルが存在しない場合やI/Oエラー時はスキップする。
    /// </summary>
    private void TryParseAndPopulate<T>(string pcInfoDir, string fileName,
        Func<string, IReadOnlyList<T>> parser, List<T> target)
    {
        var filePath = Path.Combine(pcInfoDir, fileName);
        if (!File.Exists(filePath)) return;

        try
        {
            foreach (var item in parser(filePath))
                target.Add(item);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[Parse Skip] {filePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// primary ファイルを優先し、存在しなければ fallback ファイルを返す。
    /// どちらも存在しない場合は null。
    /// </summary>
    private static string? ResolveFileWithFallback(string dir, string primaryName, string fallbackName)
    {
        var primary = Path.Combine(dir, primaryName);
        if (File.Exists(primary)) return primary;
        var fallback = Path.Combine(dir, fallbackName);
        return File.Exists(fallback) ? fallback : null;
    }

    /// <summary>
    /// ヘッダーが "Name" のみの単純CSVからName列を抽出する。
    /// サンプル行やプレースホルダーは除外する。
    /// </summary>
    private static List<string> ParseNameOnlyCsv(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var names = new List<string>();

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = ParseCsvLine(line);
            if (fields.Count < 1 || string.IsNullOrWhiteSpace(fields[0])) continue;
            // サンプル行を除外 (括弧で始まるプレースホルダー)
            if (fields[0].StartsWith('(')) continue;
            names.Add(fields[0]);
        }

        return names;
    }
}
