using System.Diagnostics;

namespace EntraRdpConnect.Infrastructure.Processes;

/// <summary>
/// Asynkron supervisor rundt én ekstern prosess. Streamer stdout/stderr linje for linje,
/// lar oss skrive til stdin, og rydder opp (dreper hele prosess-treet) ved kansellering.
///
/// FreeRDP-innloggingen driver stdin/stdout interaktivt: appen ser etter "Browse to:" på en
/// av strømmene, og skriver retur-koden tilbake på stdin. Derfor eksponerer vi begge strømmene
/// som events framfor å bare samle dem opp.
///
/// Dette er en teknisk primitiv (OS-detalj) og bor derfor i infrastrukturlaget. Adapterne
/// UwgVpnController, FreeRdpSession osv. bygger på den.
/// </summary>
public sealed class ProcessRunner : IAsyncDisposable
{
    private readonly Process _process;
    private readonly bool _redirectStdin;
    private Task _stdoutPump = Task.CompletedTask;
    private Task _stderrPump = Task.CompletedTask;

    /// <summary>En linje fra stdout (uten linjeskift).</summary>
    public event Action<string>? StdoutLine;

    /// <summary>En linje fra stderr (uten linjeskift).</summary>
    public event Action<string>? StderrLine;

    private ProcessRunner(Process process, bool redirectStdin)
    {
        _process = process;
        _redirectStdin = redirectStdin;
    }

    public int ProcessId => _process.Id;
    public bool HasExited => _process.HasExited;

    /// <summary>
    /// Starter prosessen og begynner å pumpe output. Handlerne kobles til FØR pumpene starter,
    /// så vi ikke mister tidlige linjer fra en rask prosess.
    /// </summary>
    public static ProcessRunner Start(
        ProcessSpec spec,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = spec.FileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = spec.RedirectStdin,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in spec.Arguments)
            psi.ArgumentList.Add(arg);
        if (spec.Environment is not null)
            foreach (var (key, value) in spec.Environment)
                psi.Environment[key] = value;

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var runner = new ProcessRunner(process, spec.RedirectStdin);
        if (onStdout is not null) runner.StdoutLine += onStdout;
        if (onStderr is not null) runner.StderrLine += onStderr;

        process.Start();

        // Pumpene startes etter at handlerne er koblet på — ingen race på tidlige linjer.
        runner._stdoutPump = runner.PumpAsync(process.StandardOutput, isError: false);
        runner._stderrPump = runner.PumpAsync(process.StandardError, isError: true);
        return runner;
    }

    private async Task PumpAsync(StreamReader reader, bool isError)
    {
        // ReadLineAsync fanger ikke en prompt uten linjeskift ("Paste redirect URL here:"),
        // men trigger-linja vi trenger ("Browse to:") er linjeterminert. Vi kan trygt skrive
        // koden til stdin så snart vi har sett "Browse to:" — den ligger i pipe-bufferet til
        // FreeRDP leser den.
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            if (isError) StderrLine?.Invoke(line);
            else StdoutLine?.Invoke(line);
        }
    }

    /// <summary>Skriver én linje til prosessens stdin og flusher.</summary>
    public async Task WriteStdinLineAsync(string line, CancellationToken ct = default)
    {
        if (!_redirectStdin)
            throw new InvalidOperationException("Stdin er ikke omdirigert for denne prosessen.");

        await _process.StandardInput.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
        await _process.StandardInput.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Lukker stdin (sender EOF til prosessen).</summary>
    public void CloseStdin()
    {
        if (_redirectStdin && !_process.HasExited)
            _process.StandardInput.Close();
    }

    /// <summary>
    /// Venter på at prosessen avslutter og at output er ferdig drenert. Ved kansellering drepes
    /// hele prosess-treet før unntaket kastes videre.
    /// </summary>
    public async Task<int> WaitForExitAsync(CancellationToken ct = default)
    {
        try
        {
            await _process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill();
            await DrainPumpsAsync().ConfigureAwait(false);
            throw;
        }

        await DrainPumpsAsync().ConfigureAwait(false);
        return _process.ExitCode;
    }

    private async Task DrainPumpsAsync()
    {
        try { await Task.WhenAll(_stdoutPump, _stderrPump).ConfigureAwait(false); }
        catch (OperationCanceledException) { /* pumpene avsluttes når strømmene lukkes */ }
    }

    private void TryKill()
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { /* prosessen rakk å avslutte selv */ }
    }

    /// <summary>Kjører en prosess til slutt og samler stdout/stderr. For korte kommandoer
    /// (dig, uwg-status) der vi bare vil ha resultatet.</summary>
    public static async Task<ProcessResult> RunToCompletionAsync(ProcessSpec spec, CancellationToken ct = default)
    {
        var stdout = new List<string>();
        var stderr = new List<string>();

        await using var runner = Start(
            spec,
            onStdout: line => { lock (stdout) stdout.Add(line); },
            onStderr: line => { lock (stderr) stderr.Add(line); });

        var exitCode = await runner.WaitForExitAsync(ct).ConfigureAwait(false);
        return new ProcessResult(exitCode, stdout, stderr);
    }

    public async ValueTask DisposeAsync()
    {
        TryKill();
        await DrainPumpsAsync().ConfigureAwait(false);
        _process.Dispose();
    }
}
