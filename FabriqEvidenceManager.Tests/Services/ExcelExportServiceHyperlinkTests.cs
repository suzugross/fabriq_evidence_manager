using ClosedXML.Excel;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

/// <summary>
/// PC情報一覧シートの PC名 ハイパーリンクが、ClosedXML の string コンストラクタによる
/// 内部セル参照誤判定 ("シート名!パス" になる) を起こさず、
/// 外部ファイルへの相対 URI として書き出されることを保証する回帰テスト。
/// </summary>
public class ExcelExportServiceHyperlinkTests
{
    [Fact]
    public async Task ExportAsync_PcWithDetailData_PcNameHyperlinkIsExternalRelative()
    {
        var sut = new ExcelExportService();
        var pc = new PcEvidence
        {
            PcName = "TEST-PC",
            SerialNumber = "TESTSN-1",
            CollectionDate = "2026_05_01",
        };
        // HasAnyDetailData を true にするため最小データを 1 件流す
        pc.LocalUsers.Add(new LocalUserEntry { Name = "test-user" });

        var outputDir = Path.Combine(Path.GetTempPath(), $"fabriq_xl_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "main.xlsx");

        try
        {
            await sut.ExportAsync(new[] { pc }, outputPath);

            using var book = new XLWorkbook(outputPath);
            var ws = book.Worksheet("PC情報一覧");
            // ヘッダ行 1、データ行 2 が PC1 件目の PC名セル
            var pcNameCell = ws.Cell(2, 1);
            var link = pcNameCell.GetHyperlink();

            Assert.NotNull(link);
            // 外部リンクであること (内部セル参照だと IsExternal=false で
            // location 属性に "PC情報一覧!pc_details/..." と書かれて壊れる)
            Assert.True(link.IsExternal,
                "PC name hyperlink must be external (file path), not an internal cell reference.");
            Assert.NotNull(link.ExternalAddress);
            Assert.Contains("pc_details/", link.ExternalAddress.OriginalString);
            Assert.EndsWith(".xlsx", link.ExternalAddress.OriginalString);
        }
        finally
        {
            // テスト後の一時ファイル掃除 (失敗時の調査用に残してもよいが
            // CI 環境のディスク汚染回避のため try で削除)
            try { Directory.Delete(outputDir, recursive: true); } catch { }
        }
    }
}
