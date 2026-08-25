using System.Text.Json;
using EntraRdpConnect.Core.Configuration;
using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.Infrastructure.Config;

/// <summary>Adapter for <see cref="IConfigProvider"/> som leser den lokale, ikke-innsjekkede
/// config-filen (~/.config/entra-rdp-connect/config.json).</summary>
public sealed class FileConfigProvider : IConfigProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _path;

    /// <summary>DI-konstruktør: bruker standard XDG-sti.</summary>
    public FileConfigProvider() : this(DefaultConfigPath()) { }

    /// <summary>Til test: eksplisitt sti.</summary>
    public FileConfigProvider(string path) => _path = path;

    public string Location => _path;

    public bool Exists => File.Exists(_path);

    public async Task<AppConfig> GetAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path))
            throw new FileNotFoundException(
                $"Fant ingen config på {_path}. Kopier config.sample.json dit og fyll inn verdier.", _path);

        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<AppConfig>(stream, JsonOptions, ct).ConfigureAwait(false)
            ?? throw new InvalidDataException($"Config-filen {_path} er tom eller ugyldig JSON.");
    }

    public async Task SaveAsync(AppConfig config, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        // Filen inneholder UPN og interne vertsnavn — ikke hemmeligheter, men heller ikke noe
        // andre brukere på maskinen trenger å se. Kun eier får lese/skrive.
        await using var stream = new FileStream(_path, new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        });
        await JsonSerializer.SerializeAsync(stream, config, WriteOptions, ct).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>~/.config/entra-rdp-connect/config.json, med respekt for XDG_CONFIG_HOME.</summary>
    public static string DefaultConfigPath()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var baseDir = string.IsNullOrWhiteSpace(xdg)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdg;
        return Path.Combine(baseDir, "entra-rdp-connect", "config.json");
    }
}
