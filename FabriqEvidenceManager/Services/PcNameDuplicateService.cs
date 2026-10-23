using System;
using System.Collections.Generic;
using System.Linq;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <inheritdoc cref="IPcNameDuplicateService"/>
public sealed class PcNameDuplicateService : IPcNameDuplicateService
{
    /// <summary>
    /// fabriq がシリアル取得不能時にフォルダ名へ入れるプレースホルダ
    /// (kernel/common.ps1 Get-HardwareUniqueId の "UNKNOWN")。正規化後の値で比較する。
    /// </summary>
    private const string UnknownSerialPlaceholder = "UNKNOWN";

    /// <inheritdoc />
    public IReadOnlyList<PcNameDuplicateGroup> Evaluate(IReadOnlyList<PcEvidence> pcs)
    {
        // 前回の判定結果が残らないよう、いったん全 PC をリセット
        foreach (var pc in pcs)
            pc.DuplicatePcNameMachineCount = 0;

        var groups = new List<PcNameDuplicateGroup>();

        foreach (var sameName in pcs.GroupBy(p => p.PcName, StringComparer.OrdinalIgnoreCase))
        {
            var members = sameName.ToList();
            if (members.Count < 2) continue;

            var machineCount = CountMachines(members);
            if (machineCount < 2) continue; // 同一端末の再実行のみ

            foreach (var pc in members)
                pc.DuplicatePcNameMachineCount = machineCount;

            groups.Add(new PcNameDuplicateGroup
            {
                PcName = members[0].PcName,
                MachineCount = machineCount,
                Members = members,
            });
        }

        return groups
            .OrderBy(g => g.PcName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 別端末の台数を数える。シリアル既知の PC はシリアルで同一端末をまとめ、
    /// シリアル不明の PC は 1 台ずつ別端末として数える。
    /// </summary>
    private static int CountMachines(IEnumerable<PcEvidence> members)
    {
        var knownSerials = new HashSet<string>(StringComparer.Ordinal);
        var unknownCount = 0;

        foreach (var pc in members)
        {
            var serial = NormalizeSerial(pc.SerialNumber);
            if (serial.Length == 0)
                unknownCount++;
            else
                knownSerials.Add(serial);
        }

        return knownSerials.Count + unknownCount;
    }

    /// <summary>
    /// シリアルを英数字のみ・大文字に正規化する。
    /// フォルダ名由来 ("VMware-56-4d-...": 記号をハイフン置換) と
    /// manifest 由来 ("VMware-56 4d ...") の表記ゆれで同一端末を別端末と誤判定しないため。
    /// プレースホルダ "UNKNOWN" は不明 (空文字) として扱う。
    /// </summary>
    private static string NormalizeSerial(string? serial)
    {
        if (string.IsNullOrEmpty(serial)) return string.Empty;

        var normalized = new string(serial
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

        return normalized == UnknownSerialPlaceholder ? string.Empty : normalized;
    }
}
