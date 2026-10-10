import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, request } from '../../api/client';
import { clearSession, getSession, saveSession } from './session';

const user = { id: 1, fullName: 'Alex Turner', email: 'alex@example.com' };

describe('authenticated API client', () => {
  beforeEach(() => { clearSession(); vi.restoreAllMocks(); });

  it('sends the bearer token on API calls', async () => {
    saveSession({ token: 'sample-jwt', expiresAtUtc: '2099-01-01T00:00:00Z', user });
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 200,
      json: async () => ({ id: 42 }) });
    vi.stubGlobal('fetch', fetchMock);
    await request('/tickets/42');
    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/tickets/42'),
      expect.objectContaining({ headers: expect.objectContaining({ Authorization: 'Bearer sample-jwt' }) }));
    vi.unstubAllGlobals();
  });

  it('clears the session when a protected call returns 401', async () => {
    saveSession({ token: 'expired-token', expiresAtUtc: '2099-01-01T00:00:00Z', user });
    const fetchMock = vi.fn().mockResolvedValue({ ok: false, status: 401, statusText: 'Unauthorized',
      json: async () => ({ title: 'Unauthorized' }) });
    vi.stubGlobal('fetch', fetchMock);
    await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);
    expect(getSession()).toBeNull();
    vi.unstubAllGlobals();
  });
});
