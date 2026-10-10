import { useCallback } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';

/** Retrieves and refreshes the ticket's newest-first escalation history. */
export function useEscalationHistory(ticketId: number) {
  const load = useCallback(
    () => ticketsApi.getEscalations(ticketId),
    [ticketId],
  );
  return useAsyncData(load, 'Unable to load escalation history.');
}
