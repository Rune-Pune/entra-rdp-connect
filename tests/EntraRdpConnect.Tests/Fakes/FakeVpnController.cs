using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.Tests.Fakes;

/// <summary>Fake VPN-controller: returnerer en forhåndsdefinert sekvens av statuser (klamper til
/// den siste), og teller opp/ned-kall. Lar oss spille av «handshake kommer etter N poll» og
/// «handshake kommer aldri» uten en ekte tunnel.</summary>
internal sealed class FakeVpnController : IVpnController
{
    private readonly IReadOnlyList<VpnStatus> _sequence;
    private int _index;

    public int BringUpCalls { get; private set; }
    public int BringDownCalls { get; private set; }

    public FakeVpnController(params VpnStatus[] sequence)
    {
        if (sequence.Length == 0)
            throw new ArgumentException("Sekvensen må ha minst én status.", nameof(sequence));
        _sequence = sequence;
    }

    public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var status = _sequence[Math.Min(_index, _sequence.Count - 1)];
        _index++;
        return Task.FromResult(status);
    }

    public Task BringUpAsync(CancellationToken ct = default)
    {
        BringUpCalls++;
        return Task.CompletedTask;
    }

    public Task BringDownAsync(CancellationToken ct = default)
    {
        BringDownCalls++;
        return Task.CompletedTask;
    }
}
