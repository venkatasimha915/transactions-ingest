import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('renders transactions returned by the API', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);

    http.expectOne('/api/sources').flush(['Online']);
    http.expectOne((r) => r.url === '/api/imports').flush([]);
    http.expectOne((r) => r.url === '/api/transactions').flush({
      items: [{
        id: 1, sourceSystem: 'Online', transactionId: 100001, cardNumber: '***4444', locationCode: 'ONLINE',
        productName: 'USB-C Docking Statio', amount: 129.95, transactionTime: '2026-02-27T11:20:05Z',
        status: 'Finalized', revision: 1, createdAtUtc: '', updatedAtUtc: '',
      }],
      page: 1, pageSize: 25, totalCount: 1, totalPages: 1,
    });
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).querySelector('tbody')?.textContent ?? '';
    expect(text).toContain('100001');
    expect(text).toContain('ONLINE');
    expect(text).toContain('$129.95');
    expect(text).toContain('Finalized');
    http.verify();
  });
});
