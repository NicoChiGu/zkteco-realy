using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ZktecoRelay.Manager;

internal sealed record SdkHealthResult(
    bool IsHealthy,
    string Architecture,
    string? ComServerPath,
    string? FileVersion,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Details);

internal static class SdkHealthChecker
{
    private static readonly string[] CoreDependencies =
    [
        "zkemkeeper.dll",
        "zkemsdk.dll",
        "zkemsdkutils.dll",
        "commpro.dll",
        "comms.dll",
        "tcpcomm.dll"
    ];

    private static readonly string[] OptionalDependencies =
    [
        "ZKEMCrypto.dll",
        "ZKCommuCryptoClient.dll",
        "plcommpro.dll",
        "plcomms.dll",
        "plcommutils.dll",
        "pltcpcomm.dll",
        "p4p.dll",
        "p4pcomm.dll",
        "rscomm.dll",
        "usbcomm.dll"
    ];

    public static SdkHealthResult Check()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var details = new List<string>();
        var architecture = Environment.Is64BitProcess ? "x64" : "x86";
        string? comServerPath = null;
        string? fileVersion = null;

        details.Add($"管理器进程架构：{architecture}");
        details.Add($"操作系统架构：{(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");

        Type? comType = null;
        string? selectedProgId = null;
        foreach (var progId in new[] { "zkemkeeper.ZKEM.1", "zkemkeeper.ZKEM" })
        {
            try
            {
                comType = Type.GetTypeFromProgID(progId, throwOnError: false);
                if (comType is not null)
                {
                    selectedProgId = progId;
                    break;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"读取 COM ProgID {progId} 时发生异常：{ex.Message}");
            }
        }

        if (comType is null || selectedProgId is null)
        {
            errors.Add($"未在当前 {architecture} 注册表视图中找到 zkemkeeper COM。请安装并注册与程序位数一致的 SDK。");
            return new SdkHealthResult(false, architecture, null, null, errors, warnings, details);
        }

        details.Add($"COM ProgID：{selectedProgId}");
        details.Add($"COM CLSID：{comType.GUID:B}");

        try
        {
            var view = Environment.Is64BitProcess ? RegistryView.Registry64 : RegistryView.Registry32;
            using var classesRoot = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view);
            using var serverKey = classesRoot.OpenSubKey($@"CLSID\{comType.GUID:B}\InprocServer32");
            comServerPath = serverKey?.GetValue(null)?.ToString()?.Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(comServerPath))
            {
                errors.Add("COM 已注册，但注册表中缺少 InprocServer32 路径。");
            }
            else
            {
                details.Add($"COM DLL：{comServerPath}");
                if (!File.Exists(comServerPath))
                {
                    errors.Add($"COM 注册路径指向的 DLL 不存在：{comServerPath}");
                }
                else
                {
                    fileVersion = FileVersionInfo.GetVersionInfo(comServerPath).FileVersion;
                    if (!string.IsNullOrWhiteSpace(fileVersion))
                    {
                        details.Add($"zkemkeeper.dll 版本：{fileVersion}");
                    }

                    CheckDependencies(Path.GetDirectoryName(comServerPath)!, errors, warnings, details);
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"检查 COM 注册路径失败：{ex.Message}");
        }

        object? instance = null;
        try
        {
            instance = Activator.CreateInstance(comType);
            if (instance is null)
            {
                errors.Add("COM 类型已注册，但无法创建 zkemkeeper 实例。");
            }
            else
            {
                details.Add("COM 实例化测试：成功");
            }
        }
        catch (BadImageFormatException ex)
        {
            errors.Add($"DLL 位数与管理器进程不匹配：{ex.Message}");
        }
        catch (COMException ex)
        {
            errors.Add($"COM 实例化失败（0x{ex.HResult:X8}）：{ex.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"COM 实例化失败：{ex.Message}");
        }
        finally
        {
            if (instance is not null && Marshal.IsComObject(instance))
            {
                try
                {
                    Marshal.FinalReleaseComObject(instance);
                }
                catch
                {
                    // Health checking must not fail while releasing the probe object.
                }
            }
        }

        return new SdkHealthResult(errors.Count == 0, architecture, comServerPath, fileVersion, errors, warnings, details);
    }

    private static void CheckDependencies(
        string directory,
        ICollection<string> errors,
        ICollection<string> warnings,
        ICollection<string> details)
    {
        details.Add($"SDK 依赖目录：{directory}");

        foreach (var fileName in CoreDependencies)
        {
            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                errors.Add($"缺少关键 DLL：{fileName}");
            }
        }

        var missingOptional = OptionalDependencies
            .Where(fileName => !File.Exists(Path.Combine(directory, fileName)))
            .ToArray();

        if (missingOptional.Length > 0)
        {
            warnings.Add($"部分可选通信 DLL 不存在：{string.Join(", ", missingOptional)}。对应的 USB、串口、PULL 或加密功能可能不可用。");
        }
    }
}
