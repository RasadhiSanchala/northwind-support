import { Badge, type BadgeTone } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';

const labels: Record<SlaStatus, { text: string; tone: BadgeTone }> = {
  NotApplicable: { text: 'No SLA', tone: 'neutral' },
  WithinSla: { text: 'Within SLA', tone: 'info' },
  AtRisk: { text: 'At risk', tone: 'warning' },
  Breached: { text: 'Breached', tone: 'danger' },
  Met: { text: 'Met', tone: 'success' },
};

interface SlaCellProps {
  dueAtUtc: string | null;
  slaStatus: SlaStatus;
}

/** Shared by the ticket list and detail page; state is never communicated by colour alone. */
export function SlaCell({ dueAtUtc, slaStatus }: SlaCellProps) {
  const label = labels[slaStatus];

  return (
    <div className="sla-cell">
      <Badge tone={label.tone}>{label.text}</Badge>
      <span className="sla-cell__due">{formatDateTime(dueAtUtc)}</span>
    </div>
  );
}
