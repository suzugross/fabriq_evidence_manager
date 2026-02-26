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

        // ---- エビデンス欠損検知 ----
        ValidateEvidence(pc);
    }

    /// <summary>
    /// パース結果を検証し、欠損がある場合は HasWarning / WarningMessage をセットする。
    /// </summary>
    private static void ValidateEvidence(PcEvidence pc)
    {
        var warnings = new List<string>();

        // MACアドレス欠損
        if (string.IsNullOrEmpty(pc.EthernetMac))
            warnings.Add("MACアドレス(有線)欠損");
        if (string.IsNullOrEmpty(pc.WifiMac))
            warnings.Add("MACアドレス(Wi-Fi)欠損");

        // BitLocker欠損
        if (pc.BitLockerKeys.Count == 0)
            warnings.Add("BitLockerキー未取得");

        // ディレクトリ/ファイル欠損
        if (pc.PcInformationDirectoryPath is null)
            warnings.Add("pc_information未検出");
        if (pc.AutoCaptureDirectoryPath is null)
            warnings.Add("auto_capture未検出");
        if (pc.ChecklistFilePath is null)
            warnings.Add("チェックリスト未検出");

        if (warnings.Count > 0)
        {
            pc.HasWarning = true;
            pc.WarningMessage = string.Join(", ", warnings);
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
    // ファイルI/O (BOM付きUTF-8)
    // ================================================================

    private static string[] ReadAllLines(string filePath)
        => File.ReadAllLines(filePath, Encoding.UTF8);

    private static string ReadAllText(string filePath)
        => File.ReadAllText(filePath, Encoding.UTF8);
}
