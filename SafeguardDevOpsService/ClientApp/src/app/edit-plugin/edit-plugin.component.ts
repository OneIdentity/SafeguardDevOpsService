import { Component, OnInit } from '@angular/core';
import { EditPluginService } from '../edit-plugin.service';
import { DevOpsServiceClient } from '../service-client.service';
import { ServiceClientHelper as SCH } from '../service-client-helper';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';
import { switchMap, filter, tap } from 'rxjs/operators';
import { of, forkJoin, Observable } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { MatSnackBar } from '@angular/material/snack-bar';

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
    private dialog: MatDialog,
    private snackBar: MatSnackBar
    ) { }

  plugin: any;
  configs = [];
  error: any;
  isSaving = false;
  isTesting = false;
  displayedColumns: string[] = ['asset', 'account', 'delete'];
  isPluginDisabled: boolean;

  ngOnInit(): void {
    this.plugin = this.editPluginService.plugin;
    this.isPluginDisabled = this.plugin.IsDisabled;

    this.configs.splice(0);
    Object.keys(this.plugin.Configuration).forEach(key => {
      this.configs.push({ key, value: this.plugin.Configuration[key] });
    });

    this.plugin.VaultAccountDisplayName = this.editPluginService.getVaultAccountDisplay(this.plugin.VaultAccount);
  }

  selectVaultAccount(): void {
    // Save the current configuration first
    this.mapConfiguration();

    this.editPluginService.openVaultAccount();
  }

  removeVaultAccount(): void {
    this.plugin.VaultAccount = null;
    this.plugin.VaultAccountDisplayName = this.editPluginService.getVaultAccountDisplay(this.plugin.VaultAccount);
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
      data: {
        title: 'Delete Plugin',
        message:
          '<p>Are you sure you want to remove the configuration for this plugin and unregister the plugin from Safeguard Secrets Broker for DevOps?</p>' +
          '<p>This does not remove the plugin from the \\ProgramData\\SafeguardDevOpsService\\ExternalPlugins folder at this point.</p>' +
          '<p>The Safeguard Secrets Broker for DevOps service must be restarted to completely remove the deleted plugin. Select the "Restart Secrets Broker" option from the settings menu.</p>',
        confirmText: 'Delete Plugin'
      }
    });

    dialogRef.afterClosed().pipe(
      filter((dlgResult) => dlgResult?.result === 'OK'),
      tap(() => {
        this.editPluginService.deletePlugin();
        this.snackBar.open('Deleting plugin...');
      }),
      switchMap(() => this.serviceClient.deletePluginConfiguration(this.plugin.Name))
    ).subscribe(
      () => {
        this.snackBar.dismiss();
        this.dialog.open(ConfirmDialogComponent, {
          data: {
            title: 'Next Steps',
            message: 'The Safeguard Secrets Broker for DevOps service must be restarted to complete the plugin removal from the \\ProgramData\\SafeguardDevOpsService\\ExternalPlugins folder. Select the "Restart Secrets Broker" option from the settings menu.',
            showCancel: false,
            confirmText: 'OK'
        }});
      }
    );
  }

  testConnection(): void {
    this.isTesting = true;
    this.snackBar.open('Testing Configuration...');
    this.saveConfiguration().pipe(
      switchMap(() =>
        this.serviceClient.postPluginTestConnection(this.plugin.Name)
      )).subscribe(() => {
        this.snackBar.open('Test configuration successful.', 'Dismiss', {duration: 5000});
        this.isTesting = false;
      },
      (error) => {
        this.snackBar.open('Test configuration failed.', 'Dismiss', { duration: 5000 });
        this.isTesting = false;
        this.error = SCH.parseError(error);
      });
  }

  updatePluginDisabled(): void {
    this.plugin.IsDisabled = this.isPluginDisabled;
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
      switchMap(() => this.plugin.Accounts.length > 0 ?
        this.serviceClient.putRetrievableAccounts(this.plugin.Accounts) : of({})),
      switchMap(() => this.plugin.Accounts.length > 0 ?
        this.serviceClient.putPluginAccounts(this.plugin.Name, this.plugin.Accounts) : of([]))
    );

    const obs2 = this.serviceClient.putPluginConfiguration(this.plugin.Name, this.plugin.Configuration);

    const obs3 = this.plugin.VaultAccount ?
      this.serviceClient.putPluginVaultAccount(this.plugin.Name, this.plugin.VaultAccount) :
      this.serviceClient.deletePluginVaultAccount(this.plugin.Name);

    const obs4 = this.serviceClient.postPluginDisableState(this.plugin.Name, this.plugin.IsDisabled);

    forkJoin([obs1, obs2, obs3, obs4]).pipe(
      untilDestroyed(this)
    ).subscribe(
      (data) => {
        this.plugin = data[1];
        this.plugin.Accounts =  data[0];
        this.plugin.MappedAccountsCount = this.plugin.Accounts.length;
        this.plugin.IsDisabled = data[3].Disabled;

        this.editPluginService.closeProperties(this.plugin);
      },
      (error) => {
        this.error = SCH.parseError(error);
      }
    );
  }

  saveConfiguration(): Observable<any> {
    this.mapConfiguration();

    const obs1 = this.serviceClient.putPluginConfiguration(this.plugin.Name, this.plugin.Configuration);
    const obs2 = this.plugin.VaultAccount ?
      this.serviceClient.putPluginVaultAccount(this.plugin.Name, this.plugin.VaultAccount) :
      this.serviceClient.deletePluginVaultAccount(this.plugin.Name);

    return forkJoin([obs1, obs2]).pipe(
      tap((data) => {
        const plugin = data[0];
        if (plugin) {
          this.plugin.Configuration = plugin.Configuration;
        }

        const vaultAccount = data[1];
        if (vaultAccount) {
          this.plugin.VaultAccount = {
            Id: vaultAccount.AccountId,
            Name: vaultAccount.AccountName,
            DomainName: vaultAccount.DomainName,
            SystemName: vaultAccount.SystemName,
            SystemNetworkAddress: vaultAccount.NetworkAddress
          };
          this.plugin.VaultAccountDisplayName = this.editPluginService.getVaultAccountDisplay(this.plugin.VaultAccount);
        }
     })
    );
  }
}
