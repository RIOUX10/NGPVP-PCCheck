using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using NGPVP.Shared;

namespace NGPVP.Agent.Services;

public sealed class PcScanner
{
    private readonly string[] suspiciousTerms =
    {
        "inject", "mapper", "executor", "modmenu", "cheat", "trainer", "xenos", "extremeinjector"
    };

    private readonly HashSet<string> knownProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "RTSSHooksLoader64", "RTSSHooksLoader", "RTSS", "RivaTunerStatisticsServer",
        "EasyAntiCheat", "EasyAntiCheat_EOS", "EAAntiCheatGameService", "EADesktop",
        "vgc", "vgtray", "RiotClientServices"
    };

    private readonly HashSet<string> knownServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "EasyAntiCheat", "EasyAntiCheat_EOS", "EAAntiCheatService", "PcaSvc",
        "DenuvoAntiCheat", "Denuvo Anti-Cheat Update Service", "BITS", "wuauserv"
    };

    public async IAsyncEnumerable<ScanResult> ScanAsync(
        string? fivemPath,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new("System", "Environment", FindingSeverity.Info,
            $"Machine: {Environment.MachineName} | User: {Environment.UserName}");
        await Task.Delay(80, ct);

        foreach (var r in ScanProcesses())
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
            await Task.Delay(8, ct);
        }

        foreach (var r in ScanStartup())
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
            await Task.Delay(8, ct);
        }

        foreach (var r in ScanServices())
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
            await Task.Delay(8, ct);
        }

        foreach (var r in ScanFiveM(fivemPath))
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
            await Task.Delay(8, ct);
        }

        foreach (var r in ScanRecentExecutables())
        {
            ct.ThrowIfCancellationRequested();
            yield return r;
            await Task.Delay(8, ct);
        }
    }

    private IEnumerable<ScanResult> ScanProcesses()
    {
        foreach (var p in Process.GetProcesses())
        {
            string? name = null;
            string? path = null;
            try
            {
                name = p.ProcessName;
                try { path = p.MainModule?.FileName; } catch { }
            }
            catch { }
            finally { p.Dispose(); }

            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (knownProcesses.Contains(name))
            {
                yield return new("Processes", name, FindingSeverity.Known,
                    $"Known/whitelisted application. Path: {path ?? "not accessible"}");
                continue;
            }

            var hit = suspiciousTerms.FirstOrDefault(t =>
                name.Contains(t, StringComparison.OrdinalIgnoreCase));

            if (hit is not null)
            {
                var evidence = BuildEvidence(path);
                yield return new("Processes", name, evidence.Severity,
                    $"Process name matched heuristic '{hit}'. {evidence.Details}");
            }
        }

        yield return new("Processes", "Process scan", FindingSeverity.Clean,
            "Running processes checked against local heuristics and whitelist.");
    }

    private IEnumerable<ScanResult> ScanStartup()
    {
        foreach (var hive in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
        {
            Microsoft.Win32.RegistryKey? key = null;
            try
            {
                key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                if (key is null)
                    continue;

                foreach (var valueName in key.GetValueNames())
                {
                    var value = key.GetValue(valueName)?.ToString() ?? string.Empty;
                    var match = suspiciousTerms.FirstOrDefault(t => value.Contains(t, StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                    {
                        yield return new("Startup", valueName, FindingSeverity.Suspicious,
                            $"Startup command matched heuristic '{match}': {value}");
                    }
                }
            }
            finally
            {
                key?.Dispose();
            }
        }

        yield return new("Startup", "Startup entries", FindingSeverity.Clean,
            "Common Run registry locations checked.");
    }

    private IEnumerable<ScanResult> ScanServices()
    {
        var findings = new List<ScanResult>();

        try
        {
            foreach (var svc in System.ServiceProcess.ServiceController.GetServices())
            {
                try
                {
                    if (knownServices.Contains(svc.ServiceName))
                    {
                        findings.Add(new("Services", svc.ServiceName, FindingSeverity.Known,
                            "Known/whitelisted Windows or anti-cheat service."));
                        continue;
                    }

                    var hit = suspiciousTerms.FirstOrDefault(t =>
                        svc.ServiceName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        svc.DisplayName.Contains(t, StringComparison.OrdinalIgnoreCase));

                    if (hit is not null)
                    {
                        findings.Add(new("Services", svc.DisplayName, FindingSeverity.Suspicious,
                            $"Service name matched heuristic '{hit}'. Manual verification required."));
                    }
                }
                finally
                {
                    svc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            findings.Add(new("Services", "Service scan", FindingSeverity.Info,
                $"Service enumeration could not be completed: {ex.Message}"));
        }

        foreach (var finding in findings)
            yield return finding;

        yield return new("Services", "Windows services", FindingSeverity.Clean,
            "Service names checked against local heuristics and whitelist.");
    }

    private IEnumerable<ScanResult> ScanFiveM(string? fivemPath)
    {
        var path = fivemPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var candidates = new[]
            {
                System.IO.Path.Combine(local, "FiveM"),
                System.IO.Path.Combine(local, "FiveM", "FiveM.app"),
                System.IO.Path.Combine(roaming, "CitizenFX")
            };
            path = candidates.FirstOrDefault(System.IO.Directory.Exists);
        }

        if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path))
        {
            yield return new("FiveM", "FiveM path", FindingSeverity.Info,
                "FiveM directory was not found in standard locations.");
            yield break;
        }

        yield return new("FiveM", "FiveM installation", FindingSeverity.Clean, path);

        IEnumerable<string> exeFiles;
        ScanResult? enumerationError = null;
        try
        {
            exeFiles = System.IO.Directory.EnumerateFiles(
                path, "*.exe", System.IO.SearchOption.AllDirectories).Take(300).ToList();
        }
        catch (Exception ex)
        {
            exeFiles = Array.Empty<string>();
            enumerationError = new("FiveM", "File scan", FindingSeverity.Info,
                $"Could not fully enumerate FiveM files: {ex.Message}");
        }

        if (enumerationError is not null)
        {
            yield return enumerationError;
            yield break;
        }

        foreach (var file in exeFiles)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(file);
            if (knownProcesses.Contains(name))
            {
                yield return new("FiveM", name, FindingSeverity.Known, file);
                continue;
            }

            var hit = suspiciousTerms.FirstOrDefault(t => name.Contains(t, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                var evidence = BuildEvidence(file);
                yield return new("FiveM", name, evidence.Severity,
                    $"File name matched heuristic '{hit}'. {evidence.Details} Path: {file}");
            }
        }

        yield return new("FiveM", "File structure", FindingSeverity.Clean,
            "FiveM executable files checked against local heuristics and whitelist.");
    }

    private IEnumerable<ScanResult> ScanRecentExecutables()
    {
        var roots = new[]
        {
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            System.IO.Path.GetTempPath()
        };

        foreach (var root in roots.Where(System.IO.Directory.Exists))
        {
            List<string> files;
            try
            {
                files = System.IO.Directory.EnumerateFiles(
                    root, "*.exe", System.IO.SearchOption.TopDirectoryOnly).Take(250).ToList();
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(file);
                var hit = suspiciousTerms.FirstOrDefault(t => name.Contains(t, StringComparison.OrdinalIgnoreCase));
                if (hit is not null)
                {
                    var evidence = BuildEvidence(file);
                    yield return new("Recent files", name, evidence.Severity,
                        $"Recent executable matched heuristic '{hit}'. {evidence.Details} Path: {file}");
                }
            }
        }

        yield return new("Recent files", "Recent executable scan", FindingSeverity.Clean,
            "Downloads and temporary executable names checked.");
    }

    private static (FindingSeverity Severity, string Details) BuildEvidence(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            return (FindingSeverity.Suspicious, "Path unavailable; manual verification required.");

        var publisher = TryGetPublisher(path);
        var hash = TryGetSha256(path);
        var signature = publisher is null ? "Signature: unavailable" : $"Publisher: {publisher}";

        return (FindingSeverity.Suspicious,
            $"{signature} | SHA-256: {hash ?? "unavailable"} | Manual verification required.");
    }

    private static string? TryGetPublisher(string path)
    {
        try
        {
            var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path));
            return cert.GetNameInfo(System.Security.Cryptography.X509Certificates.X509NameType.SimpleName, false);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetSha256(string path)
    {
        try
        {
            using var stream = System.IO.File.OpenRead(path);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream));
        }
        catch
        {
            return null;
        }
    }
}
