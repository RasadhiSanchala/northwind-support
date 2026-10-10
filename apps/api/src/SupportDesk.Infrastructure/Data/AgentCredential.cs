namespace SupportDesk.Infrastructure.Data;

/// <summary>Credentials are separate from the Agent aggregate's support-workload data.</summary>
public sealed class AgentCredential
{
    private AgentCredential() { }

    public AgentCredential(int agentId, string passwordHash)
    {
        if (agentId <= 0) throw new ArgumentOutOfRangeException(nameof(agentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        AgentId = agentId;
        PasswordHash = passwordHash;
    }

    public int AgentId { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
}
