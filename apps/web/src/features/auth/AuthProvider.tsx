import { createContext, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { AUTH_CHANGED_EVENT, clearSession, getSession, saveSession } from './session';
import type { AgentUser, LoginResponse } from './types';

interface AuthContextValue {
  user: AgentUser | null;
  isAuthenticated: boolean;
  signIn: (response: LoginResponse) => void;
  signOut: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState(getSession);
  useEffect(() => {
    const update = () => setSession(getSession());
    window.addEventListener(AUTH_CHANGED_EVENT, update);
    return () => window.removeEventListener(AUTH_CHANGED_EVENT, update);
  }, []);

  const signIn = (value: LoginResponse) => {
    saveSession(value);
    setSession(value);
  };
  const signOut = () => {
    clearSession();
    setSession(null);
  };
  return (
    <AuthContext.Provider value={{
      user: session?.user ?? null,
      isAuthenticated: Boolean(session),
      signIn, signOut,
    }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (value === null) throw new Error('useAuth requires AuthProvider.');
  return value;
}
