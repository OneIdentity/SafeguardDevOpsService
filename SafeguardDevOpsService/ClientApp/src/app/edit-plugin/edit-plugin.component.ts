import { Component, OnInit } from '@angular/core';
import { EditPluginService, EditPluginMode } from '../edit-plugin.service';
import { DevOpsServiceClient } from '../service-client.service';
import { ServiceClientHelper as SCH } from '../service-client-helper';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';
import { finalize, switchMap, tap } from 'rxjs/operators';
import { of, forkJoin } from 'rxjs';
import { MatAutocompleteModule } from '@angular/material/autocomplete';

@Component({
  selector: 'app-edit-plugin',
  templateUrl: './edit-plugin.component.html',
  styleUrls: ['./edit-plugin.component.scss']
})

@UntilDestroy()
export class EditPluginComponent implements OnInit {

  constructor(
    private editPluginService: EditPluginService,
    private serviceClient: DevOpsServiceClient
    ) { }

  plugin: any;
  configs = [];
  error: any;
  isSaving = false;
  displayedColumns: string[] = ['asset', 'account', 'delete'];

  // Vault account
  vaultAccount: any;
  vaultAccountStr: string;
  vAccountInvalid = false;
  allVAccounts = [];
  validVAccounts = [];
  loadingVAccounts: boolean;

  ngOnInit(): void {
    this.editPluginService.notifyEvent$.pipe(
      untilDestroyed(this)
    ).subscribe(
      (data) => {
        if (data.mode === EditPluginMode.Properties) {
          this.plugin = data.plugin;

          this.configs.splice(0);
          Object.keys(this.plugin.Configuration).forEach(key => {
            this.configs.push({ key, value: this.plugin.Configuration[key] });
          });

          this.serviceClient.getPluginVaultAccount(this.plugin.Name).pipe(
            untilDestroyed(this)
          ).subscribe(
            (vAcct) => {
              console.log(vAcct);
              this.vaultAccount = {
                Id: vAcct.AccountId,
                Name: vAcct.AccountName,
                System: vAcct.DomainName ?? (vAcct.SystemName ?? vAcct.SystemNetworkAddress)
              };
              this.vaultAccountStr = `${this.vaultAccount.Name} (${this.vaultAccount.System})`;
            });
        }
      });

    this.loadingVAccounts = true;
    this.serviceClient.getAvailableAccounts().pipe(
      untilDestroyed(this)
    ).subscribe(
      (data) => {
        this.allVAccounts = data;
        this.loadingVAccounts = false;
      }
    );
  }

  selectAccounts(): void {
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
    // TODO: put up a confirmation dialog
    this.serviceClient.deletePluginConfiguration(this.plugin.Name).pipe(
      untilDestroyed(this)
    ).subscribe(
      () => this.editPluginService.deletePlugin()
    );
  }

  save(): void {
    this.error = null;
    this.isSaving = true;

    this.configs.forEach(config => {
      this.plugin.Configuration[config.key] = config.value;
    });

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
      switchMap(() => this.serviceClient.putRetrievableAccounts(this.plugin.Accounts)),
      switchMap(() => this.serviceClient.putPluginAccounts(this.plugin.Name, this.plugin.Accounts))
    );

    const obs2 = this.serviceClient.putPluginConfiguration(this.plugin.Name, this.plugin.Configuration);

    const obs3 = this.vaultAccount ?
      this.serviceClient.putPluginVaultAccount(this.plugin.Name, this.vaultAccount) :
      this.serviceClient.deletePluginVaultAccount(this.plugin.Name);

    forkJoin([obs1, obs2, obs3]).pipe(
      untilDestroyed(this)
    ).subscribe(
      (data) => {
        this.plugin = data[1];
        this.plugin.Accounts =  data[0];

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
    this.vaultAccount = event.option.value;
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
      this.vaultAccount = null;
    }
  }
}
