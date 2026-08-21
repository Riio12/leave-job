# 离职隐私卫士

面向已获授权的员工离职设备清理场景的 Windows 本地工具。程序不联网，默认不删除文件，也不会关闭或绕过终端防护。

## 下载

请从仓库右侧的 **Releases** 下载 `leave-job-win-x64.zip`。解压后把 EXE 复制到启用了 BitLocker To Go 的 U 盘，插入 Windows 10/11 x64 电脑后双击运行。

> Windows 默认禁止普通 U 盘自动运行 EXE，因此本工具采用“插入后双击运行”。

## 功能

- 按姓名、邮箱、工号等关键词搜索文件名与内容。
- 支持文本、CSV、JSON、XML、日志和 Office Open XML（DOCX/XLSX/PPTX）。
- 隐私相关性规则：身份证件、银行卡、账号口令、手机号、邮箱、地址、薪酬人事、医疗健康。
- 按严重/高/中/低等级及相关度排序。
- 勾选后生成 ZIP 备份，附原路径、SHA-256、分类和时间清单。
- 可将文件移动到隐藏隔离区，保留 `audit.json`，避免误删后无法恢复。
- 可移至 Windows 回收站，并在用户本地应用数据目录保存审计记录。
- 提供权限诊断、管理员运行指引和 Windows 安全中心入口。
- 自包含单文件发布，无需目标电脑安装 .NET。

## 安全边界

本工具不提供关闭或绕过 Windows Defender、安全中心、EDR、BitLocker、文件 ACL 或其他安全控制的能力。遇到拦截时，应由 IT/安全管理员在已授权范围内处置。

使用前应获得组织授权并明确数据范围。扫描结果必须人工复核；相关性等级不等于法律定性。建议始终先备份、再隔离。

## 本地构建

需要 .NET 8 SDK。在 Windows PowerShell 中运行：

```powershell
cd src\ExitPrivacyGuard
Set-ExecutionPolicy -Scope Process Bypass
.\build-windows.ps1
```

输出位于 `src\ExitPrivacyGuard\publish\win-x64\离职隐私卫士.exe`。

GitHub Actions 也会在提交和 PR 时自动构建 Windows x64 便携包。

## 后续增强建议

1. 由法务/安全团队维护可签名的关键词、规则和数据保留策略包。
2. 增加桌面、文档、下载、邮件归档和同步盘等扫描范围预设。
3. 为隔离或永久删除增加双人复核。
4. 增加读取隔离审计文件的一键恢复向导。
5. 对接组织证书或受管 BitLocker U 盘进行加密备份。
6. 经许可后增加 PDF、旧版 Office、PST/OST 等解析器。

## 隐私说明

扫描、匹配、哈希和备份均在本机完成，不会向网络上传文件名、文件内容或匹配到的隐私数据。
