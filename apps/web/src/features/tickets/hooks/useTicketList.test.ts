
import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import type { TicketFilters } from '../../../types/api';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

// Filters that must trigger an immediate API request.
const immediateFilterCases: {
  name: string;
  patch: Partial<TicketFilters>;
}[] = [
    {
      name: 'priority',
      patch: { priority: 'High' },
    },
    {
      name: 'category',
      patch: { categoryId: 2 },
    },
    {
      name: 'customer',
      patch: { customerId: 3 },
    },
    {
      name: 'assigned agent',
      patch: { assignedAgentId: 2 },
    },
    {
      name: 'unassigned only',
      patch: { unassignedOnly: true },
    },
    {
      name: 'sorting',
      patch: { sortBy: 'priority' },
    },
    {
      name: 'page',
      patch: { page: 2 },
    },
    {
      name: 'page size',
      patch: { pageSize: 10 },
    },
  ];

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();

    getTickets.mockResolvedValue(
      page([ticketListItem()]),
    );
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  // --------------------------------------------------
  // TEST 1: Search debounce behaviour
  // --------------------------------------------------

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({
        search: 'invoice',
      });
    });

    // Search must not request immediately.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);

    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({
        search: 'invoice',
        page: 1,
      }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  // --------------------------------------------------
  // TEST 2: DEFECT-117 status change regression
  // --------------------------------------------------

  it('requests tickets immediately when status changes', async () => {
    const { result } = renderHook(() => useTicketList());

    // Complete the initial request.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    // Change only the status filter.
    await act(async () => {
      result.current.updateFilters({
        status: 'InProgress',
      });
    });

    // Status change must trigger a request immediately.
    expect(getTickets).toHaveBeenCalledTimes(2);

    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({
        status: 'InProgress',
        page: 1,
      }),
    );
  });

  // --------------------------------------------------
  // TESTS 3-10: All other immediate filter changes
  // --------------------------------------------------

  it.each(immediateFilterCases)(
    'requests tickets immediately when $name changes',
    async ({ patch }) => {
      const { result } = renderHook(() => useTicketList());

      // Complete the initial request.
      await act(async () => {
        await vi.advanceTimersByTimeAsync(300);
      });

      expect(getTickets).toHaveBeenCalledTimes(1);

      // Change exactly one filter.
      await act(async () => {
        result.current.updateFilters(patch);
      });

      // No debounce delay should be required.
      expect(getTickets).toHaveBeenCalledTimes(2);

      // The request must contain the updated filter.
      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining(patch),
      );
    },
  );

  // --------------------------------------------------
  // TEST 11: Rapid search typing debounce
  // --------------------------------------------------

  it('debounces rapid search typing and sends only the latest text', async () => {
    const { result } = renderHook(() => useTicketList());

    // Complete the initial request.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    // User starts typing.
    act(() => {
      result.current.updateFilters({
        search: 'inv',
      });
    });

    // Only 200ms has passed.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(200);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    // User continues typing before the debounce finishes.
    act(() => {
      result.current.updateFilters({
        search: 'invoice',
      });
    });

    // Still inside the new 300ms debounce period.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(299);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    // The full debounce period has now completed.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1);
    });

    // Only one additional request must be made.
    expect(getTickets).toHaveBeenCalledTimes(2);

    // The API must receive the final search text.
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({
        search: 'invoice',
        page: 1,
      }),
    );

    expect(result.current.isLoading).toBe(false);
  });

  // --------------------------------------------------
  // TEST 12: Stale API response protection
  // --------------------------------------------------

  it('ignores an older API response after filters change', async () => {
    const olderData = page([ticketListItem()]);
    const latestData = page([]);

    let resolveOlder!: (data: typeof olderData) => void;

    // Simulate a slow, unfinished API request.
    const olderRequest = new Promise<typeof olderData>(
      (resolve) => {
        resolveOlder = resolve;
      },
    );

    getTickets.mockReset();

    getTickets
      .mockReturnValueOnce(olderRequest)
      .mockResolvedValueOnce(latestData);

    const { result } = renderHook(() => useTicketList());

    // Start the first request.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    // The first request is still pending.
    // Changing priority must start another request.
    await act(async () => {
      result.current.updateFilters({
        priority: 'High',
      });
    });

    expect(getTickets).toHaveBeenCalledTimes(2);

    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({
        priority: 'High',
      }),
    );

    // The newer request has returned an empty list.
    expect(result.current.data?.items).toHaveLength(0);

    // The older API request finishes after the new one.
    await act(async () => {
      resolveOlder(olderData);
    });

    // Old data must not overwrite the latest results.
    expect(result.current.data?.items).toHaveLength(0);

    expect(result.current.isLoading).toBe(false);
  });
});
