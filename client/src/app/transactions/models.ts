export type TransactionStatus = 'Active' | 'Revoked' | 'Finalized';

export interface Transaction {
  id: number;
  sourceSystem: string;
  transactionId: number;
  cardNumber: string;
  locationCode: string;
  productName: string;
  amount: number;
  transactionTime: string;
  status: TransactionStatus;
  revision: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AuditEntry {
  id: number;
  importRunId: string;
  action: 'Created' | 'Updated' | 'Revoked' | 'Reactivated' | 'Finalized';
  fieldName: string | null;
  oldValue: string | null;
  newValue: string | null;
  changedAtUtc: string;
}

export interface TransactionDetails {
  transaction: Transaction;
  auditHistory: AuditEntry[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ImportRun {
  id: string;
  trigger: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  inserted: number;
  updated: number;
  revoked: number;
  reactivated: number;
  finalized: number;
  unchanged: number;
  rejected: number;
  failedSources: string[];
  succeeded: boolean;
}

export type SortColumn =
  | 'sourceSystem' | 'transactionId' | 'locationCode' | 'productName' | 'amount' | 'transactionTime' | 'status';

export interface TransactionQuery {
  source?: string;
  status?: TransactionStatus | '';
  search?: string;
  sort: SortColumn;
  direction: 'asc' | 'desc';
  page: number;
  pageSize: number;
}
