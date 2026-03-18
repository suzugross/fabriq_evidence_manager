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

        // ブロック単位でパース:
        //   Connection Name:      {name}
        //   Adapter:              {adapter}
        //   Physical Address:     {mac}
        //   Status:               {status}
        //   (空行で区切り)

        string? connectionName = null;
        string? adapter = null;
        string? physicalAddress = null;
        string? status = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // ヘッダ行はスキップ
            if (trimmed.StartsWith("====") || trimmed.Contains("MAC Address List"))
                continue;

            if (TryExtractValue(trimmed, "Connection Name:", out var cn))
            {
                // 新しいブロックの開始 → 前のブロックがあればフラッシュ
                FlushEntry(entries, connectionName, adapter, physicalAddress, status);
                connectionName = cn;
                adapter = null;
                physicalAddress = null;
                status = null;
            }
            else if (TryExtractValue(trimmed, "Adapter:", out var ad))
            {
                adapter = ad;
            }
            else if (TryExtractValue(trimmed, "Physical Address:", out var pa))
            {
                physicalAddress = pa;
            }
            else if (TryExtractValue(trimmed, "Status:", out var st))
            {
                status = st;
            }
        }

        // 最後のブロックをフラッシュ
        FlushEntry(entries, connectionName, adapter, physicalAddress, status);

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

    /// <summary>ブロックの4フィールドが揃っていればエントリとして追加する。</summary>
    private static void FlushEntry(
        List<MacAddressEntry> entries,
        string? connectionName, string? adapter, string? physicalAddress, string? status)
    {
        if (connectionName is null || physicalAddress is null)
            return;

        // MACアドレスの妥当性を正規表現で検証
        if (!MacAddressPattern().IsMatch(physicalAddress))
            return;

        entries.Add(new MacAddressEntry
        {
            ConnectionName = connectionName,
            AdapterName = adapter ?? string.Empty,
            PhysicalAddress = physicalAddress,
            Status = status ?? string.Empty,
        });
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
    // ネットワーク設定 (03_NetworkConfig.txt)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<NetworkInterfaceInfo> ParseNetworkConfig(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<NetworkInterfaceInfo>();

        string? currentInterface = null;
        string ipv4 = "", subnet = "", gateway = "";
        var dnsList = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // ヘッダ行はスキップ
            if (trimmed.StartsWith("====") || trimmed.Contains("Network Settings"))
                continue;

            if (trimmed.StartsWith("Interface:", StringComparison.OrdinalIgnoreCase))
            {
                // 前のブロックをフラッシュ
                if (currentInterface is not null)
                    results.Add(BuildNetworkInfo(currentInterface, ipv4, subnet, gateway, dnsList));

                currentInterface = trimmed["Interface:".Length..].Trim();
                ipv4 = "";
                subnet = "";
                gateway = "";
                dnsList = [];
            }
            else if (TryExtractValue(trimmed, "IPv4 Address:", out var ip))
            {
                ipv4 = ip;
            }
            else if (TryExtractValue(trimmed, "Subnet Mask:", out var sm))
            {
                subnet = sm;
            }
            else if (TryExtractValue(trimmed, "Default Gateway:", out var gw))
            {
                gateway = gw;
            }
            else if (TryExtractValue(trimmed, "DNS Servers:", out var dns))
            {
                dnsList = dns.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Where(d => !d.Contains(':')) // IPv6 (fec0:...) を除外
                    .Order()
                    .ToList();
            }
        }

        // 最後のブロック
        if (currentInterface is not null)
            results.Add(BuildNetworkInfo(currentInterface, ipv4, subnet, gateway, dnsList));

        return results;
    }

    private static NetworkInterfaceInfo BuildNetworkInfo(
        string interfaceName, string ipv4, string subnet, string gateway, List<string> dnsList) =>
        new()
        {
            InterfaceName = interfaceName,
            IPv4Address = ipv4,
            SubnetMask = subnet,
            DefaultGateway = gateway,
            DnsServers = dnsList,
        };

    // ================================================================
    // プリンタ一覧 (04_Printers.txt)
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<PrinterEntry> ParsePrinters(string filePath)
    {
        var lines = ReadAllLines(filePath);
        var results = new List<PrinterEntry>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // ヘッダ行・空行・区切り行をスキップ
            if (string.IsNullOrEmpty(trimmed)
                || trimmed.StartsWith("====")
                || trimmed.Contains("Printers")
                || !trimmed.Contains('='))
                continue;

            // Name=xxx|Driver=xxx|Port=xxx 形式をパース
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var segment in trimmed.Split('|'))
            {
                var eqIndex = segment.IndexOf('=');
                if (eqIndex > 0)
                    fields[segment[..eqIndex].Trim()] = segment[(eqIndex + 1)..].Trim();
            }

            if (fields.TryGetValue("Name", out var name) && !string.IsNullOrWhiteSpace(name))
            {
                results.Add(new PrinterEntry
                {
                    Name = name,
                    Driver = fields.GetValueOrDefault("Driver", string.Empty),
                    Port = fields.GetValueOrDefault("Port", string.Empty),
                });
            }
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
    // ファイルI/O (BOM付きUTF-8)
    // ================================================================

    private static string[] ReadAllLines(string filePath)
        => File.ReadAllLines(filePath, Encoding.UTF8);

    private static string ReadAllText(string filePath)
        => File.ReadAllText(filePath, Encoding.UTF8);
}
