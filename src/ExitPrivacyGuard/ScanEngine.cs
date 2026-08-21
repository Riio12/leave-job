using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace ExitPrivacyGuard;

public sealed class ScanEngine
{
    private static readonly HashSet<string> PlainTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".txt", ".csv", ".tsv", ".log", ".md", ".json", ".xml", ".html", ".htm", ".ini", ".cfg", ".conf", ".eml", ".sql", ".ps1", ".bat", ".cmd" };

    private static readonly HashSet<string> OfficeExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".docx", ".xlsx", ".pptx" };

    private static readonly (string Name, PrivacyLevel Level, Regex Pattern)[] PrivacyRules =
    {
        ("身份证件", PrivacyLevel.严重, new Regex(@"(?<!\d)(?:[1-9]\d{5})(?:18|19|20)\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])\d{3}[0-9Xx](?!\d)", RegexOptions.Compiled)),
        ("银行卡", PrivacyLevel.严重, new Regex(@"(?<!\d)(?:\d[ -]?){15,18}\d(?!\d)", RegexOptions.Compiled)),
        ("账号口令", PrivacyLevel.严重, new Regex(@"(?i)(password|passwd|pwd|口令|密码|secret|token|api[_ -]?key)\s*[:=：]", RegexOptions.Compiled)),
        ("手机号码", PrivacyLevel.高, new Regex(@"(?<!\d)1[3-9]\d{9}(?!\d)", RegexOptions.Compiled)),
        ("电子邮箱", PrivacyLevel.中, new Regex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("住址信息", PrivacyLevel.高, new Regex(@"(住址|家庭地址|现居住地|户籍地址)\s*[:：]", RegexOptions.Compiled)),
        ("薪酬人事", PrivacyLevel.高, new Regex(@"(工资|薪资|薪酬|奖金|绩效|劳动合同|离职证明|社保|公积金)", RegexOptions.Compiled)),
        ("医疗健康", PrivacyLevel.高, new Regex(@"(病历|诊断|体检报告|疾病|用药|医疗记录)", RegexOptions.Compiled)),
    };

    public async Task ScanAsync(ScanOptions options, IProgress<ScanResult> resultProgress,
        IProgress<(int Files, string Current)> status, CancellationToken token)
    {
        var pending = new Stack<string>();
        pending.Push(options.RootPath);
        var files = 0;

        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            IEnumerable<string> dirs;
            IEnumerable<string> paths;
            try
            {
                dirs = Directory.EnumerateDirectories(directory).ToArray();
                paths = Directory.EnumerateFiles(directory).ToArray();
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }

            foreach (var dir in dirs)
            {
                try
                {
                    if (!IsExcludedDirectory(dir) && !new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReparsePoint)) pending.Push(dir);
                }
                catch { }
            }

            foreach (var path in paths)
            {
                token.ThrowIfCancellationRequested();
                files++;
                if (files % 25 == 0) status.Report((files, path));
                var result = await AnalyzeAsync(path, options, token).ConfigureAwait(false);
                if (result is not null) resultProgress.Report(result);
            }
        }
        status.Report((files, "扫描完成"));
    }

    private static bool IsExcludedDirectory(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
            || name.Equals("WindowsApps", StringComparison.OrdinalIgnoreCase)
            || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || path.Contains(Path.DirectorySeparatorChar + ".exit-privacy-quarantine" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ScanResult?> AnalyzeAsync(string path, ScanOptions options, CancellationToken token)
    {
        FileInfo info;
        try { info = new FileInfo(path); }
        catch { return null; }

        var searchable = info.Name;
        string content = "";
        if (options.SearchContents && info.Length <= options.MaxContentBytes)
            content = await TryExtractTextAsync(path, token).ConfigureAwait(false);

        var keywordHits = options.Keywords.Count(k => searchable.Contains(k, StringComparison.OrdinalIgnoreCase)
            || content.Contains(k, StringComparison.OrdinalIgnoreCase));
        var reasons = new List<string>();
        PrivacyLevel level = PrivacyLevel.低;
        string category = keywordHits > 0 ? "关键词相关" : "其他";
        if (keywordHits > 0) reasons.Add($"命中 {keywordHits} 个关键词");

        var privacyHits = 0;
        if (options.PrivacyCorrelation)
        {
            var sample = searchable + "\n" + content;
            foreach (var rule in PrivacyRules)
            {
                var matches = rule.Pattern.Matches(sample).Count;
                if (matches == 0) continue;
                privacyHits += Math.Min(matches, 5);
                if (rule.Level > level) level = rule.Level;
                if (category == "关键词相关" || category == "其他") category = rule.Name;
                reasons.Add($"{rule.Name}×{matches}");
            }
        }

        if (keywordHits == 0 && privacyHits == 0) return null;
        if (keywordHits > 0 && level < PrivacyLevel.中) level = PrivacyLevel.中;
        var fileNameHit = options.Keywords.Any(k => searchable.Contains(k, StringComparison.OrdinalIgnoreCase));
        var relevance = Math.Min(100, keywordHits * 35 + privacyHits * 12 + (fileNameHit ? 15 : 0));
        return new ScanResult
        {
            FileName = info.Name, FullPath = info.FullName, Category = category, Level = level,
            Relevance = relevance, Reason = string.Join("；", reasons), Size = info.Length, Modified = info.LastWriteTime
        };
    }

    private static async Task<string> TryExtractTextAsync(string path, CancellationToken token)
    {
        try
        {
            var ext = Path.GetExtension(path);
            if (PlainTextExtensions.Contains(ext))
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, true);
                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                var buffer = new char[2_000_000];
                var count = await reader.ReadBlockAsync(buffer.AsMemory(), token).ConfigureAwait(false);
                return new string(buffer, 0, count);
            }
            if (OfficeExtensions.Contains(ext)) return ExtractOfficeText(path);
        }
        catch { }
        return "";
    }

    private static string ExtractOfficeText(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var sb = new StringBuilder();
        foreach (var entry in archive.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            if (!(entry.FullName.StartsWith("word/") || entry.FullName.StartsWith("xl/") || entry.FullName.StartsWith("ppt/"))) continue;
            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreWhitespace = true });
            while (reader.Read()) if (reader.NodeType == XmlNodeType.Text) sb.Append(reader.Value).Append(' ');
            if (sb.Length > 2_000_000) break;
        }
        return sb.ToString();
    }
}
