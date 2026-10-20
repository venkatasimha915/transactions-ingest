import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ImportRun, PagedResult, Transaction, TransactionDetails, TransactionQuery } from './models';

/** Thin typed wrapper over the backend API. In dev, /api is proxied to the ASP.NET app (src/proxy.conf.json). */
@Injectable({ providedIn: 'root' })
export class TransactionsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  getTransactions(query: TransactionQuery): Observable<PagedResult<Transaction>> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize)
      .set('sort', query.sort)
      .set('direction', query.direction);
    if (query.source) params = params.set('source', query.source);
    if (query.status) params = params.set('status', query.status);
    if (query.search?.trim()) params = params.set('search', query.search.trim());
    return this.http.get<PagedResult<Transaction>>(`${this.base}/transactions`, { params });
  }

  getTransaction(id: number): Observable<TransactionDetails> {
    return this.http.get<TransactionDetails>(`${this.base}/transactions/${id}`);
  }

  getSources(): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/sources`);
  }

  getImportRuns(take = 1): Observable<ImportRun[]> {
    return this.http.get<ImportRun[]>(`${this.base}/imports`, { params: { take } });
  }

  runImport(): Observable<ImportRun> {
    return this.http.post<ImportRun>(`${this.base}/import`, null);
  }
}
