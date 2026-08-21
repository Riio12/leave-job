using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Security.Principal;

namespace ExitPrivacyGuard;

public sealed class MainForm : Form
{
    private readonly TextBox _keywords = new() { PlaceholderText = "姓名、邮箱、工号等；多个关键词用逗号分隔" };
    private readonly TextBox _root = new() { Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
    private readonly CheckBox _contents = new() { Text = "搜索文件内容（支持文本与 Office）", Checked = true, AutoSize = true };
    private readonly CheckBox _correlation = new() { Text = "隐私相关性搜索", Checked = true, AutoSize = true };
    private readonly Button _scan = new() { Text = "开始扫描", Height = 38 };
    private readonly Button _stop = new() { Text = "停止", Height = 38, Enabled = false };
    private readonly Button _backup = new() { Text = "备份选中项", Height = 38 };
    private readonly Button _quarantine = new() { Text = "隔离选中项", Height = 38 };
    private readonly Button _recycle = new() { Text = "移至回收站", Height = 38 };
    private readonly Button _security = new() { Text = "权限与安全拦截诊断", Height = 38 };
    private readonly Label _status = new() { Text = "就绪。扫描不会上传任何数据。", AutoSize = false, Height = 28 };
    private readonly Label _summary = new() { Text = "0 个结果", AutoSize = true };
    private readonly BindingList<ScanResult> _results = new();
    private readonly DataGridView _grid = new();
    private readonly ScanEngine _engine = new();
    private CancellationTokenSource? _cts;

    public MainForm()
    {
        Text = "离职隐私卫士 · 本地版";
        MinimumSize = new Size(1050, 680);
        Size = new Size(1280, 820);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = Color.FromArgb(246, 248, 252);
        BuildUi();
        WireEvents();
    }

    private void BuildUi()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Color.FromArgb(25, 36, 60), Padding = new Padding(22, 13, 20, 8) };
        header.Controls.Add(new Label { Text = "离职隐私卫士", ForeColor = Color.White, Font = new Font(Font.FontFamily, 18, FontStyle.Bold), AutoSize = true, Location = new Point(20, 11) });
        header.Controls.Add(new Label { Text = "数据留在本机 · 先备份后隔离 · 全程可审计", ForeColor = Color.FromArgb(180, 196, 220), AutoSize = true, Location = new Point(23, 48) });

        var options = new TableLayoutPanel { Dock = DockStyle.Top, Height = 142, Padding = new Padding(18, 14, 18, 8), ColumnCount = 4, RowCount = 3 };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        options.Controls.Add(new Label { Text = "搜索范围", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        options.Controls.Add(_root, 1, 0);
        var browse = new Button { Text = "选择目录", Dock = DockStyle.Fill };
        options.Controls.Add(browse, 2, 0);
        options.Controls.Add(new Label { Text = "搜索关键词", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        options.Controls.Add(_keywords, 1, 1);
        options.SetColumnSpan(_keywords, 2);
        var togglePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 7, 0, 0) };
        togglePanel.Controls.AddRange(new Control[] { _contents, _correlation });
        options.Controls.Add(togglePanel, 1, 2);
        options.SetColumnSpan(togglePanel, 2);
        options.Controls.Add(_scan, 3, 0);
        options.Controls.Add(_stop, 3, 1);
        browse.Click += (_, _) => BrowseRoot();

        ConfigureGrid();

        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 65, Padding = new Padding(18, 11, 18, 8), BackColor = Color.White, FlowDirection = FlowDirection.LeftToRight };
        actions.Controls.AddRange(new Control[] { _backup, _quarantine, _recycle, _security });
        _summary.Margin = new Padding(18, 10, 0, 0);
        actions.Controls.Add(_summary);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(20, 9, 20, 5), BackColor = Color.FromArgb(232, 237, 245) };
        _status.Dock = DockStyle.Fill;
        footer.Controls.Add(_status);

        Controls.Add(_grid);
        Controls.Add(actions);
        Controls.Add(footer);
        Controls.Add(options);
        Controls.Add(header);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.DataSource = _results;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(ScanResult.Selected), HeaderText = "选择", Width = 55 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ScanResult.Level), HeaderText = "隐私等级", Width = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ScanResult.Relevance), HeaderText = "相关度", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ScanResult.Category), HeaderText = "类别", Width = 95 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ScanResult.FileName), HeaderText = "文件", Width = 180 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ScanResult.FullPath), HeaderText = "路径", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 250 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ScanResult.Reason), HeaderText = "判定依据", Width = 210 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ScanResult.Modified), HeaderText = "修改时间", Width = 135, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" } });
        _grid.CellFormatting += (_, e) =>
        {
            if (_grid.Columns[e.ColumnIndex].DataPropertyName != nameof(ScanResult.Level) || e.Value is not PrivacyLevel level) return;
            if (e.CellStyle is not null)
                e.CellStyle.BackColor = level switch { PrivacyLevel.严重 => Color.MistyRose, PrivacyLevel.高 => Color.FromArgb(255, 235, 205), PrivacyLevel.中 => Color.LightYellow, _ => Color.Honeydew };
        };
    }

    private void WireEvents()
    {
        _scan.Click += async (_, _) => await StartScanAsync();
        _stop.Click += (_, _) => _cts?.Cancel();
        _backup.Click += async (_, _) => await BackupAsync();
        _quarantine.Click += async (_, _) => await QuarantineAsync();
        _recycle.Click += async (_, _) => await RecycleAsync();
        _security.Click += (_, _) => ShowSecurityDiagnostics();
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OpenContainingFolder(_results[e.RowIndex].FullPath); };
        _results.ListChanged += (_, _) => UpdateSummary();
    }

    private async Task StartScanAsync()
    {
        var keywords = _keywords.Text.Split(new[] { ',', '，', ';', '；', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (keywords.Length == 0 && !_correlation.Checked)
        {
            MessageBox.Show("请输入至少一个关键词，或开启“隐私相关性搜索”。", "缺少扫描条件", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Directory.Exists(_root.Text)) { MessageBox.Show("搜索目录不存在。", "无法扫描", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        _results.Clear();
        SetBusy(true);
        _cts = new CancellationTokenSource();
        var resultProgress = new Progress<ScanResult>(r => { _results.Add(r); SortResults(); });
        var statusProgress = new Progress<(int Files, string Current)>(x => _status.Text = $"已检查 {x.Files:N0} 个文件 · {CompactPath(x.Current)}");
        try
        {
            await _engine.ScanAsync(new ScanOptions { RootPath = _root.Text, Keywords = keywords, SearchContents = _contents.Checked, PrivacyCorrelation = _correlation.Checked }, resultProgress, statusProgress, _cts.Token);
            _status.Text = $"扫描完成：发现 {_results.Count:N0} 个相关文件。请人工复核后再操作。";
        }
        catch (OperationCanceledException) { _status.Text = $"扫描已停止，保留当前 {_results.Count:N0} 个结果。"; }
        finally { SetBusy(false); _cts.Dispose(); _cts = null; }
    }

    private void SortResults()
    {
        if (_results.Count < 2) return;
        var ordered = _results.OrderByDescending(x => x.Level).ThenByDescending(x => x.Relevance).ToList();
        _results.RaiseListChangedEvents = false;
        _results.Clear(); foreach (var item in ordered) _results.Add(item);
        _results.RaiseListChangedEvents = true; _results.ResetBindings();
    }

    private async Task BackupAsync()
    {
        var selected = SelectedItems();
        if (selected.Count == 0) { MessageBox.Show("请先勾选需要备份的文件。", "没有选择", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var dialog = new SaveFileDialog { Filter = "ZIP 备份包|*.zip", FileName = $"离职隐私备份-{DateTime.Now:yyyyMMdd-HHmm}.zip" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        SetBusy(true);
        try
        {
            var path = await DataActions.CreateBackupAsync(selected, dialog.FileName, "建议将备份保存至已启用 BitLocker To Go 的U盘", new Progress<string>(s => _status.Text = s), CancellationToken.None);
            MessageBox.Show("备份完成。ZIP 本身未加密，请存放到 BitLocker To Go 加密的U盘并妥善保管。\n\n" + path, "备份完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "备份失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { SetBusy(false); }
    }

    private async Task QuarantineAsync()
    {
        var selected = SelectedItems();
        if (selected.Count == 0) { MessageBox.Show("请先勾选需要隔离的文件。", "没有选择", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var text = $"将把 {selected.Count} 个文件移动到搜索根目录下的隐藏隔离区。\n\n这是可恢复操作，但文件将离开原位置。建议先完成备份。继续吗？";
        if (MessageBox.Show(text, "确认隔离", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        SetBusy(true);
        try
        {
            var audit = await DataActions.QuarantineAsync(selected, _root.Text, new Progress<string>(s => _status.Text = s), CancellationToken.None);
            foreach (var item in selected.Where(s => audit.Any(a => a.Path == s.FullPath && a.Result.StartsWith("成功"))).ToList()) _results.Remove(item);
            var failed = audit.Count(a => a.Result.StartsWith("失败"));
            MessageBox.Show($"隔离完成：成功 {audit.Count - failed}，失败 {failed}。隔离区内保存有 audit.json 审计记录。", "操作完成", MessageBoxButtons.OK, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        finally { SetBusy(false); }
    }

    private async Task RecycleAsync()
    {
        var selected = SelectedItems();
        if (selected.Count == 0) { MessageBox.Show("请先勾选需要删除的文件。", "没有选择", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var text = $"将把 {selected.Count} 个文件移至 Windows 回收站。\n\n请确认已完成备份和人工复核。继续吗？";
        if (MessageBox.Show(text, "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        SetBusy(true);
        try
        {
            var audit = await DataActions.RecycleAsync(selected, new Progress<string>(s => _status.Text = s), CancellationToken.None);
            var auditPath = await DataActions.SaveAuditAsync(audit, CancellationToken.None);
            foreach (var item in selected.Where(s => audit.Any(a => a.Path == s.FullPath && a.Result == "成功")).ToList()) _results.Remove(item);
            var failed = audit.Count(a => a.Result.StartsWith("失败"));
            MessageBox.Show($"已移至回收站：成功 {audit.Count - failed}，失败 {failed}。\n审计记录：{auditPath}", "操作完成", MessageBoxButtons.OK, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        finally { SetBusy(false); }
    }

    private void ShowSecurityDiagnostics()
    {
        var admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        var message = $"当前权限：{(admin ? "管理员" : "标准用户")}\n\n" +
            "如果文件访问被拦截：\n" +
            "1. 先确认你对该员工设备和数据拥有组织授权。\n" +
            "2. 关闭本工具，右键 EXE → 以管理员身份运行。\n" +
            "3. 若为“受控文件夹访问”、杀毒软件或企业 EDR 拦截，请联系 IT/安全管理员按组织流程临时放行本工具。\n" +
            "4. 本工具不会关闭、绕过或篡改 Windows 安全中心、杀毒软件、EDR、BitLocker 或访问控制。\n\n" +
            "是否打开 Windows 安全中心？";
        if (MessageBox.Show(message, "权限与安全拦截诊断", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
        {
            try { Process.Start(new ProcessStartInfo("windowsdefender:") { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show("无法打开安全中心：" + ex.Message); }
        }
    }

    private List<ScanResult> SelectedItems()
    {
        _grid.EndEdit();
        return _results.Where(x => x.Selected).ToList();
    }

    private void BrowseRoot()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = _root.Text, Description = "选择已获授权的扫描目录", UseDescriptionForTitle = true };
        if (dialog.ShowDialog() == DialogResult.OK) _root.Text = dialog.SelectedPath;
    }

    private static void OpenContainingFolder(string path)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); } catch { }
    }

    private void SetBusy(bool busy)
    {
        _scan.Enabled = !busy; _stop.Enabled = busy; _backup.Enabled = !busy; _quarantine.Enabled = !busy; _recycle.Enabled = !busy;
        _root.Enabled = !busy; _keywords.Enabled = !busy; _contents.Enabled = !busy; _correlation.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void UpdateSummary()
    {
        var severe = _results.Count(x => x.Level == PrivacyLevel.严重);
        var high = _results.Count(x => x.Level == PrivacyLevel.高);
        _summary.Text = $"{_results.Count:N0} 个结果 · 严重 {severe} · 高 {high}";
    }

    private static string CompactPath(string path) => path.Length > 100 ? "…" + path[^99..] : path;
}
