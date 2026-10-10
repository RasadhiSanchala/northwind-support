import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from './features/auth/AuthProvider';

export function App() {
  const { user, signOut } = useAuth();
  const navigate = useNavigate();
  const logout = () => { signOut(); navigate('/login', { replace: true }); };

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">Northwind Support</Link>
        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>
        <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '12px' }}>
          <span aria-label="Signed-in agent">{user?.fullName}</span>
          <button type="button" className="button button--ghost" onClick={logout}>Log out</button>
        </div>
      </header>
      <main className="app__main"><Outlet /></main>
    </div>
  );
}
