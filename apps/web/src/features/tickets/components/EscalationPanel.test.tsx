import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { TicketDetail } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: { escalateTicket: vi.fn() },
}));

const escalateTicket = vi.mocked(ticketsApi.escalateTicket);
const ticket: TicketDetail = {
  id: 42,
  reference: 'TCK-0042',
  title: 'Ticket needing escalation',
  description: 'A support request',
  status: 'Open',
  priority: 'Low',
  customer: {
    id: 1,
    name: 'Contoso',
    tier: 'Premium',
    email: 'support@contoso.example',
    phone: null,
  },
  category: { id: 3, name: 'Technical', requiresSpecialist: false },
  assignedAgent: null,
  createdAtUtc: '2026-10-10T08:00:00Z',
  updatedAtUtc: '2026-10-10T08:00:00Z',
  dueAtUtc: '2026-10-11T20:00:00Z',
  resolvedAtUtc: null,
  slaStatus: 'WithinSla',
};

const fillReason = (reason: string) => {
  fireEvent.change(screen.getByLabelText('Reason'), {
    target: { value: reason },
  });
};

describe('EscalationPanel', () => {
  beforeEach(() => vi.clearAllMocks());

  it('prevents submission of a reason shorter than 5 characters', () => {
    const onEscalated = vi.fn();
    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);
    fillReason('bad');
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(screen.getByRole('alert')).toHaveTextContent('5 to 500 characters');
    expect(escalateTicket).not.toHaveBeenCalled();
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('shows an HTTP 409 without changing ticket state', async () => {
    const onEscalated = vi.fn();
    escalateTicket.mockRejectedValue(new ApiError(409, 'Ticket is already Critical.'));
    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);
    fillReason('Customer needs faster handling');
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('already Critical');
    });
    expect(onEscalated).not.toHaveBeenCalled();
    expect(ticket.priority).toBe('Low');
  });

  it('refreshes ticket/history callback after a successful escalation', async () => {
    const onEscalated = vi.fn();
    const response = {
      ticket: { ...ticket, priority: 'Medium' as const },
      escalation: {
        id: 1,
        ticketId: 42,
        fromPriority: 'Low' as const,
        toPriority: 'Medium' as const,
        fromAgentId: null,
        toAgentId: null,
        fromDueAtUtc: ticket.dueAtUtc,
        toDueAtUtc: '2026-10-11T09:00:00Z',
        reason: 'Urgent customer impact',
        escalatedBy: 'Development Agent',
        escalatedAtUtc: '2026-10-10T09:00:00Z',
      },
    };
    escalateTicket.mockResolvedValue(response);
    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);
    fillReason('Urgent customer impact');
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    await waitFor(() => expect(onEscalated).toHaveBeenCalledWith(response));
    expect(escalateTicket).toHaveBeenCalledWith(42, {
      reason: 'Urgent customer impact',
    });
  });

  it('disables escalation for Critical tickets', () => {
    render(<EscalationPanel ticket={{ ...ticket, priority: 'Critical' }} onEscalated={vi.fn()} />);
    expect(screen.getByText('Critical tickets cannot be escalated further.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Escalate ticket' })).not.toBeInTheDocument();
  });
});
