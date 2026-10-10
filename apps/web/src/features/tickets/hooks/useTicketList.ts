import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  slaStatus: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Filter changes trigger immediate requests, except search typing (300 ms debounce). */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [state, setState] = useState<State>({ isLoading: true });
  const previousFiltersRef = useRef<TicketFilters>(filters);
  const requestIdRef = useRef(0);

  const load = useCallback(async (query: TicketFilters) => {
    const requestId = ++requestIdRef.current;
    setState((current) => ({ ...current, isLoading: true, error: undefined }));

    try {
      const data = await ticketsApi.getTickets(query);
      if (requestId === requestIdRef.current) {
        setState({ isLoading: false, data, error: undefined });
      }
    } catch (error) {
      if (requestId === requestIdRef.current) {
        const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
        setState((current) => ({ ...current, isLoading: false, error: message }));
      }
    }
  }, []);

  useEffect(() => {
    const previous = previousFiltersRef.current;
    const searchChanged = previous.search !== filters.search;

    const immediateFields: (keyof TicketFilters)[] = [
      'status',
      'priority',
      'slaStatus',
      'categoryId',
      'customerId',
      'assignedAgentId',
      'unassignedOnly',
      'pageSize',
      'sortBy',
      'sortDirection',
    ];

    const otherFilterChanged = immediateFields.some(
      (field) => previous[field] !== filters[field],
    );
    const shouldDebounce = searchChanged && !otherFilterChanged;
    previousFiltersRef.current = filters;

    // Invalidate pending results before the debounce has even finished.
    requestIdRef.current += 1;

    if (shouldDebounce) {
      const handle = window.setTimeout(() => { void load(filters); }, DEBOUNCE_MS);
      return () => window.clearTimeout(handle);
    }

    void load(filters);
  }, [filters, load]);

  useEffect(() => () => { requestIdRef.current += 1; }, []);

  const updateFilters = (patch: Partial<TicketFilters>) => {
    setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));
  };

  const resetFilters = () => setFilters(emptyFilters);

  return {
    filters,
    updateFilters,
    resetFilters,
    reload: () => load(filters),
    ...state,
  };
}
