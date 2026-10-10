/** Shared, fully typed HTTP contracts for the support desk frontend. */
export type CustomerTier = 'Standard' | 'Premium';
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical';
export type TicketStatus = 'New' | 'Open' | 'InProgress' | 'Resolved' | 'Closed';
export type SlaStatus = 'NotApplicable' | 'WithinSla' | 'AtRisk' | 'Breached' | 'Met';
export type TicketSortField = 'createdAtUtc' | 'updatedAtUtc' | 'dueAtUtc' | 'priority' | 'status';
export type SortDirection = 'asc' | 'desc';

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CustomerSummary {
  id: number;
  name: string;
  tier: CustomerTier;
}

export interface CustomerContact extends CustomerSummary {
  email: string;
  phone: string | null;
}

export interface CategorySummary {
  id: number;
  name: string;
}

export interface CategoryOnTicket extends CategorySummary {
  requiresSpecialist: boolean;
}

export interface AgentSummary {
  id: number;
  fullName: string;
}

export interface TicketListItem {
  id: number;
  reference: string;
  title: string;
  status: TicketStatus;
  priority: TicketPriority;
  customer: CustomerSummary;
  category: CategorySummary;
  assignedAgent: AgentSummary | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  dueAtUtc: string | null;
  resolvedAtUtc: string | null;
  slaStatus: SlaStatus;
}

export interface TicketTriage {
  priorityReason: string;
  slaReason: string;
  assignmentReason: string;
}

export interface TicketDetail {
  id: number;
  reference: string;
  title: string;
  description: string;
  status: TicketStatus;
  priority: TicketPriority;
  customer: CustomerContact;
  category: CategoryOnTicket;
  assignedAgent: AgentSummary | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  dueAtUtc: string | null;
  resolvedAtUtc: string | null;
  slaStatus: SlaStatus;
  triage?: TicketTriage | null;
  unassignedReason?: string | null;
}

/** All filters are executed in SQL before count, sorting and pagination. */
export interface TicketFilters {
  search: string;
  status?: TicketStatus;
  priority?: TicketPriority;
  slaStatus?: SlaStatus;
  categoryId?: number;
  customerId?: number;
  assignedAgentId?: number;
  unassignedOnly?: boolean;
  page: number;
  pageSize: number;
  sortBy: TicketSortField;
  sortDirection: SortDirection;
}

export interface CreateTicketPayload {
  title: string;
  description: string;
  customerId: number;
  categoryId: number;
  requestedPriority?: TicketPriority;
}

/** In Task 2 the actor is explicitly supplied; Task 3 replaces it with JWT identity. */
export interface EscalateTicketPayload {
  reason: string;
  escalatedBy: string;
}

export interface TicketEscalation {
  id: number;
  ticketId: number;
  fromPriority: TicketPriority;
  toPriority: TicketPriority;
  fromAgentId: number | null;
  toAgentId: number | null;
  fromDueAtUtc: string | null;
  toDueAtUtc: string | null;
  reason: string;
  escalatedBy: string;
  escalatedAtUtc: string;
}

export interface EscalateTicketResponse {
  ticket: TicketDetail;
  escalation: TicketEscalation;
}

export interface Agent {
  id: number;
  fullName: string;
  email: string;
  isActive: boolean;
  maxOpenTickets: number;
  openTicketCount: number;
  specializations: CategorySummary[];
}

export interface Category {
  id: number;
  name: string;
  isActive: boolean;
  requiresSpecialist: boolean;
  forcesCriticalPriority: boolean;
}

export interface CustomerListItem {
  id: number;
  name: string;
  email: string;
  phone: string | null;
  tier: CustomerTier;
  openTicketCount: number;
}

export interface CustomerDetail {
  id: number;
  name: string;
  email: string;
  phone: string | null;
  tier: CustomerTier;
  createdAtUtc: string;
  tickets: TicketListItem[];
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}
