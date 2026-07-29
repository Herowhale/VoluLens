using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace VoluLens.Core;

public sealed partial class HtmlReportBuilder
{
    public string Build(ScanResult result, bool redactUserPath, string? userRoot = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var findings = result.Findings.OrderByDescending(item => item.Bytes).ToArray();
        var safeBytes = findings.Where(item => item.Risk == RiskLevel.Safe).Sum(item => item.Bytes);
        var reviewBytes = findings.Where(item => item.Risk == RiskLevel.Review).Sum(item => item.Bytes);
        var protectedBytes = findings.Where(item => item.Risk == RiskLevel.Protected).Sum(item => item.Bytes);
        var rows = BuildFindingRows(findings, redactUserPath, userRoot);
        var denied = BuildDeniedRows(result.DeniedPaths, redactUserPath, userRoot);
        var roots = string.Join(
            " · ",
            result.Roots.Select(path => Encode(path, redactUserPath, userRoot)));

        return $$$"""
<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>VoluLens 存储分析报告</title>
<style>
:root{color-scheme:light;--ink:#202629;--muted:#6f7a7e;--paper:#fff;--canvas:#f3f4f1;--line:#dce1de;--green:#26745a;--amber:#a36c22;--red:#a94e4a;--blue:#4d718c}
*{box-sizing:border-box;letter-spacing:0}body{margin:0;background:var(--canvas);color:var(--ink);font-family:"Segoe UI","Microsoft YaHei UI",sans-serif}.page{width:min(1120px,calc(100% - 32px));margin:0 auto;padding:36px 0 52px}.masthead{border-bottom:1px solid var(--line);padding-bottom:24px}.eyebrow{font-size:11px;font-weight:800;letter-spacing:.12em;color:var(--green)}h1{font-size:clamp(28px,5vw,52px);line-height:1.02;margin:14px 0 10px}.meta{color:var(--muted);font-size:13px;line-height:1.7}.metrics{display:grid;grid-template-columns:1.2fr repeat(3,1fr);border:1px solid var(--line);background:var(--paper);margin:24px 0}.metric{padding:20px;border-right:1px solid var(--line)}.metric:last-child{border:0}.metric span{display:block;color:var(--muted);font-size:12px}.metric strong{display:block;margin-top:8px;font-size:25px}.metric.safe strong{color:var(--green)}.metric.review strong{color:var(--amber)}.metric.protected strong{color:var(--red)}section{margin-top:34px}h2{font-size:18px;margin:0 0 12px}.intro{color:var(--muted);line-height:1.7;margin:0 0 16px}.table{border:1px solid var(--line);background:var(--paper)}.row{display:grid;grid-template-columns:minmax(230px,2fr) 125px 145px;gap:18px;padding:15px 18px;border-bottom:1px solid var(--line);align-items:center}.row:last-child{border-bottom:0}.name{font-weight:700;overflow-wrap:anywhere}.path{font-family:Consolas,monospace;font-size:11px;color:var(--muted);margin-top:5px;overflow-wrap:anywhere}.reason{font-size:12px;color:var(--muted);line-height:1.5;margin-top:5px}.size{text-align:right;font-variant-numeric:tabular-nums}.risk{font-size:11px;font-weight:800;text-transform:uppercase}.risk.safe{color:var(--green)}.risk.review{color:var(--amber)}.risk.protected{color:var(--red)}.command{display:flex;gap:10px;align-items:center;padding:15px 18px;border:1px solid var(--line);background:#1e2325;color:#f6f7f4}.command code{min-width:0;flex:1;overflow-wrap:anywhere;font-size:12px}.command button{border:1px solid #667074;background:transparent;color:#fff;padding:8px 10px;cursor:pointer}.empty{padding:18px;color:var(--muted)}.notice{border-left:3px solid var(--blue);padding:12px 15px;background:#edf3f6;color:#465b68;line-height:1.6;font-size:13px}footer{margin-top:40px;padding-top:18px;border-top:1px solid var(--line);color:var(--muted);font-size:11px}@media(max-width:720px){.page{width:min(100% - 20px,1120px);padding-top:22px}.metrics{grid-template-columns:1fr 1fr}.metric:nth-child(2){border-right:0}.metric:nth-child(-n+2){border-bottom:1px solid var(--line)}.row{grid-template-columns:1fr}.size{text-align:left}.command{align-items:flex-start;flex-direction:column}}
</style>
</head>
<body>
<main class="page">
  <header class="masthead">
    <div class="eyebrow">VOLULENS 存储分析报告</div>
    <h1>磁盘空间分析</h1>
    <div class="meta">扫描范围：{{{roots}}}<br>完成时间：{{{result.CompletedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}}} · 用时 {{{Math.Max(0, (result.CompletedAt - result.StartedAt).TotalSeconds):0.0}}} 秒</div>
  </header>
  <div class="metrics">
    <div class="metric"><span>已扫描</span><strong>{{{FormatBytes(result.TotalBytes)}}}</strong></div>
    <div class="metric safe"><span>安全项</span><strong>{{{FormatBytes(safeBytes)}}}</strong></div>
    <div class="metric review"><span>需判断</span><strong>{{{FormatBytes(reviewBytes)}}}</strong></div>
    <div class="metric protected"><span>受保护</span><strong>{{{FormatBytes(protectedBytes)}}}</strong></div>
  </div>
  <section>
    <h2>占用排行</h2>
    <p class="intro">结果按扫描到的一级项目汇总。大小为扫描估算值，未读取到的目录不计入总量。</p>
    <div class="table">{{{rows}}}</div>
  </section>
  <section>
    <h2>Windows 维护建议</h2>
    <p class="intro">下面的命令只用于 Windows 组件存储维护。请在管理员终端中由你本人确认后运行。</p>
    <div class="command"><code id="dism-command">DISM /Online /Cleanup-Image /StartComponentCleanup</code><button type="button" onclick="copyCommand()">复制命令</button></div>
  </section>
  <section>
    <h2>未能读取的目录</h2>
    <div class="notice">这些目录可能因权限、长路径或正在使用而未计入。报告没有尝试提升权限，也没有修改任何文件。</div>
    <div class="table" style="margin-top:10px">{{{denied}}}</div>
  </section>
  <footer>这是只读离线报告。它不包含 VoluLens 桌面桥接、删除能力、遥测或远程资源。</footer>
</main>
<script>
function copyCommand(){var value=document.getElementById('dism-command').textContent;navigator.clipboard&&navigator.clipboard.writeText(value);}
</script>
</body>
</html>
""";
    }

    private static string BuildFindingRows(
        IEnumerable<ScanFinding> findings,
        bool redactUserPath,
        string? userRoot)
    {
        var builder = new StringBuilder();
        foreach (var finding in findings)
        {
            var riskClass = finding.Risk.ToString().ToLowerInvariant();
            var riskLabel = finding.Risk switch
            {
                RiskLevel.Safe => "安全",
                RiskLevel.Review => "需判断",
                _ => "受保护"
            };

            builder.Append("<div class=\"row\"><div><div class=\"name\">")
                .Append(Encode(finding.Name, redactUserPath, userRoot))
                .Append("</div><div class=\"path\">")
                .Append(Encode(finding.Path, redactUserPath, userRoot))
                .Append("</div><div class=\"reason\">")
                .Append(Encode(finding.Reason, redactUserPath, userRoot))
                .Append("</div></div><div class=\"size\">")
                .Append(FormatBytes(finding.Bytes))
                .Append("</div><div class=\"risk ")
                .Append(riskClass)
                .Append("\">")
                .Append(riskLabel)
                .Append(" · ")
                .Append(Encode(finding.Category, redactUserPath, userRoot))
                .Append("</div></div>");
        }

        return builder.Length == 0 ? "<div class=\"empty\">没有扫描结果。</div>" : builder.ToString();
    }

    private static string BuildDeniedRows(
        IEnumerable<string> paths,
        bool redactUserPath,
        string? userRoot)
    {
        var values = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (values.Length == 0)
        {
            return "<div class=\"empty\">没有遗漏目录。</div>";
        }

        return string.Concat(values.Select(path =>
            $"<div class=\"row\"><div class=\"path\">{Encode(path, redactUserPath, userRoot)}</div><div></div><div class=\"risk review\">未扫描</div></div>"));
    }

    private static string Encode(string value, bool redactUserPath, string? userRoot)
    {
        var display = redactUserPath ? RedactUserProfile(value, userRoot) : value;
        return WebUtility.HtmlEncode(display);
    }

    private static string RedactUserProfile(string value, string? userRoot)
    {
        if (string.IsNullOrWhiteSpace(userRoot))
        {
            return UserProfileRegex().Replace(value, "%USERPROFILE%");
        }

        var root = Path.TrimEndingDirectorySeparator(userRoot);
        return Regex.Replace(
            value,
            Regex.Escape(root) + @"(?=$|[\\/])",
            "%USERPROFILE%",
            RegexOptions.IgnoreCase);
    }

    private static string FormatBytes(long bytes)
    {
        var value = Math.Max(0, bytes);
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var amount = (double)value;
        var unit = 0;
        while (amount >= 1024 && unit < units.Length - 1)
        {
            amount /= 1024;
            unit++;
        }

        return unit == 0 ? $"{amount:0} {units[unit]}" : $"{amount:0.0} {units[unit]}";
    }

    [GeneratedRegex("[A-Za-z]:\\\\Users\\\\[^\\\\/<>\\\"']+", RegexOptions.IgnoreCase)]
    private static partial Regex UserProfileRegex();
}
