namespace Transactions.Infrastructure.Sources;

public sealed class SourceSystemsOptions
{
    public const string SectionName = "SourceSystems";

    /// <summary>Folder holding the mock snapshot files, relative to the app content root.</summary>
    public string DataDirectory { get; set; } = "SampleData";

    public List<SourceSystemOptions> Systems { get; set; } = [];
}

public sealed class SourceSystemOptions
{
    public string Name { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;

    /// <summary>Demo switch: makes this source throw, to show that a failing source never causes revocations.</summary>
    public bool SimulateFailure { get; set; }
}
