using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace GearShift.App.Services;

public sealed record UpdateInfo(
    Version Version,
    string Tag,
    string Name,
    string Notes,
    string DownloadUrl,
    string Sha256);

public static class UpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/zhnq/gearshift/releases/latest";
    private static readonly HttpClient Client = CreateClient();

    public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        using var response = await Client.GetAsync(LatestReleaseUrl, ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var root = json.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V').Split('-')[0], out var latest) || latest <= CurrentVersion)
            return null;
        var asset = root.GetProperty("assets").EnumerateArray().FirstOrDefault(x =>
            (x.GetProperty("name").GetString() ?? "").EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase));
        if (asset.ValueKind == JsonValueKind.Undefined) return null;
        var digest = asset.TryGetProperty("digest", out var digestElement)
            ? digestElement.GetString() ?? ""
            : "";
        if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || digest.Length != 71)
            throw new InvalidDataException("GitHub Release 未提供有效的 SHA-256 摘要，已拒绝不安全更新");
        return new UpdateInfo(
            latest,
            tag,
            root.GetProperty("name").GetString() ?? tag,
            root.GetProperty("body").GetString() ?? "",
            asset.GetProperty("browser_download_url").GetString()!,
            digest[7..]);
    }

    public static async Task ApplyAsync(UpdateInfo update, CancellationToken ct = default)
    {
        var staging = Path.Combine(Path.GetTempPath(), "GearShift-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var zip = Path.Combine(staging, "update.zip");
        await using (var source = await Client.GetStreamAsync(update.DownloadUrl, ct))
        await using (var target = File.Create(zip))
            await source.CopyToAsync(target, ct);

        await using (var downloaded = File.OpenRead(zip))
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(downloaded, ct));
            if (!string.Equals(actual, update.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(staging, recursive: true);
                throw new InvalidDataException($"更新包校验失败。期望 {update.Sha256}，实际 {actual}");
            }
        }

        var payload = Path.Combine(staging, "payload");
        ZipFile.ExtractToDirectory(zip, payload, overwriteFiles: true);
        var script = Path.Combine(staging, "apply-update.ps1");
        await File.WriteAllTextAsync(script, UpdateScript, ct);
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("无法定位 GearShift.exe");
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script,
                     "-ProcessId", Environment.ProcessId.ToString(), "-Source", payload,
                     "-Target", AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), "-Exe", exe })
            psi.ArgumentList.Add(arg);
        _ = Process.Start(psi) ?? throw new InvalidOperationException("无法启动更新器");
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GearShift", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private const string UpdateScript = """
param([int]$ProcessId, [string]$Source, [string]$Target, [string]$Exe)
$ErrorActionPreference = 'Stop'
try { Wait-Process -Id $ProcessId -Timeout 45 -ErrorAction SilentlyContinue } catch {}
Start-Sleep -Milliseconds 500
Copy-Item -Path (Join-Path $Source '*') -Destination $Target -Recurse -Force
Start-Process -FilePath $Exe
Remove-Item -LiteralPath (Split-Path $Source -Parent) -Recurse -Force -ErrorAction SilentlyContinue
""";
}
