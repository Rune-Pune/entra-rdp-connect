namespace EntraRdpConnect.Core.Domain;

/// <summary>Det xfreerdp3 trenger for å koble til: navn (IKKE IP), port, Entra-UPN og de faste
/// argumentene (/sec:aad m.fl.).</summary>
public sealed record RdpConnectionInfo(
    string Host,
    int Port,
    string User,
    IReadOnlyList<string> ExtraArgs);
