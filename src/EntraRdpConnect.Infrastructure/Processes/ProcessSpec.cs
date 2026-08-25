namespace EntraRdpConnect.Infrastructure.Processes;

/// <summary>Beskriver en prosess appen skal starte. Argumenter sendes som liste (ikke én streng),
/// så vi slipper shell-quoting og injeksjonsproblemer.</summary>
public sealed record ProcessSpec(string FileName, IReadOnlyList<string> Arguments)
{
    /// <summary>Sett til true når vi trenger å skrive til prosessens stdin (xfreerdp3-koden).</summary>
    public bool RedirectStdin { get; init; }

    /// <summary>Ekstra miljøvariabler (arves ellers fra appen).</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    public ProcessSpec(string fileName, params string[] arguments)
        : this(fileName, (IReadOnlyList<string>)arguments) { }
}

/// <summary>Resultatet av en prosess kjørt til slutt.</summary>
public sealed record ProcessResult(int ExitCode, IReadOnlyList<string> Stdout, IReadOnlyList<string> Stderr)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>Stdout + stderr slått sammen til én tekst — nyttig i feilmeldinger.</summary>
    public string CombinedOutput => string.Join('\n', Stdout.Concat(Stderr));
}
