import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, Subject, forkJoin, of } from 'rxjs';
import { DevOpsServiceClient } from './service-client.service';
import { tap, finalize, switchMap } from 'rxjs/operators';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';

@UntilDestroy()
@Injectable({ providedIn: 'root' })
export class EditPluginService {
  private notifyEventSource = new Subject<EditPluginEvent>();
  public notifyEvent$ = this.notifyEventSource.asObservable();
  public availableAccountsTotalCount = 0;

  private availableAccounts = [];
  private availableAccountsSub;
  private availableAccounts$ = new BehaviorSubject(this.availableAccounts);

  constructor(
    private serviceClient: DevOpsServiceClient,
    private window: Window) {
  }

  public instanceIndex: number = 0;
  public pluginInstances = [];
  public reverseFlowAvailable: boolean = false;

  public get plugin() {
    return this.pluginInstances[this.instanceIndex];
  }
  public set plugin(value: any) {
    this.pluginInstances[this.instanceIndex] = value;
  }

  getAvailableAccounts(): Observable<any[]> {
    this.plugin.LoadingAvailableAccounts = true;

    if (!this.availableAccountsSub) {
      const sortColumn = this.window.sessionStorage.getItem('AccountsSortColumn');
      const sortDirection = this.window.sessionStorage.getItem('AccountsSortDirection');

      let sortby = '';
      if ((sortColumn === 'account' || sortColumn === 'asset') && (sortDirection === 'asc' || sortDirection === 'desc')) {
        const dir =  sortDirection === 'desc' ? '-' : '';

        if (sortColumn === 'asset') {
          sortby = `${dir}Asset.Name,${dir}Asset.NetworkAddress`;
        } else {
          sortby = `${dir}Name,${dir}DomainName`;
        }
      }
      const defaultPageSize = 25;
      this.availableAccountsSub = this.serviceClient.getAvailableAccountsCount('', sortby).pipe(
        untilDestroyed(this),
        switchMap((count: number) => {
          this.availableAccountsTotalCount = count;
          if (count > 0) {
            return this.serviceClient.getAvailableAccounts('', sortby, 0, defaultPageSize);
          } else {
            return of([]);
          }
        }),
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

  openProperties(pluginInstances: any): void {
    this.instanceIndex = 0;
    this.pluginInstances = pluginInstances;

    pluginInstances.forEach(p => {
      p.Accounts = [];
      p.VaultAccount = null;
      p.VaultAccountDisplayName = '';
      p.LoadingPluginAccounts = true;
      this.initializePluginAccounts(p);
    });

    this.notifyEventSource.next({
      plugin: null,
      mode: EditPluginMode.Properties
    });
  }

  deletePlugin(): void {
    this.notifyEventSource.next({
      plugin: null,
      mode: EditPluginMode.None
    });
  }

  deletePluginConfiguration(name: string, deleteAll: boolean, restartService: boolean = false): Observable<any> {
    return deleteAll ?
      this.serviceClient.deleteAllPluginConfigurations(name, restartService) :
      this.serviceClient.deletePluginConfiguration(name, restartService);
  }

  closeProperties(saved: boolean = false, reload: boolean = false): void {
    this.notifyEventSource.next({
      plugin: null,
      mode: EditPluginMode.None,
      restartMonitoring: saved,
      reload: reload
    });
    this.notifyEventSource.complete();
    this.notifyEventSource = new Subject<EditPluginEvent>();
    this.notifyEvent$ = this.notifyEventSource.asObservable();
    this.clearAvailableAccounts();
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

  closeViewMonitorEvents(): void {
    this.notifyEventSource.next({
      plugin: {},
      mode: EditPluginMode.ViewMonitorEvents
    });
  }

  createInstance(name: string, copyConfig: boolean): Observable<any> {
    return this.serviceClient.postPluginInstance(name, copyConfig);
  }

  private initializePluginAccounts(plugin: any): void {
    forkJoin([
      this.serviceClient.getPluginAccounts(plugin.Name),
      this.serviceClient.getPluginVaultAccount(plugin.Name)
    ]).pipe(
      tap(([managedAccounts, vaultAccount]) => {
        this.mapRetrievableToAvailableAccount(managedAccounts);
        plugin.Accounts = managedAccounts;

        if (vaultAccount) {
          plugin.VaultAccount = {
            Id: vaultAccount.AccountId,
            Name: vaultAccount.AccountName,
            DomainName: vaultAccount.DomainName,
            Asset: {
              Name: vaultAccount.AssetName,
              NetworkAddress: vaultAccount.NetworkAddress
            }
          };
          plugin.VaultAccountDisplayName = this.getVaultAccountDisplay(plugin.VaultAccount);
        }
      }),
      finalize(() => plugin.LoadingPluginAccounts = false)
    ).subscribe();
  }

  public getVaultAccountDisplay(vaultAccount: any): string {
    if (!vaultAccount) {
      return '';
    }
    const system = vaultAccount.DomainName ?? (vaultAccount.Asset.Name ?? vaultAccount.Asset.NetworkAddress);

    return `${vaultAccount.Name} (${system})`;
  }

  private mapRetrievableToAvailableAccount(accounts: any[]): void {
    accounts.forEach(a => {
      a.Id = a.AccountId;
      a.Name = a.AccountName;
      a.AssetNetworkAddress = a.NetworkAddress;
    });
  }
}

export class EditPluginEvent {
  plugin: any;
  mode: EditPluginMode;
  restartMonitoring?: boolean;
  reload?: boolean;
}

export enum EditPluginMode {
  None,
  Properties,
  Accounts,
  VaultAccount,
  ViewMonitorEvents
}
