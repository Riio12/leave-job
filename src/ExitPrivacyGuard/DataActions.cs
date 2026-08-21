using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

namespace ExitPrivacyGuard;

public static class DataActions
{
    public static async Task<List<AuditEntry>> RecycleAsync(IEnumerable<ScanResult> items,
        IProgress<string> progress, CancellationToken token)
    {
        var audit = new List<AuditEntry>();
        var selected = items.ToList();
        for (var i = 0; i < selected.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var item = selected[i];
            progress.Report($"移至回收站 {i + 1}/{selected.Count}: {item.FileName}");
            try
            {
                var hash = await Sha256Async(item.FullPath, token);
                FileSystem.DeleteFile(item.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                audit.Add(new AuditEntry { Action = "移至回收站", Path = item.FullPath, Result = "成功", Sha256 = hash });
            }
            catch (Exception ex) { audit.Add(new AuditEntry { Action = "移至回收站", Path = item.FullPath, Result = "失败: " + ex.Message }); }
        }
        return audit;
    }

    public static async Task<string> SaveAuditAsync(IEnumerable<AuditEntry> audit, CancellationToken token)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExitPrivacyGuard", "audit");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"audit-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(audit, new JsonSerializerOptions { WriteIndented = true }), token);
        return path;
    }

    public static async Task<string> CreateBackupAsync(IEnumerable<ScanResult> items, string destination, string? passwordHint,
        IProgress<string> progress, CancellationToken token)
    {
        var selected = items.ToList();
        var tempRoot = Path.Combine(Path.GetTempPath(), "ExitPrivacyGuard", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var manifest = new List<object>();
        try
        {
            for (var i = 0; i < selected.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var item = selected[i];
                progress.Report($"备份 {i + 1}/{selected.Count}: {item.FileName}");
                if (!File.Exists(item.FullPath)) continue;
                var safeName = $"{i + 1:D4}_{SanitizeFileName(item.FileName)}";
                var copied = Path.Combine(tempRoot, "files", safeName);
                Directory.CreateDirectory(Path.GetDirectoryName(copied)!);
                File.Copy(item.FullPath, copied, true);
                manifest.Add(new { originalPath = item.FullPath, storedAs = "files/" + safeName,
                    item.Level, item.Category, item.Relevance, sha256 = await Sha256Async(copied, token) });
            }
            var metadata = new { createdAt = DateTimeOffset.Now, computer = Environment.MachineName,
                user = Environment.UserName, encryptionNotice = "ZIP未加密；请保存到 BitLocker To Go 加密U盘。", passwordHint, files = manifest };
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "manifest.json"), JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }), token);
            if (File.Exists(destination)) File.Delete(destination);
            ZipFile.CreateFromDirectory(tempRoot, destination, CompressionLevel.Optimal, false);
            return destination;
        }
        finally { try { Directory.Delete(tempRoot, true); } catch { } }
    }

    public static async Task<List<AuditEntry>> QuarantineAsync(IEnumerable<ScanResult> items, string root,
        IProgress<string> progress, CancellationToken token)
    {
        var audit = new List<AuditEntry>();
        var folder = Path.Combine(root, ".exit-privacy-quarantine", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);
        var selected = items.ToList();
        for (var i = 0; i < selected.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var item = selected[i];
            progress.Report($"隔离 {i + 1}/{selected.Count}: {item.FileName}");
            try
            {
                var hash = await Sha256Async(item.FullPath, token);
                var target = Path.Combine(folder, $"{i + 1:D4}_{SanitizeFileName(item.FileName)}");
                File.Move(item.FullPath, target);
                audit.Add(new AuditEntry { Action = "隔离", Path = item.FullPath, Result = "成功 -> " + target, Sha256 = hash });
            }
            catch (Exception ex) { audit.Add(new AuditEntry { Action = "隔离", Path = item.FullPath, Result = "失败: " + ex.Message }); }
        }
        await File.WriteAllTextAsync(Path.Combine(folder, "audit.json"), JsonSerializer.Serialize(audit, new JsonSerializerOptions { WriteIndented = true }), token);
        return audit;
    }

    public static async Task<string> Sha256Async(string path, CancellationToken token)
    {
        using var sha = SHA256.Create();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        return Convert.ToHexString(await sha.ComputeHashAsync(stream, token));
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value.Length > 120 ? value[..120] : value;
    }
}
