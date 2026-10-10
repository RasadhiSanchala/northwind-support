import { useState } from 'react';
import type { FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { authApi } from '../../api/auth';
import { toErrorMessage } from '../../api/client';
import { useAuth } from './AuthProvider';

interface ReturnLocation { pathname?: string; search?: string; hash?: string; }

export function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const { signIn, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const state = location.state as { from?: ReturnLocation } | null;
  const target = state?.from?.pathname?.startsWith('/') &&
    !state.from.pathname.startsWith('//')
      ? `${state.from.pathname}${state.from.search ?? ''}${state.from.hash ?? ''}`
      : '/tickets';

  if (isAuthenticated) return <Navigate to={target} replace />;

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError('');
    if (!email.trim() || !password) {
      setError('Enter your email and password.');
      return;
    }
    setPending(true);
    try {
      const response = await authApi.login(email.trim(), password);
      signIn(response);
      navigate(target, { replace: true });
    } catch (caught) {
      setError(toErrorMessage(caught, 'Could not sign in.'));
    } finally {
      setPending(false);
    }
  };

  return (
    <main className="app__main">
      <section className="card form" aria-label="Agent sign in">
        <h1>Sign in to Northwind Support</h1>
        <p className="page-header__subtitle">Use your agent account to access support tickets.</p>
        <form onSubmit={(event) => void submit(event)} noValidate>
          <div className="field">
            <label htmlFor="login-email" className="field__label">Email</label>
            <input id="login-email" type="email" autoComplete="username"
              value={email} disabled={pending} onChange={e => setEmail(e.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="login-password" className="field__label">Password</label>
            <input id="login-password" type="password" autoComplete="current-password"
              value={password} disabled={pending} onChange={e => setPassword(e.target.value)} />
          </div>
          {error && <p className="field__error" role="alert">{error}</p>}
          <button className="button button--primary" type="submit" disabled={pending}>
            {pending ? 'Signing in...' : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  );
}
