import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useReferenceData } from '../../hooks/useReferenceData';
import type { TicketDetail } from '../../types/api';
import { CreateTicketForm } from './components/CreateTicketForm';
import { SlaCell } from './components/SlaCell';
import { PriorityBadge } from './components/TicketBadges';

export function CreateTicketPage() {
  const reference = useReferenceData();
  const navigate = useNavigate();
  const [created, setCreated] = useState<TicketDetail>();

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb"><Link to="/tickets">Tickets</Link> / New</p>
          <h1>Raise a ticket</h1>
        </div>
      </header>

      {reference.isLoading && <Spinner label="Loading customers and categories" />}
      {reference.error && (
        <ErrorMessage message={reference.error} onRetry={() => void reference.reload()} />
      )}

      {created && (
        <div className="card notice" role="status">
          <h2>Ticket created: {created.reference}</h2>
          <p>Raised for {created.customer.name}.</p>
          <p>Applied priority: <PriorityBadge priority={created.priority} /></p>
          <p>Assigned agent: {created.assignedAgent?.fullName ?? 'Unassigned'}</p>
          <p>SLA:</p>
          <SlaCell dueAtUtc={created.dueAtUtc} slaStatus={created.slaStatus} />

          {created.triage && (
            <div aria-label="Automatic triage decision">
              <h3>Automatic triage</h3>
              <p>{created.triage.priorityReason}</p>
              <p>{created.triage.slaReason}</p>
              <p>{created.triage.assignmentReason}</p>
            </div>
          )}

          {created.unassignedReason && (
            <p className="field__error">Unassigned because: {created.unassignedReason}</p>
          )}

          <div className="button-row">
            <button
              type="button"
              className="button button--primary"
              onClick={() => void navigate(`/tickets/${created.id}`)}
            >
              Open {created.reference}
            </button>
            <button
              type="button"
              className="button button--ghost"
              onClick={() => setCreated(undefined)}
            >
              Raise another
            </button>
          </div>
        </div>
      )}

      {reference.data && !created && (
        <CreateTicketForm
          customers={reference.data.customers}
          categories={reference.data.categories}
          onCreated={setCreated}
        />
      )}
    </section>
  );
}
