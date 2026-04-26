using System;
using System.IO;
using System.Text;
using System.Text.Json;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <inheritdoc />
public sealed class PcMemoService : IPcMemoService
{
    /// <summary>メモファイル名。fabriq 側の他ツールと衝突しない名前を選択。</summary>
    public const string MemoFileName = "manager_memo.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public PcMemo? Load(string pcRootDirectory)
    {
        var path = Path.Combine(pcRootDirectory, MemoFileName);
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<PcMemo>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[PcMemo Load Skip] {path}: {ex.Message}");
            return null;
        }
    }

    public void Save(string pcRootDirectory, PcMemo memo)
    {
        var path = Path.Combine(pcRootDirectory, MemoFileName);
        var json = JsonSerializer.Serialize(memo, JsonOptions);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    public void Delete(string pcRootDirectory)
    {
        var path = Path.Combine(pcRootDirectory, MemoFileName);
        if (File.Exists(path)) File.Delete(path);
    }
}
