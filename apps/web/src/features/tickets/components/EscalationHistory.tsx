import './escalation-history.css';
import { formatDateTime } from '../../../lib/format';
import type { Agent, TicketEscalation } from '../../../types/api';

interface EscalationHistoryProps {
  history: TicketEscalation[];
  agents: Agent[];
  loading: boolean;
  error?: string;
  onRetry: () => void;
}

/** Displays the immutable before/after snapshot, newest escalation first. */
export function EscalationHistory({
  history,
  agents,
  loading,
  error,
  onRetry,
}: EscalationHistoryProps) {
  const agentLabel = (id: number | null) => {
    if (id === null) return 'Unassigned';
    return agents.find((agent) => agent.id === id)?.fullName ?? `Agent #${id}`;
  };

  return (
    <section className="card" aria-label="Escalation history" aria-busy={loading}>
      <h2>Escalation history</h2>
      {loading && <p role="status">Loading escalation history...</p>}
      {error && (
        <div role="alert">
          <p>{error}</p>
          <button type="button" className="button button--ghost" onClick={onRetry}>
            Retry history
          </button>
        </div>
      )}
      {!loading && !error && history.length === 0 && <p>No escalations recorded.</p>}
      {history.length > 0 && (
        <ol className="escalation-history">
          {[...history]
            .sort((a, b) => b.escalatedAtUtc.localeCompare(a.escalatedAtUtc) || b.id - a.id)
            .map((item) => (
              <li key={item.id} className="escalation-history__item">
                <strong>{item.fromPriority} → {item.toPriority}</strong>
                <p>Agent: {agentLabel(item.fromAgentId)} → {agentLabel(item.toAgentId)}</p>
                <p>
                  Due: {formatDateTime(item.fromDueAtUtc)} → {formatDateTime(item.toDueAtUtc)}
                </p>
                <p>Reason: {item.reason}</p>
                <p className="summary__note">
                  By {item.escalatedBy} on {formatDateTime(item.escalatedAtUtc)}
                </p>
              </li>
            ))}
        </ol>
      )}
    </section>
  );
}
