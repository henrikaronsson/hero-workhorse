namespace HeroWorkhorse.AgentLoop;

public sealed class AgentLoopOptions
{
    /// <summary>Max model round-trips (each may include tool calls) before the loop gives up.</summary>
    public int MaxSteps { get; set; } = 10;

    /// <summary>Wall-clock limit for the whole run. Null = no timeout.</summary>
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Total token budget (input + output) across all steps. Null = unlimited.</summary>
    public long? MaxTotalTokens { get; set; }
}
