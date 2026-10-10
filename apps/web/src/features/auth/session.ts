import type { LoginResponse } from './types';

const KEY = 'northwind-support-auth';
export const AUTH_CHANGED_EVENT = 'northwind:auth-changed';

export function getSession(): LoginResponse | null {
  try {
    const value = localStorage.getItem(KEY);
    if (!value) return null;
    const parsed: unknown = JSON.parse(value);
    if (
      typeof parsed !== 'object' || parsed === null ||
      !('token' in parsed) || typeof parsed.token !== 'string' ||
      !('expiresAtUtc' in parsed) || typeof parsed.expiresAtUtc !== 'string' ||
      !('user' in parsed) || typeof parsed.user !== 'object' || parsed.user === null ||
      !('fullName' in parsed.user) || typeof parsed.user.fullName !== 'string'
    ) return null;
    if (Date.parse(parsed.expiresAtUtc) <= Date.now()) {
      localStorage.removeItem(KEY);
      return null;
    }
    return parsed as LoginResponse;
  } catch { return null; }
}

export function saveSession(value: LoginResponse): void {
  localStorage.setItem(KEY, JSON.stringify(value));
  window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
}

export function clearSession(): void {
  localStorage.removeItem(KEY);
  window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
}
