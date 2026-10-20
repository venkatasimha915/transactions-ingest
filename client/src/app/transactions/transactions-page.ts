import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ImportRun, SortColumn, Transaction, TransactionDetails, TransactionQuery, TransactionStatus } from './models';
import { TransactionsService } from './transactions.service';

@Component({
  selector: 'app-transactions-page',
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './transactions-page.html',
  styleUrl: './transactions-page.css',
})
export class TransactionsPage implements OnInit {
  private readonly api = inject(TransactionsService);

  readonly statuses: TransactionStatus[] = ['Active', 'Revoked', 'Finalized'];
  readonly columns: { key: SortColumn; label: string; numeric?: boolean }[] = [
    { key: 'sourceSystem', label: 'Source' },
    { key: 'transactionId', label: 'Txn ID', numeric: true },
    { key: 'locationCode', label: 'Location' },
    { key: 'productName', label: 'Product' },
    { key: 'amount', label: 'Amount', numeric: true },
    { key: 'transactionTime', label: 'Time (UTC)' },
    { key: 'status', label: 'Status' },
  ];

  readonly query = signal<TransactionQuery>({
    source: '', status: '', search: '', sort: 'transactionTime', direction: 'desc', page: 1, pageSize: 25,
  });
  readonly sources = signal<string[]>([]);
  readonly rows = signal<Transaction[]>([]);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly importing = signal(false);
  readonly lastRun = signal<ImportRun | null>(null);

  readonly selected = signal<TransactionDetails | null>(null);
  readonly selectedId = computed(() => this.selected()?.transaction.id ?? null);

  readonly rangeLabel = computed(() => {
    const q = this.query();
    const total = this.totalCount();
    if (total === 0) return '0 transactions';
    const from = (q.page - 1) * q.pageSize + 1;
    const to = Math.min(q.page * q.pageSize, total);
    return `${from}–${to} of ${total}`;
  });

  private searchTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.api.getSources().subscribe({ next: (s) => this.sources.set(s), error: () => this.sources.set([]) });
    this.api.getImportRuns(1).subscribe({ next: (runs) => this.lastRun.set(runs[0] ?? null), error: () => {} });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getTransactions(this.query()).subscribe({
      next: (page) => {
        this.rows.set(page.items);
        this.totalCount.set(page.totalCount);
        this.totalPages.set(page.totalPages);
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(describe(err, 'Could not load transactions'));
        this.loading.set(false);
      },
    });
  }

  runImport(): void {
    this.importing.set(true);
    this.error.set(null);
    this.api.runImport().subscribe({
      next: (run) => {
        this.lastRun.set(run);
        this.importing.set(false);
        this.load();
        const open = this.selectedId();
        if (open !== null) this.openDetails(open);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(describe(err, 'Import failed'));
        this.importing.set(false);
      },
    });
  }

  setFilter(patch: Partial<TransactionQuery>): void {
    this.query.update((q) => ({ ...q, ...patch, page: 1 }));
    this.load();
  }

  onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.setFilter({ search: value }), 300);
  }

  sortBy(column: SortColumn): void {
    this.query.update((q) => ({
      ...q,
      sort: column,
      direction: q.sort === column && q.direction === 'desc' ? 'asc' : 'desc',
      page: 1,
    }));
    this.load();
  }

  goTo(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.query.update((q) => ({ ...q, page }));
    this.load();
  }

  openDetails(id: number): void {
    this.api.getTransaction(id).subscribe({
      next: (d) => this.selected.set(d),
      error: (err: HttpErrorResponse) => this.error.set(describe(err, 'Could not load transaction')),
    });
  }

  closeDetails(): void {
    this.selected.set(null);
  }

  /** Colour family for a source system pill. */
  sourceTone(name: string): string {
    const n = name.toLowerCase();
    if (n.includes('uptown')) return 'uptown';
    if (n.includes('downtown')) return 'downtown';
    if (n.includes('online')) return 'online';
    return 'other';
  }

  ariaSort(column: SortColumn): 'ascending' | 'descending' | 'none' {
    const q = this.query();
    return q.sort !== column ? 'none' : q.direction === 'asc' ? 'ascending' : 'descending';
  }
}

function describe(err: HttpErrorResponse, fallback: string): string {
  if (err.status === 0) return `${fallback}: the API is not reachable. Is the backend running on http://localhost:5080?`;
  const problem = err.error as { title?: string; detail?: string } | null;
  return `${fallback}: ${problem?.detail ?? problem?.title ?? err.statusText}`;
}
