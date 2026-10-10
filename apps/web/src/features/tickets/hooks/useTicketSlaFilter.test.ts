import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: { getTickets: vi.fn() },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('SLA filter regression', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('immediately sends the selected SLA status without waiting for search debounce', async () => {
    const { result } = renderHook(() => useTicketList());
    await act(async () => { await vi.advanceTimersByTimeAsync(300); });
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      result.current.updateFilters({ slaStatus: 'AtRisk' });
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ slaStatus: 'AtRisk', page: 1 }),
    );
  });
});
