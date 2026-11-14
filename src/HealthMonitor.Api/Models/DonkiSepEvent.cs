namespace HealthMonitor.Api.Models;

public class DonkiSepEvent
{
    public string SepId { get; set; } = "";

    public string EventTime { get; set; } = "";

    public List<DonkiInstrument> Instruments { get; set; } = [];

    public string? Link { get; set; }

    public List<DonkiLinkedEvent>? LinkedEvents { get; set; }

    public List<DonkiNotification>? SentNotifications { get; set; }

    public string SubmissionTime { get; set; } = "";

    public int VersionId { get; set; }
}

public class DonkiInstrument
{
    public string DisplayName { get; set; } = "";
}

public class DonkiLinkedEvent
{
    public string ActivityId { get; set; } = "";
}

public class DonkiNotification
{
    public string MessageId { get; set; } = "";

    public string MessageIssueTime { get; set; } = "";

    public string MessageUrl { get; set; } = "";
}
