import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, Subject, forkJoin, pipe } from 'rxjs';
import { DevOpsServiceClient } from './service-client.service';
import { tap, finalize, delay } from 'rxjs/operators';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';

@UntilDestroy()
@Injectable({ providedIn: 'root' })
export class EditPluginService {
  private notifyEventSource = new Subject<EditPluginEvent>();
  public notifyEvent$ = this.notifyEventSource.asObservable();

  private availableAccounts = [];
  private availableAccountsSub;
  private availableAccounts$ = new BehaviorSubject(this.availableAccounts);

  constructor(
    private serviceClient: DevOpsServiceClient,
    private window: Window) {
  }

  public plugin: any;
  private originalPlugin: any;

  getAvailableAccounts(): Observable<any[]> {
    this.plugin.LoadingAvailableAccounts = true;

    if (!this.availableAccountsSub) {
      const sortColumn = this.window.sessionStorage.getItem('AccountsSortColumn');
      const sortDirection = this.window.sessionStorage.getItem('AccountsSortDirection');

      let sortby = '';
      if ((sortColumn === 'account' || sortColumn === 'asset') && (sortDirection === 'asc' || sortDirection === 'desc')) {
        const dir =  sortDirection === 'desc' ? '-' : '';

        if (sortColumn === 'asset') {
          sortby = `${dir}SystemName,${dir}SystemNetworkAddress`;
        } else {
          sortby = `${dir}Name,${dir}DomainName`;
        }
      }

      this.availableAccountsSub = this.serviceClient.getAvailableAccounts('', sortby).pipe(
        untilDestroyed(this),
        tap((accounts) => {
          this.plugin.LoadingAvailableAccounts = false;

          this.availableAccounts.push(...accounts);
          this.availableAccounts$.next(accounts);
        }),
        finalize(() => this.plugin.LoadingAvailableAccounts = false)
      ).subscribe();
    } else {
      this.plugin.LoadingAvailableAccounts = false;
    }
    return this.availableAccounts$;
  }

  clearAvailableAccounts(): void {
    this.availableAccountsSub = null;
    this.availableAccounts.splice(0);
  }

  openProperties(plugin: any): void {
    this.originalPlugin = plugin;
    this.plugin = Object.assign({}, plugin);
    this.plugin.Accounts = [];
    this.plugin.VaultAccount = null;
    this.plugin.VaultAccountDisplayName = '';
    this.plugin.LoadingPluginAccounts = true;
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

  openVaultAccount(): void {
    this.notifyEventSource.next({
      plugin: this.plugin,
      mode: EditPluginMode.VaultAccount
    });
  }

  closeVaultAccount(account?: any): void {
    if (account) {
      this.plugin.VaultAccount = account;
    }
    this.notifyEventSource.next({
      plugin: this.plugin,
      mode: EditPluginMode.Properties
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

  private initializePluginAccounts(): void {
    forkJoin([
      this.serviceClient.getPluginAccounts(this.plugin.Name),
      this.serviceClient.getPluginVaultAccount(this.plugin.Name)
    ]).pipe(
      tap(([managedAccounts, vaultAccount]) => {
        this.mapRetrievableToAvailableAccount(managedAccounts);
        this.plugin.Accounts = managedAccounts;

        if (vaultAccount) {
          this.plugin.VaultAccount = {
            Id: vaultAccount.AccountId,
            Name: vaultAccount.AccountName,
            DomainName: vaultAccount.DomainName,
            SystemName: vaultAccount.SystemName,
            SystemNetworkAddress: vaultAccount.NetworkAddress
          };
          this.plugin.VaultAccountDisplayName = this.getVaultAccountDisplay(this.plugin.VaultAccount);
        }
      }),
      finalize(() => this.plugin.LoadingPluginAccounts = false)
    ).subscribe();
  }

  public getVaultAccountDisplay(vaultAccount: any): string {
    if (!vaultAccount) {
      return '';
    }
    const system = vaultAccount.DomainName ?? (vaultAccount.SystemName ?? vaultAccount.SystemNetworkAddress);

    return `${vaultAccount.Name} (${system})`;
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
  Accounts,
  VaultAccount
}
