import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import { AuthProvider } from './AuthProvider';
import { LoginPage } from './LoginPage';
import { RequireAuth } from './RequireAuth';
import { clearSession } from './session';

vi.mock('../../api/auth', () => ({ authApi: { login: vi.fn(), me: vi.fn() } }));
const login = vi.mocked(authApi.login);

const renderFlow = (initial: string) => render(
  <AuthProvider>
    <MemoryRouter initialEntries={[initial]}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<RequireAuth />}>
          <Route path="/tickets" element={<div>Protected tickets screen</div>} />
        </Route>
      </Routes>
    </MemoryRouter>
  </AuthProvider>,
);

describe('agent authentication', () => {
  beforeEach(() => { clearSession(); vi.clearAllMocks(); });

  it('redirects an unauthenticated visitor to login', async () => {
    renderFlow('/tickets');
    expect(await screen.findByRole('heading', { name: /sign in to northwind/i }))
      .toBeInTheDocument();
    expect(screen.queryByText('Protected tickets screen')).not.toBeInTheDocument();
  });

  it('client validation stops an empty login', () => {
    renderFlow('/login');
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Enter your email and password.');
    expect(login).not.toHaveBeenCalled();
  });

  it('logs in, then returns to the originally protected page', async () => {
    login.mockResolvedValue({
      token: 'development-token', expiresAtUtc: '2099-01-01T00:00:00Z',
      user: { id: 1, fullName: 'Alex Turner', email: 'alex@example.com' },
    });
    renderFlow('/tickets');
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'alex@example.com' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'CorrectPassword1!' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await waitFor(() => expect(screen.getByText('Protected tickets screen')).toBeInTheDocument());
    expect(login).toHaveBeenCalledWith('alex@example.com', 'CorrectPassword1!');
  });
});
