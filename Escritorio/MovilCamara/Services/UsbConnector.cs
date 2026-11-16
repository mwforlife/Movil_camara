using System.Diagnostics;
using System.IO;

namespace MovilCamara.Services;

public static class UsbConnector
{
    public static string[] AuthorizedSerials(string output) => output.Split('\n')
        .Select(l => l.Trim().Split('\t'))
        .Where(p => p.Length == 2 && p[1] == "device" && !p[0].Contains(':') && !p[0].StartsWith("emulator-"))
        .Select(p => p[0]).ToArray();

    public static async Task<string> ConnectAsync(ConnectionHub hub, string adb, string serial)
    {
        await Run(adb, "-s", serial, "reverse", "tcp:47779", "tcp:47779");
        var result = await Run(adb, "-s", serial, "forward", "tcp:0", "tcp:47778");
        if (!int.TryParse(result.Trim(), out var port)) throw new IOException("ADB no devolvió un puerto válido.");
        try { return await hub.PairUsbAsync(port); }
        finally { await Run(adb, "-s", serial, "forward", "--remove", "tcp:" + port); }
    }
    public static string? FindAdb()
    {
        var sdk = Environment.GetEnvironmentVariable("ANDROID_HOME") ?? Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
        var paths = new[] {
            sdk == null ? "" : Path.Combine(sdk, "platform-tools", "adb.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", "adb.exe")
        };
        return paths.FirstOrDefault(File.Exists);
    }
    public static async Task<string> Run(string adb, params string[] args)
    {
        var info = new ProcessStartInfo(adb) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("No se pudo iniciar ADB.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw new IOException("ADB no respondió."); }
        var text = await output;
        var errors = await error;
        if (process.ExitCode != 0) throw new IOException(errors);
        return text;
    }
}
