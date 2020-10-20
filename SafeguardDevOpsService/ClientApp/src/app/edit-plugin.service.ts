import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })

export class EditPluginService {

  constructor() {
  }

  private plugin: any;
  private accounts: any[];

  setEdit(plugin: any): void {
    this.plugin = plugin;
  }

  getEdit(): any {
    return this.plugin;
  }

  closeAccounts(accounts: any[]): void {
    this.accounts = accounts;
  }
}
