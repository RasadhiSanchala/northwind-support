export interface AgentUser {
  id: number;
  fullName: string;
  email: string;
}
export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  user: AgentUser;
}
