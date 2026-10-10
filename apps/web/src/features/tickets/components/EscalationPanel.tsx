import { useState } from 'react';
import type { FormEvent } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { EscalateTicketResponse, TicketDetail } from '../../../types/api';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (response: EscalateTicketResponse) => void | Promise<void>;
}

/** Validates the reason. The server records the authenticated JWT agent. */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string>();
  const [fieldError, setFieldError] = useState<string>();

  const prohibited = ticket.priority === 'Critical' ||
    ticket.status === 'Resolved' || ticket.status === 'Closed';

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(undefined);
    setFieldError(undefined);

    if (prohibited || isSaving) return;

    const normalizedReason = reason.trim();

    if (normalizedReason.length < 5 || normalizedReason.length > 500) {
      setFieldError('Reason must contain 5 to 500 characters.');
      return;
    }

    setIsSaving(true);
    try {
      const response = await ticketsApi.escalateTicket(ticket.id, {
        reason: normalizedReason,
      });
      // Only update the ticket when the mutation succeeds.
      await onEscalated(response);
      setReason('');
    } catch (caught) {
      // Includes HTTP 409 conflicts, preserving the previous ticket state.
      setError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <section className="card" aria-label="Ticket escalation">
      <h2>Escalate ticket</h2>
      {prohibited ? (
        <p className="field__hint">
          {ticket.priority === 'Critical'
            ? 'Critical tickets cannot be escalated further.'
            : `${ticket.status} tickets cannot be escalated.`}
        </p>
      ) : (
        <form onSubmit={(event) => void submit(event)} noValidate>
          <div className="field">
            <label className="field__label" htmlFor="escalation-reason">Reason</label>
            <textarea
              id="escalation-reason"
              rows={3}
              value={reason}
              maxLength={500}
              onChange={(event) => setReason(event.target.value)}
              disabled={isSaving}
              aria-invalid={Boolean(fieldError)}
              aria-describedby={fieldError ? 'escalation-validation' : 'escalation-reason-help'}
            />
            {fieldError ? (
              <p className="field__error" role="alert" id="escalation-validation">{fieldError}</p>
            ) : (
              <p className="field__hint" id="escalation-reason-help">
                Required, 5–500 characters. Priority increases one level.
              </p>
            )}
          </div>
          <button type="submit" className="button button--primary" disabled={isSaving}>
            {isSaving ? 'Escalating...' : 'Escalate ticket'}
          </button>
        </form>
      )}
      {error && <p role="alert" className="field__error">{error}</p>}
    </section>
  );
}
