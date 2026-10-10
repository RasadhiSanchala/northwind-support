import type { ProblemDetails } from '../types/api';
import { clearSession, getSession } from '../features/auth/session';

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api';

export class ApiError extends Error {
  readonly status: number;
  readonly problem?: ProblemDetails;
  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {};
  }
}

export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getSession()?.token;
  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token && !path.startsWith('/auth/login') ? { Authorization: `Bearer ${token}` } : {}),
      ...(init?.headers ?? {}),
    },
  });

  if (!response.ok) {
    const problem = await readProblem(response);
    if (response.status === 401 && !path.startsWith('/auth/login')) clearSession();
    throw new ApiError(response.status,
      problem?.detail ?? problem?.title ?? response.statusText, problem);
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export function toErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) return error.message;
  if (error instanceof Error) return `${fallback} (${error.message})`;
  return fallback;
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  try { return (await response.json()) as ProblemDetails; }
  catch { return undefined; }
}
