namespace EntraRdpConnect.Core.Application;

public enum RdpPhase
{
    Starting,
    WaitingForLoginUrl,
    LoginUrlDetected,
    SubmittingCode,
    SessionEnded,
}
