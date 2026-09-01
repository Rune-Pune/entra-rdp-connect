using CommunityToolkit.Mvvm.ComponentModel;
using EntraRdpConnect.App.Localization;

namespace EntraRdpConnect.App.ViewModels;

/// <summary>How far a checkpoint has got.</summary>
public enum StepState
{
    /// <summary>Not reached yet.</summary>
    Pending,

    /// <summary>The app is working on this step now.</summary>
    Current,

    /// <summary>The step is waiting for the user to do something — on the phone, or in the browser.</summary>
    NeedsYou,

    /// <summary>Passed.</summary>
    Done,
}

/// <summary>
/// One checkpoint in the connection. The order is fixed, and two of the steps stop and wait for
/// a human — which is the whole reason the rail exists: it answers "where am I, and is it
/// waiting for me?" without the user having to read the status line.
/// </summary>
public sealed partial class ConnectionStep : ObservableObject
{
    public ConnectionStep(string labelKey) => LabelKey = labelKey;

    /// <summary>The resource key the label is looked up by. Kept because the text has to be
    /// fetched again when the user switches language.</summary>
    public string LabelKey { get; }

    public string Label => Localizer.Instance[LabelKey];

    internal void RefreshLabel() => OnPropertyChanged(nameof(Label));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCurrent), nameof(NeedsYou), nameof(IsDone), nameof(IsLit))]
    public partial StepState State { get; set; }

    public bool IsCurrent => State is StepState.Current;
    public bool NeedsYou => State is StepState.NeedsYou;
    public bool IsDone => State is StepState.Done;

    /// <summary>Anything but "not reached yet" — the label is only dimmed before it is relevant.</summary>
    public bool IsLit => State is not StepState.Pending;
}
