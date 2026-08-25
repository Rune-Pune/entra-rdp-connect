using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Core.Ports;
using EntraRdpConnect.Infrastructure.Processes;

namespace EntraRdpConnect.Infrastructure.Rdp;

/// <summary>
/// Adapter for <see cref="IRdpSession"/> rundt xfreerdp3. Starter prosessen med stdin omdirigert,
/// skanner stdout+stderr etter "Browse to:" (via <see cref="RdpLoginHandler"/>), og skriver
/// retur-koden tilbake på stdin. Selve parsing/koordinering ligger i Core og er enhetstestet;
/// denne klassen er kun prosess-bindingen.
/// </summary>
public sealed class FreeRdpSession : IRdpSession
{
    private readonly bool _echoRawLines;

    /// <param name="echoRawLines">Skriv hver stdout/stderr-linje fra xfreerdp3 til stderr med
    /// prefiks [rdp]. Diagnostikk for å se det faktiske outputformatet.</param>
    public FreeRdpSession(bool echoRawLines = false) => _echoRawLines = echoRawLines;

    public async Task<int> ConnectAsync(
        RdpConnectionInfo info,
        Func<Uri, CancellationToken, Task<Uri>> resolveRedirectUrl,
        IProgress<RdpPhase>? progress = null,
        CancellationToken ct = default)
    {
        var rdpArgs = new List<string>
        {
            $"/v:{info.Host}:{info.Port}",
            $"/u:{info.User}",
        };
        rdpArgs.AddRange(info.ExtraArgs);

        // FreeRDPs AAD-innlogging leser "Browse to:"/koden via en TERMINAL (tcgetattr), ikke via
        // rør. Vi kjører derfor xfreerdp3 under en pseudo-terminal som `script` allokerer, så vi
        // fortsatt kan lese stdout og skrive koden på stdin gjennom rørene våre.
        // Hvert ledd siteres for seg: verdiene kommer fra config, og `script -c` kjører strengen
        // gjennom et shell. Uten sitering ville f.eks. et vertsnavn med semikolon blitt utført.
        var rdpCommand = ShellQuote.Join(rdpArgs.Prepend("xfreerdp3"));
        var spec = new ProcessSpec("script", "-q", "-e", "-c", rdpCommand, "/dev/null")
        {
            RedirectStdin = true,
        };

        // runner refereres av submit-delegaten; tilordnes rett før Start, og submit kalles først
        // etter at en login-URL er sett (sekunder senere) — så referansen er trygt satt da.
        ProcessRunner runner = null!;
        var handler = new RdpLoginHandler(
            resolveRedirectUrl,
            submit: (text, token) => runner.WriteStdinLineAsync(text, token),
            progress,
            ct);

        void OnLine(string line)
        {
            if (_echoRawLines)
                Console.Error.WriteLine($"[rdp] {MaskAuthorizationCode(line)}");
            handler.OnLine(line);
        }

        progress?.Report(RdpPhase.Starting);
        // Handlerne kobles til i Start (før pumpene) — ingen risiko for å miste "Browse to:".
        runner = ProcessRunner.Start(spec, onStdout: OnLine, onStderr: OnLine);

        await using (runner)
        {
            progress?.Report(RdpPhase.WaitingForLoginUrl);
            var exitCode = await runner.WaitForExitAsync(ct).ConfigureAwait(false);

            // Overflaten eventuelle feil fra resolver/stdin-skriving.
            await handler.Completion.ConfigureAwait(false);

            progress?.Report(RdpPhase.SessionEnded);
            return exitCode;
        }
    }

    /// <summary>Pseudo-terminalen ekko-er stdin tilbake på stdout, så retur-adressen med
    /// autorisasjonskoden dukker opp i outputen. Den skal ikke havne i en logg.</summary>
    internal static string MaskAuthorizationCode(string line)
        => System.Text.RegularExpressions.Regex.Replace(
            line, @"(?i)(code=)[^\s&]+", "$1***");

}
