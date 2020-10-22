import { Injectable } from '@angular/core';
import { ReplaySubject, Subject } from 'rxjs';

@Injectable({ providedIn: 'root' })

export class EditPluginService {
  private notifyEventSource = new ReplaySubject<EditPluginEvent>();
  public notifyEvent$ = this.notifyEventSource.asObservable();

  constructor() {
  }

  private plugin: any;
  private originalPlugin: any;

  openProperties(plugin: any): void {
    this.originalPlugin = plugin;
    this.plugin = Object.assign({}, plugin);

    this.notifyEventSource.next({
      plugin: this.plugin,
      mode: EditPluginMode.Properties
    });
  }

  deletePlugin(): void {
    this.notifyEventSource.next({
      plugin: null,
      mode: EditPluginMode.None
    });
  }

  closeProperties(plugin?: any): void {
    this.notifyEventSource.next({
      plugin: plugin ? Object.assign(this.plugin, plugin) : this.originalPlugin,
      mode: EditPluginMode.None
    });
  }

  openAccounts(accounts: any[]): void {
    this.plugin.Accounts = accounts;
    this.notifyEventSource.next({
      plugin: this.plugin,
      mode: EditPluginMode.Accounts
    });
  }

  closeAccounts(accounts?: any[]): void {
    if (accounts) {
      this.plugin.Accounts = accounts;
    }
    this.notifyEventSource.next({
      plugin: this.plugin,
      mode: EditPluginMode.Properties
    });
  }
}

export class EditPluginEvent {
  plugin: any;
  mode: EditPluginMode;
}

export enum EditPluginMode {
  None,
  Properties,
  Accounts
}
