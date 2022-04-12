using System.CommandLine;
using System.CommandLine.IO;
using System.Diagnostics;

class ContainerEngine
{
    public string Command { get; }
    public Version Version { get; }

    public bool SupportsCacheMount
    {
        get
        {
            if (Command == "podman")
            {
                return Version.Major >= 4;
            }
            return true;
        }
    }

    private ContainerEngine(string command, Version version)
    {
        Command = command;
        Version = version;
    }

    public static ContainerEngine? TryCreate()
    {
        string? command = null;
        Version? version = null;
        foreach (var cmd in new[] { "podman", "docker" })
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = cmd,
                    ArgumentList = { "version", "-f", "{{ .Client.Version }}" },
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                })!;
                process.WaitForExit();
                string stdout = process.StandardOutput.ReadToEnd().Trim();
                Version.TryParse(stdout, out version);
                command = cmd;
                break;
            }
            catch
            { }
        }

        if (command is null)
        {
            return null;
        }

        return new ContainerEngine(command, version ?? new Version());
    }

    public bool TryBuild(IConsole console, string dockerFileName, string tag, string contextDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Command,
            ArgumentList = {  "build", "-f", dockerFileName, "-t", tag, "." },
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(psi)!;

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                console.Error.WriteLine(e.Data);
            }
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                console.Out.WriteLine(e.Data);
            }
        };

        process.StandardInput.Close();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        process.WaitForExit();

        return process.ExitCode == 0;
    }
}