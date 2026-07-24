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

internal sealed record SdkRepairResult(
    bool Success,
    string Architecture,
    string? FoundPath,
    string? Message,
    SdkHealthResult HealthResult);

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

    public static SdkRepairResult Repair()
    {
        var architecture = Environment.Is64BitProcess ? "x64" : "x86";
        var baseDir = AppContext.BaseDirectory;

        var candidates = new List<string>
        {
            Path.Combine(baseDir, "dll", architecture),
            Path.Combine(baseDir, "dll"),
            Path.Combine(baseDir, "..", "dll", architecture),
            Path.Combine(baseDir, "..", "dll"),
            Path.Combine(baseDir, "sdk", architecture),
            Path.Combine(baseDir, "..", "docs", "脱机通讯开发包-6.3.1.55", "SDK", architecture)
        };

        string? sourceDir = null;
        foreach (var dir in candidates)
        {
            if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, "zkemkeeper.dll")))
            {
                sourceDir = Path.GetFullPath(dir);
                break;
            }
        }

        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var targetSystemDir = Environment.Is64BitProcess
            ? Path.Combine(windir, "System32")
            : (Environment.Is64BitOperatingSystem ? Path.Combine(windir, "SysWOW64") : Path.Combine(windir, "System32"));

        if (sourceDir is null)
        {
            var existingInSys = Path.Combine(targetSystemDir, "zkemkeeper.dll");
            if (File.Exists(existingInSys))
            {
                sourceDir = targetSystemDir;
            }
            else
            {
                var searched = string.Join("\n", candidates.Distinct());
                return new SdkRepairResult(
                    false,
                    architecture,
                    null,
                    $"未找到与当前 {architecture} 架构匹配的 DLL 来源目录。已搜索路径：\n{searched}",
                    Check());
            }
        }

        var targetDllPath = Path.Combine(targetSystemDir, "zkemkeeper.dll");
        var regsvr32Path = Path.Combine(targetSystemDir, "regsvr32.exe");

        try
        {
            var psCommand = $"Copy-Item -Path '{sourceDir}\\*.dll' -Destination '{targetSystemDir}' -Force; Start-Process '{regsvr32Path}' -ArgumentList '/s', '\"{targetDllPath}\"' -Wait";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                WorkingDirectory = sourceDir,
                UseShellExecute = true,
                Verb = "runas"
            };

            using var process = Process.Start(startInfo);
            if (process is not null)
            {
                process.WaitForExit(20000);
            }

            var postCheck = Check();
            if (postCheck.IsHealthy)
            {
                return new SdkRepairResult(
                    true,
                    architecture,
                    targetDllPath,
                    $"已成功将 DLL 复制到 {targetSystemDir} 并完成 COM SDK 注册：\n{targetDllPath}",
                    postCheck);
            }

            var errors = string.Join("\n", postCheck.Errors);
            return new SdkRepairResult(
                false,
                architecture,
                targetDllPath,
                $"已执行 DLL 复制与 regsvr32 注册，但 SDK 健康检查仍未通过：\n{errors}",
                postCheck);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new SdkRepairResult(
                false,
                architecture,
                targetDllPath,
                "用户取消了管理员权限提权请求，重新注册已中止。",
                Check());
        }
        catch (Exception ex)
        {
            return new SdkRepairResult(
                false,
                architecture,
                targetDllPath,
                $"复制并注册 DLL 时发生异常：{ex.Message}",
                Check());
        }
    }

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
