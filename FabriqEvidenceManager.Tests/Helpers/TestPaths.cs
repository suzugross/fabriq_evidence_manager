using System.IO;

namespace FabriqEvidenceManager.Tests.Helpers;

/// <summary>
/// テストフィクスチャパス解決ヘルパ。
/// 出力ディレクトリ (bin/Debug/net8.0-windows/) に `CopyToOutputDirectory` で
/// 複製された TestData/ 配下のパスを返す。
/// </summary>
internal static class TestPaths
{
    public static string GetTestDataDirectory(params string[] segments)
        => Path.Combine(AppContext.BaseDirectory, "TestData", Path.Combine(segments));
}
