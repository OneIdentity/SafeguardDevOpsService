import { Component, OnInit } from '@angular/core';
import { EditPluginService } from '../edit-plugin.service';
import { DevOpsServiceClient } from '../service-client.service';
import { ServiceClientHelper as SCH } from '../service-client-helper';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';
import { switchMap, filter, tap, finalize } from 'rxjs/operators';
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
    private window: Window,
    private editPluginService: EditPluginService,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog,
    private snackBar: MatSnackBar
  ) { }

  error: any;
  isSaving = false;
  isTesting = false;
  isRestarting = false;
  isDeleting = false;
  reload = false;
  displayedColumns: string[] = ['asset', 'account', 'altaccount', 'delete'];

  public get plugin() {
    return this.editPluginService.plugin;
  }
  public set plugin(value: any) {
    this.editPluginService.plugin = value;
  }
  public get instanceIndex() {
    return this.editPluginService.instanceIndex;
  }
  public get isMultiInstance() {
    return this.editPluginService.pluginInstances.length > 1;
  }
  public get instanceCount() {
    return this.editPluginService.pluginInstances.length;
  }

  ngOnInit(): void {
    this.editPluginService.pluginInstances.forEach(p => {
      p.configs = [];
      Object.keys(p.Configuration).forEach(key => {
        p.configs.push({ key, value: p.Configuration[key] });
      });

      p.VaultAccountDisplayName = this.editPluginService.getVaultAccountDisplay(p.VaultAccount);
    });
  }

  selectVaultAccount(): void {
    // Save the current configuration first
    this.mapConfiguration(this.plugin);

    this.editPluginService.openVaultAccount();
  }

  removeVaultAccount(): void {
    this.plugin.VaultAccount = null;
    this.plugin.VaultAccountDisplayName = this.editPluginService.getVaultAccountDisplay(this.plugin.VaultAccount);
  }

  selectAccounts(): void {
    // Save the current configuration first
    this.mapConfiguration(this.plugin);

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
    this.editPluginService.closeProperties(false, this.reload);
  }

  goBack(): void {
    this.editPluginService.instanceIndex--;
  }

  goNext(): void {
    this.editPluginService.instanceIndex++;
  }

  instance(): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        showNo: true,
        title: 'New Instance',
        message: 'Would you like the new instance to copy settings from the current configuration?',
        confirmText: 'Yes'
      }
    });

    dialogRef.afterClosed().pipe(
      filter((dlgResult) => dlgResult?.result === 'OK' || dlgResult?.result === 'No'),
    ).subscribe((dlgResult) => {
      this.editPluginService.createInstance(this.plugin.Name, dlgResult?.result === 'OK')
        .subscribe(p => {
          p.configs = [];
          Object.keys(p.Configuration).forEach(key => {
            p.configs.push({ key, value: p.Configuration[key] });
          });
          p.Accounts = [];
          p.VaultAccountDisplayName = this.editPluginService.getVaultAccountDisplay(p.VaultAccount);

          this.editPluginService.pluginInstances.push(p);
          this.editPluginService.instanceIndex = this.editPluginService.pluginInstances.length - 1;
          this.reload = true;
        });
    },
      error => this.error = SCH.parseError(error)
    );
  }

  deleteInstance(): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete Instance',
        message: 'Are you sure you want to remove the configuration for this plugin instance?',
        confirmText: 'Delete Instance'
      }
    });

    dialogRef.afterClosed().pipe(
      filter((dlgResult) => dlgResult?.result === 'OK'),
      tap(() => this.isDeleting = true),
      switchMap(() => this.serviceClient.deletePluginConfiguration(this.plugin.Name)),
      finalize(() => this.isDeleting = false)
    ).subscribe(() => {
      this.editPluginService.pluginInstances.splice(this.instanceIndex, 1);

      if (this.editPluginService.instanceIndex > 0) {
        this.editPluginService.instanceIndex--;
      }
      this.reload = true;
    },
      error => this.error = SCH.parseError(error)
    );
  }

  delete(): void {
    var message = this.isMultiInstance ? ' all instances of ' : '';
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        showRestart: true,
        title: this.isMultiInstance ? 'Delete All Instances' : 'Delete Plugin',
        message:
          '<p>Are you sure you want to remove the configuration for' + message + 'this plugin and unregister the plugin from Safeguard Secrets Broker for DevOps?</p>' +
          '<p>This does not remove the plugin from the \\ProgramData\\SafeguardDevOpsService\\ExternalPlugins folder at this point.</p>' +
          '<p>The Safeguard Secrets Broker for DevOps service must be restarted to completely remove the deleted plugin.</p>',
        confirmText: this.isMultiInstance ? 'Delete All Instances' : 'Delete Plugin'
      }
    });

    dialogRef.afterClosed().pipe(
      filter((dlgResult) => dlgResult?.result === 'OK'),
      tap((dlgResult) => {
        this.isRestarting = dlgResult?.restart;
        this.isDeleting = true;
      }),
      switchMap((dlgResult) => this.editPluginService.deletePluginConfiguration(this.plugin.Name, this.isMultiInstance, dlgResult?.restart))
    ).subscribe(() => {
      this.editPluginService.deletePlugin();
      if (!this.isRestarting) {
        this.dialog.open(ConfirmDialogComponent, {
          data: {
            title: 'Next Steps',
            message: 'The Safeguard Secrets Broker for DevOps service must be restarted to complete the plugin removal from the \\ProgramData\\SafeguardDevOpsService\\ExternalPlugins folder. Select the "Restart Secrets Broker" option from the settings menu.',
            showCancel: false,
            confirmText: 'OK'
          }
        });
      }
    },
      error => {
        if (this.isRestarting) {
          setTimeout(() => {
            this.window.location.reload();
          }, 3000);
        } else {
          this.error = SCH.parseError(error);
        }
      });
  }

  testConnection(): void {
    this.error = null;
    this.isTesting = true;

    if (!this.plugin.IsSystemOwned) {
      this.saveConfiguration().pipe(
        switchMap(() =>
          this.serviceClient.postPluginTestConnection(this.plugin.Name)
        )).subscribe(() => {
          this.snackBar.open('Test configuration successful.', 'Dismiss', { duration: 5000 });
          this.isTesting = false;
        },
          error => {
            this.isTesting = false;
            this.error = SCH.parseError(error);
            this.snackBar.open('Test configuration failed: ' + this.error);
          });
    } else {
      this.serviceClient.postPluginTestConnection(this.plugin.Name)
        .subscribe(() => {
          this.snackBar.open('Test configuration successful.', 'Dismiss', { duration: 5000 });
          this.isTesting = false;
        },
          error => {
            this.isTesting = false;
            this.error = SCH.parseError(error);
            this.snackBar.open('Test configuration failed: ' + this.error);
          });
    }
  }

  private mapConfiguration(plugin: any): void {
    plugin.configs.forEach(config => {
      plugin.Configuration[config.key] = config.value;
    });
  }

  saveAll(): void {
    this.error = null;
    this.isSaving = true;

    var batch = [];
    this.editPluginService.pluginInstances.forEach(p => {
      batch.push(this.save(p));
    });

    forkJoin(batch).pipe(
      untilDestroyed(this)
    ).subscribe(() => {
      this.editPluginService.closeProperties(true, true);
    },
      error => {
        this.isSaving = false;
        this.error = SCH.parseError(error);
      }
    );
  }

  save(plugin: any): Observable<any> {
    this.mapConfiguration(plugin);

    // Make sure the accounts have AccountId, which PUT Plugin/Accounts expects
    plugin.Accounts.forEach(x => x.AccountId = x.Id);

    const obs1 = this.serviceClient.getPluginAccounts(plugin.Name).pipe(
      switchMap((accts) => {
        const deleted = [];
        accts.forEach(a => {
          if (!plugin.Accounts.find(x => x.AccountId === a.AccountId)) {
            deleted.push(a);
          }
        });
        if (deleted.length > 0) {
          return this.serviceClient.deletePluginAccounts(plugin.Name, deleted);
        } else {
          return of({});
        }
      }),
      switchMap(() => plugin.Accounts.length > 0 ?
        this.serviceClient.putRetrievableAccounts(plugin.Accounts) : of({})),
      switchMap(() => plugin.Accounts.length > 0 ?
        this.serviceClient.putPluginAccounts(plugin.Name, plugin.Accounts) : of([]))
    );

    if (!plugin.IsSystemOwned) {
      const obs2 = this.serviceClient.putPluginConfiguration(plugin.Name, plugin.Configuration, plugin.AssignedCredentialType);

      const obs3 = plugin.VaultAccount ?
        this.serviceClient.putPluginVaultAccount(plugin.Name, plugin.VaultAccount) :
        this.serviceClient.deletePluginVaultAccount(plugin.Name);

      const obs4 = this.serviceClient.postPluginDisableState(plugin.Name, plugin.IsDisabled);

      return forkJoin([obs1, obs2, obs3, obs4]);
    } else {
      return forkJoin([obs1]);
    }
  }

  saveConfiguration(): Observable<any> {
    this.mapConfiguration(this.plugin);

    const obs1 = this.serviceClient.putPluginConfiguration(this.plugin.Name, this.plugin.Configuration, this.plugin.AssignedCredentialType);
    const obs2 = this.plugin.VaultAccount ?
      this.serviceClient.putPluginVaultAccount(this.plugin.Name, this.plugin.VaultAccount) :
      this.serviceClient.deletePluginVaultAccount(this.plugin.Name);

    return forkJoin([obs1, obs2]).pipe(
      tap((data) => {
        const plugin = data[0];
        if (plugin) {
          this.plugin.Configuration = plugin.Configuration;
          this.plugin.AssignedCredentialType = plugin.AssignedCredentialType;
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
