import { Injectable } from '@angular/core';
import { ReplaySubject, BehaviorSubject, Observable, Subject } from 'rxjs';
import { DevOpsServiceClient } from './service-client.service';
import { tap, switchMap } from 'rxjs/operators';

@Injectable({ providedIn: 'root' })

export class EditPluginService {
  private notifyEventSource = new Subject<EditPluginEvent>();
  public notifyEvent$ = this.notifyEventSource.asObservable();

  private availableAccounts = [];
  private availableAccountsSub;
  private availableAccounts$ = new BehaviorSubject(this.availableAccounts);

  constructor(private serviceClient: DevOpsServiceClient) {
  }

  public plugin: any;
  private originalPlugin: any;

  getAvailableAccounts(): Observable<any[]> {
    if (!this.availableAccountsSub) {
      this.availableAccountsSub = this.serviceClient.getAvailableAccounts().pipe(
        tap((accounts) => {
          this.availableAccounts.push(...accounts);
        })).subscribe();
    }
    return this.availableAccounts$;
  }

  openProperties(plugin: any): void {
    this.originalPlugin = plugin;
    this.plugin = Object.assign({}, plugin);
    this.plugin.Accounts = [];
    this.plugin.VaultAccount = null;
    this.plugin.VaultAccountDisplayName = '';
    this.initializePluginAccounts();

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
    this.notifyEventSource.complete();
    this.notifyEventSource = new Subject<EditPluginEvent>();
    this.notifyEvent$ = this.notifyEventSource.asObservable();
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

  private initializePluginAccounts(): void {
    this.serviceClient.getPluginAccounts(this.plugin.Name).pipe(
      tap((accounts) => {
        this.mapRetrievableToAvailableAccount(accounts);
        this.plugin.Accounts = accounts;
      }),
      switchMap(() => {
        return this.serviceClient.getPluginVaultAccount(this.plugin.Name);
      }),
      tap((vAcct) => {
        if (vAcct) {
          this.plugin.VaultAccount = {
            Id: vAcct.AccountId,
            Name: vAcct.AccountName,
            System: vAcct.DomainName ?? (vAcct.SystemName ?? vAcct.SystemNetworkAddress)
          };
          this.plugin.VaultAccountDisplayName = this.getVaultAccountDisplay(this.plugin.VaultAccount);
        }
      })).subscribe(() => {
      });
  }

  public getVaultAccountDisplay(vaultAccount: any): string {
    if (!vaultAccount) {
      return '';
    }
    return `${vaultAccount.Name} (${vaultAccount.System})`;
  }

  private mapRetrievableToAvailableAccount(accounts: any[]): void {
    accounts.forEach(a => {
      a.Id = a.AccountId;
      a.Name = a.AccountName;
      a.SystemName = a.AssetName;
      a.SystemNetworkAddress = a.NetworkAddress;
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
