using System.IO;
using FabriqEvidenceManager.Helpers;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// hostlist の期待値とエビデンスファイルの実測値を突き合わせる。
/// pc_information 内の 01, 03, 04, 05 ファイルをパースし、
/// hostlist の対応カラムと比較してレポートを生成する。
/// </summary>
public sealed class EvidenceVerificationService : IEvidenceVerificationService
{
    private readonly IHostlistService _hostlist;
    private readonly IEvidenceParserService _parser;

    public EvidenceVerificationService(IHostlistService hostlist, IEvidenceParserService parser)
    {
        _hostlist = hostlist;
        _parser = parser;
    }

    /// <inheritdoc />
    public VerificationReport Verify(PcEvidence pc)
    {
        // hostlist 未読み込み or 該当PC名なし
        var entry = _hostlist.IsLoaded ? _hostlist.FindByPcName(pc.PcName) : null;
        if (entry is null)
        {
            return new VerificationReport
            {
                PcName = pc.PcName,
                HostlistFound = false,
            };
        }

        var items = new List<VerificationItem>();
        var pcInfoDir = pc.PcInformationDirectoryPath;

        // ---- ホスト名 ----
        var actualHostname = TryParseFile(pcInfoDir, EvidenceConstants.FileSystemInfo,
            path => _parser.ParseHostname(path));
        items.Add(Compare("ホスト名", entry.NewPCName, actualHostname ?? ""));

        // ---- ネットワーク ----
        var nics = TryParseFile(pcInfoDir, EvidenceConstants.FileNetworkConfig,
            path => _parser.ParseNetworkConfig(path));

        var ethernet = nics?.FirstOrDefault(n => n.IsEthernet);
        var wifi = nics?.FirstOrDefault(n => n.IsWifi);

        items.Add(Compare("Ethernet IP", entry.EthernetIP, ethernet?.IPv4Address ?? ""));
        items.Add(Compare("Ethernet サブネット", entry.EthernetSubnet, ethernet?.SubnetMask ?? ""));
        items.Add(Compare("Ethernet ゲートウェイ", entry.EthernetGateway, ethernet?.DefaultGateway ?? ""));
        items.Add(Compare("Wi-Fi IP", entry.WifiIP, wifi?.IPv4Address ?? ""));
        items.Add(Compare("Wi-Fi サブネット", entry.WifiSubnet, wifi?.SubnetMask ?? ""));
        items.Add(Compare("Wi-Fi ゲートウェイ", entry.WifiGateway, wifi?.DefaultGateway ?? ""));

        // ---- DNS (ソート済みリスト比較) ----
        var expectedDns = entry.GetDnsList();
        // Ethernet + Wi-Fi の DNS をマージしてユニーク化＆ソート
        var actualDnsList = new List<string>();
        if (ethernet is not null) actualDnsList.AddRange(ethernet.DnsServers);
        if (wifi is not null) actualDnsList.AddRange(wifi.DnsServers);
        var actualDns = actualDnsList.Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();

        items.Add(Compare("DNS",
            string.Join(", ", expectedDns),
            string.Join(", ", actualDns)));

        // ---- プリンタ ----
        var actualPrinters = TryParseFile(pcInfoDir, EvidenceConstants.FilePrinters,
            path => _parser.ParsePrinters(path));

        foreach (var expectedPrinter in entry.Printers)
        {
            var matched = actualPrinters?.FirstOrDefault(p =>
                p.Name.Equals(expectedPrinter.Name, StringComparison.OrdinalIgnoreCase));

            if (matched is not null)
            {
                items.Add(new VerificationItem
                {
                    ItemName = $"プリンタ: {expectedPrinter.Name}",
                    Expected = $"{expectedPrinter.Name} (Port: {expectedPrinter.Port})",
                    Actual = $"{matched.Name} (Port: {matched.Port})",
                    Status = matched.Port.Equals(expectedPrinter.Port, StringComparison.OrdinalIgnoreCase)
                        ? VerificationStatus.Match
                        : VerificationStatus.Mismatch,
                });
            }
            else
            {
                items.Add(new VerificationItem
                {
                    ItemName = $"プリンタ: {expectedPrinter.Name}",
                    Expected = $"{expectedPrinter.Name} (Port: {expectedPrinter.Port})",
                    Actual = "(未検出)",
                    Status = VerificationStatus.NoActual,
                });
            }
        }

        // ---- BitLocker (Cドライブが有効化されていること) ----
        var volumes = TryParseFile(pcInfoDir, EvidenceConstants.FileBitLockerStatus,
            path => _parser.ParseBitLockerStatus(path));
        var cVolume = volumes?.FirstOrDefault(v =>
            v.DriveLetter.Equals("C", StringComparison.OrdinalIgnoreCase));

        items.Add(new VerificationItem
        {
            ItemName = "BitLocker (C:)",
            Expected = "On (FullyEncrypted)",
            Actual = cVolume is not null
                ? $"{cVolume.ProtectionStatus} ({cVolume.ConversionStatus})"
                : "(未検出)",
            Status = cVolume is not null && cVolume.IsProtected
                ? VerificationStatus.Match
                : cVolume is null
                    ? VerificationStatus.NoActual
                    : VerificationStatus.Mismatch,
        });

        return new VerificationReport
        {
            PcName = pc.PcName,
            HostlistFound = true,
            Items = items,
        };
    }

    // ---- private helpers ----

    /// <summary>
    /// pc_information 配下のファイルをパースする。ファイル不在や I/O エラー時は null を返す。
    /// </summary>
    private static T? TryParseFile<T>(string? pcInfoDir, string fileName, Func<string, T> parser)
        where T : class
    {
        if (pcInfoDir is null) return null;
        var path = Path.Combine(pcInfoDir, fileName);
        if (!File.Exists(path)) return null;

        try
        {
            return parser(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[Verify Skip] {path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>string を返すパーサー用オーバーロード。</summary>
    private static string? TryParseFile(string? pcInfoDir, string fileName, Func<string, string> parser)
    {
        if (pcInfoDir is null) return null;
        var path = Path.Combine(pcInfoDir, fileName);
        if (!File.Exists(path)) return null;

        try
        {
            return parser(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[Verify Skip] {path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 期待値と実測値を比較して VerificationItem を生成する。
    /// 両方空の場合は NoExpected、期待値のみ空は NoExpected、実測値のみ空は NoActual。
    /// </summary>
    private static VerificationItem Compare(string itemName, string expected, string actual)
    {
        var expTrimmed = expected.Trim();
        var actTrimmed = actual.Trim();

        VerificationStatus status;
        if (string.IsNullOrEmpty(expTrimmed) && string.IsNullOrEmpty(actTrimmed))
            status = VerificationStatus.NoExpected;
        else if (string.IsNullOrEmpty(expTrimmed))
            status = VerificationStatus.NoExpected;
        else if (string.IsNullOrEmpty(actTrimmed))
            status = VerificationStatus.NoActual;
        else
            status = expTrimmed.Equals(actTrimmed, StringComparison.OrdinalIgnoreCase)
                ? VerificationStatus.Match
                : VerificationStatus.Mismatch;

        return new VerificationItem
        {
            ItemName = itemName,
            Expected = expTrimmed,
            Actual = actTrimmed,
            Status = status,
        };
    }
}
