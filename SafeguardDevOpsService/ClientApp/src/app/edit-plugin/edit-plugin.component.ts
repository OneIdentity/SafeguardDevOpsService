import { Component, OnInit } from '@angular/core';
import { EditPluginService, EditPluginMode } from '../edit-plugin.service';
import { DevOpsServiceClient } from '../service-client.service';
import { ServiceClientHelper as SCH } from '../service-client-helper';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';
import { finalize, switchMap, tap, take, takeLast, map, last, filter } from 'rxjs/operators';
import { of, forkJoin } from 'rxjs';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-edit-plugin',
  templateUrl: './edit-plugin.component.html',
  styleUrls: ['./edit-plugin.component.scss']
})

@UntilDestroy()
export class EditPluginComponent implements OnInit {

  constructor(
    private editPluginService: EditPluginService,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog
    ) { }

  plugin: any;
  configs = [];
  error: any;
  isSaving = false;
  displayedColumns: string[] = ['asset', 'account', 'delete'];

  // Vault account
  vAccountInvalid = false;
  allVAccounts = [];
  validVAccounts = [];
  loadingVAccounts: boolean;
  subscription: any;

  ngOnInit(): void {
    this.plugin = this.editPluginService.plugin;

    this.configs.splice(0);
    Object.keys(this.plugin.Configuration).forEach(key => {
      this.configs.push({ key, value: this.plugin.Configuration[key] });
    });

    this.loadingVAccounts = true;
    this.editPluginService.getAvailableAccounts().pipe(
      untilDestroyed(this)
    ).subscribe(
      (data) => {
        this.allVAccounts = data;
        this.loadingVAccounts = false;
      }
    );
  }

  selectAccounts(): void {
    // Save the current configuration first
    this.mapConfiguration();

    this.editPluginService.openAccounts(this.plugin.Accounts);
  }

  removeAccount(event: Event, account: any): void {
    event.stopPropagation();

    const accounts = this.plugin.Accounts as any[];
    const indx = accounts.indexOf(account);

    if (indx > -1) {
      accounts.splice(indx, 1);

      // Change the reference to the array so the grid updates
      this.plugin.Accounts = [...accounts];
    }
  }

  close(): void {
    this.editPluginService.closeProperties();
  }

  delete(): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: { title: 'Delete Plugin', message: 'This removes the configuration for a specific plugin and unregisters the plugin from Safeguard Secrets Broker for DevOps. However, this does not remove the plugin from the ExternalPlugins folder. The plugin files must be manually removed from the ExternalPlugins folder once Safeguard Secrets Broker for DevOps has been stopped. Click "OK" to continue.' }
    });

    dialogRef.afterClosed().pipe(
      filter((dlgResult) => dlgResult.result === 'OK'),
      switchMap(() => this.serviceClient.deletePluginConfiguration(this.plugin.Name))
    ).subscribe(
      () => this.editPluginService.deletePlugin()
    );
  }

  private mapConfiguration(): void {
    this.configs.forEach(config => {
      this.plugin.Configuration[config.key] = config.value;
    });
  }

  save(): void {
    this.error = null;
    this.isSaving = true;

    this.mapConfiguration();

    // Make sure the accounts have AccountId, which PUT Plugin/Accounts expects
    this.plugin.Accounts.forEach(x => x.AccountId = x.Id);

    const obs1 = this.serviceClient.getPluginAccounts(this.plugin.Name).pipe(
      switchMap((accts) => {
        const deleted = [];
        accts.forEach(a => {
          if (!this.plugin.Accounts.find(x => x.AccountId === a.AccountId)) {
            deleted.push(a);
          }
        });
        if (deleted.length > 0) {
          return this.serviceClient.deletePluginAccounts(this.plugin.Name, deleted);
        } else {
          return of({});
        }
      }),
      switchMap(() => this.plugin.Accounts.length > 0 ? this.serviceClient.putRetrievableAccounts(this.plugin.Accounts) : of({})),
      switchMap(() => this.plugin.Accounts.length > 0 ? this.serviceClient.putPluginAccounts(this.plugin.Name, this.plugin.Accounts) : of([]))
    );

    const obs2 = this.serviceClient.putPluginConfiguration(this.plugin.Name, this.plugin.Configuration);

    const obs3 = this.plugin.VaultAccount ?
      this.serviceClient.putPluginVaultAccount(this.plugin.Name, this.plugin.VaultAccount) :
      this.serviceClient.deletePluginVaultAccount(this.plugin.Name);

    forkJoin([obs1, obs2, obs3]).pipe(
      untilDestroyed(this)
    ).subscribe(
      (data) => {
        this.plugin = data[1];
        this.plugin.Accounts =  data[0];
        this.plugin.MappedAccountsCount = this.plugin.Accounts.length;

        this.editPluginService.closeProperties(this.plugin);
      },
      (error) => {
        this.error = SCH.parseError(error);
      }
    );
  }

  //
  // Vault account auto complete
  //
  getVAccountText(option: any): string {
    if (!option) {
      return '';
    }
    if (typeof option === 'string') {
      return option;
    }
    return `${option.Name} (${option.System})`;
  }

  selectVAccount(event: any): void {
    this.plugin.VaultAccount = event.option.value;
    this.vAccountInvalid = false;
  }

  showVAccounts(name?: string): void {
    this.validVAccounts = [];

    const accts = this.allVAccounts.filter(a => a.Name.includes(name));
    accts.forEach(a => {
      if (!this.validVAccounts.some(v => v.Id === a.Id)) {
        this.validVAccounts.push({
          Id: a.Id,
          Name: a.Name,
          System: a.DomainName ?? (a.SystemName ?? a.SystemNetworkAddress)
        });
      }
    });

    this.validVAccounts = this.validVAccounts
      .sort((n1, n2) => {
        if (n1.Name > n2.Name) {
          return 1;
        } else if (n1.Name < n2.Name) {
          return -1;
        } else {
          return 0;
        }
      });
  }

  changeVAccounts(name?: any): void {
    this.showVAccounts(name);

    const invalid = !this.loadingVAccounts && this.validVAccounts && name ? !this.validVAccounts.some(a => a.Name === name) : false;
    this.vAccountInvalid = invalid;

    if (!name) {
      this.plugin.VaultAccount = null;
    }
  }
}
