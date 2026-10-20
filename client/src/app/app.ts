import { Component } from '@angular/core';
import { TransactionsPage } from './transactions/transactions-page';

@Component({
  selector: 'app-root',
  imports: [TransactionsPage],
  template: '<app-transactions-page />',
})
export class App {}
