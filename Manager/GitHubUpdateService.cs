using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZktecoRelay.Manager;

internal sealed record UpdateSettings(string Repository, string ProxyPrefix);
internal sealed record UpdateAsset(string Name, string DownloadUrl, long Size);
internal sealed record UpdateInfo(
    Version CurrentVersion,
    Version LatestVersion,
    string TagName,
    string ReleaseName,
    string ReleaseNotes,
    string ReleasePageUrl,
    DateTimeOffset PublishedAt,
    UpdateAsset Package,
    UpdateAsset? Checksum,
    bool IsUpdateAvailable);

internal static class GitHubUpdateService
{
    private const string DefaultRepository = "NicoChiGu/zkteco-realy";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

    public static UpdateSettings NormalizeSettings(string? repository, string? proxyPrefix)
    {
        var normalizedRepository = string.IsNullOrWhiteSpace(repository)
            ? DefaultRepository
            : repository.Trim().Trim('/');

        var normalizedProxy = string.IsNullOrWhiteSpace(proxyPrefix)
            ? string.Empty
            : proxyPrefix.Trim().TrimEnd('/') + "/";

        return new UpdateSettings(normalizedRepository, normalizedProxy);
    }

    public static async Task<UpdateInfo> CheckAsync(UpdateSettings settings, CancellationToken cancellationToken)
    {
        ValidateRepository(settings.Repository);
        var apiUrl = $"https://api.github.com/repos/{settings.Repository}/releases/latest";
        using var response = await HttpClient.GetAsync(ApplyProxy(settings.ProxyPrefix, apiUrl), cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken)
                      ?? throw new InvalidOperationException("GitHub Release 响应为空。" );

        var latestVersion = ParseVersion(release.TagName);
        var architecture = Environment.Is64BitProcess ? "win-x64" : "win-x86";
        var packageName = $"zkteco-relay-{architecture}-setup.exe";
        var checksumName = packageName + ".sha256";

        var package = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, packageName, StringComparison.OrdinalIgnoreCase));
        if (package is null)
        {
            throw new InvalidOperationException($"Release '{release.TagName}' 中未找到 {packageName}。" );
        }

        var checksum = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, checksumName, StringComparison.OrdinalIgnoreCase));

        return new UpdateInfo(
            CurrentVersion,
            latestVersion,
            release.TagName,
            string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name,
            release.Body ?? string.Empty,
            release.HtmlUrl,
            release.PublishedAt,
            new UpdateAsset(package.Name, package.BrowserDownloadUrl, package.Size),
            checksum is null ? null : new UpdateAsset(checksum.Name, checksum.BrowserDownloadUrl, checksum.Size),
            latestVersion > CurrentVersion);
    }

    public static async Task<string> DownloadAsync(
        UpdateSettings settings,
        UpdateInfo update,
        string destinationDirectory,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);
        var destinationPath = Path.Combine(destinationDirectory, update.Package.Name);
        var temporaryPath = destinationPath + ".download";

        await DownloadFileAsync(
            ApplyProxy(settings.ProxyPrefix, update.Package.DownloadUrl),
            temporaryPath,
            update.Package.Size,
            progress,
            cancellationToken);

        if (update.Checksum is not null)
        {
            var checksumText = await HttpClient.GetStringAsync(
                ApplyProxy(settings.ProxyPrefix, update.Checksum.DownloadUrl),
                cancellationToken);
            var expected = checksumText.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(expected))
            {
                throw new InvalidOperationException("SHA-256 校验文件内容无效。" );
            }

            await using var file = File.OpenRead(temporaryPath);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken));
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(temporaryPath);
                throw new InvalidOperationException("更新包 SHA-256 校验失败，文件可能损坏或被篡改。" );
            }
        }

        File.Move(temporaryPath, destinationPath, overwrite: true);
        return destinationPath;
    }

    public static void OpenReleasePage(UpdateInfo update) =>
        Process.Start(new ProcessStartInfo(update.ReleasePageUrl) { UseShellExecute = true });

    public static void LaunchInstaller(string installerPath)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("安装程序不存在。", installerPath);
        }

        Process.Start(new ProcessStartInfo(installerPath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(installerPath) ?? AppContext.BaseDirectory
        });
    }

    private static async Task DownloadFileAsync(
        string url,
        string destinationPath,
        long expectedSize,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? expectedSize;

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(destinationPath);
        var buffer = new byte[1024 * 128];
        long written = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            written += read;
            if (total > 0)
            {
                progress?.Report((int)Math.Min(100, written * 100 / total));
            }
        }
    }

    private static string ApplyProxy(string proxyPrefix, string originalUrl) =>
        string.IsNullOrEmpty(proxyPrefix) ? originalUrl : proxyPrefix + originalUrl;

    private static Version ParseVersion(string tagName)
    {
        var value = tagName.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        var separator = value.IndexOfAny(['-', '+']);
        if (separator >= 0)
        {
            value = value[..separator];
        }

        return Version.TryParse(value, out var version)
            ? version
            : throw new InvalidOperationException($"无法解析 Release 标签版本：{tagName}" );
    }

    private static void ValidateRepository(string repository)
    {
        var parts = repository.Split('/');
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("GitHub 仓库格式必须为 owner/repository。" );
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZktecoRelay-Manager", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("published_at")] DateTimeOffset PublishedAt,
        [property: JsonPropertyName("assets")] List<GitHubAsset> Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl,
        [property: JsonPropertyName("size")] long Size);
}
