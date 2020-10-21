import { Component, OnInit, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { SelectionModel } from '@angular/cdk/collections';
import { EditPluginService, EditPluginMode } from '../edit-plugin.service';
import { fromEvent, Observable, of } from 'rxjs';
import { distinctUntilChanged, debounceTime, switchMap, tap } from 'rxjs/operators';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';

@UntilDestroy()
@Component({
  selector: 'app-select-accounts',
  templateUrl: './select-accounts.component.html',
  styleUrls: ['./select-accounts.component.scss']
})
export class SelectAccountsComponent implements OnInit, AfterViewInit {
  @ViewChild('assetSearch', { static: false }) assetSearchEl: ElementRef;
  @ViewChild('accountSearch', { static: false }) accountSearchEl: ElementRef;

  constructor(
    private serviceClient: DevOpsServiceClient,
    private editPluginService: EditPluginService
  ) { }

  accounts: any[];
  displayedColumns: string[] = ['select', 'asset', 'account'];
  selection = new SelectionModel(true, []);
  assetSearchVal: string;
  accountSearchVal: string;
  pluginAccounts = [];

  ngOnInit(): void {
    // TODO: loading icon
    this.editPluginService.notifyEvent$.pipe(
      untilDestroyed(this),
      switchMap((data) => {
        if (data.mode === EditPluginMode.Accounts) {
          console.log('edit accounts');
          console.log(data.plugin.Accounts);
          this.pluginAccounts.splice(0);
          this.pluginAccounts.push(...data.plugin.Accounts);

          return  this.serviceClient.getAvailableAccounts();
        }
        return of();
      })
    ).subscribe(
      (data: any[]) => {
        this.accounts = data;
        this.pluginAccounts.forEach(account => {
          const indx = data.findIndex(x => x.Id === account.Id);
          if (indx > -1) {
            this.accounts.splice(indx, 1);
          }
        });
      }
    );
  }

  ngAfterViewInit(): void {
    const searchChange = [ fromEvent(this.assetSearchEl.nativeElement, 'keyup'),
      fromEvent(this.accountSearchEl.nativeElement, 'keyup') ];

    searchChange.forEach(
      (x) => x.pipe(
        untilDestroyed(this),
        debounceTime(400),
        distinctUntilChanged(),
        switchMap(() => this.doSearch)
      ).subscribe(
        (data) => {
          this.accounts = data;
      }));
  }

  doSearch(): Observable<any[]> {
    let filter = '';
    if (this.assetSearchVal?.length > 0) {
      filter = `(SystemName contains '${this.assetSearchVal}' or SystemNetworkAddress contains '${this.assetSearchVal}')`;
    }
    if (this.accountSearchVal?.length > 0) {
      if (filter.length > 0) {
        filter += ' and ';
      }
      filter = `(Name contains '${this.accountSearchVal}' or DomainName contains '${this.accountSearchVal}')`;
    }
    return this.serviceClient.getAvailableAccounts(filter);
  }

  /** Whether the number of selected elements matches the total number of rows. */
  isAllSelected(): boolean {
    const numSelected = this.selection.selected.length;
    const numRows = this.accounts.length;
    return numSelected === numRows;
  }

  /** Selects all rows if they are not all selected; otherwise clear selection. */
  masterToggle(): void {
    this.isAllSelected() ?
        this.selection.clear() :
        this.accounts.forEach(row => this.selection.select(row));
  }

  close(): void {
    this.editPluginService.closeAccounts();
  }

  selectAccounts(): void {
    this.selection.selected.forEach(sel => {
      if (this.pluginAccounts.indexOf(x => x.Id === sel.Id) === -1) {
        this.pluginAccounts.push(sel);
      }
    });
    this.editPluginService.closeAccounts(this.pluginAccounts);
  }
}
