import { request } from './client';
import type { AgentUser, LoginResponse } from '../features/auth/types';

export const authApi = {
  login: (email: string, password: string) =>
    request<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),
  me: () => request<AgentUser>('/auth/me'),
};
