import { Component, OnInit, ViewChild, ElementRef, AfterViewInit, Input } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { SelectionModel } from '@angular/cdk/collections';
import { EditPluginService } from '../edit-plugin.service';
import { fromEvent, Observable, merge, of } from 'rxjs';
import { distinctUntilChanged, debounceTime, switchMap, filter, tap, finalize } from 'rxjs/operators';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';
import { MatSort, SortDirection } from '@angular/material/sort';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';

@UntilDestroy()
@Component({
  selector: 'app-select-accounts',
  templateUrl: './select-accounts.component.html',
  styleUrls: ['./select-accounts.component.scss']
})
export class SelectAccountsComponent implements OnInit, AfterViewInit {
  @ViewChild('assetSearch', { static: false }) assetSearchEl: ElementRef;
  @ViewChild('accountSearch', { static: false }) accountSearchEl: ElementRef;
  @ViewChild(MatSort) sort: MatSort;
  @ViewChild(MatPaginator) paginator: MatPaginator;

  @Input() selectVaultAccount: boolean;

  constructor(
    private serviceClient: DevOpsServiceClient,
    private editPluginService: EditPluginService,
    private window: Window
  ) { }

  accounts: any[];
  displayedColumns: string[];
  selection: SelectionModel<any>;
  assetSearchVal: string;
  accountSearchVal: string;
  pluginAccounts = [];
  sortColumn: string;
  sortDirection: string;
  isLoading: boolean;
  dataSource = new MatTableDataSource([]);
  totalCount = 0;
  currFilterStr: string;
  currSortby: string;
  page = 0;

  ngOnInit(): void {
    this.displayedColumns = !this.selectVaultAccount ? ['select', 'asset', 'account'] : ['asset', 'account'];
    this.selection = new SelectionModel<any>(!this.selectVaultAccount, []);

    this.pluginAccounts = this.editPluginService.plugin.Accounts;

    const sortColumn = this.window.sessionStorage.getItem('AccountsSortColumn');
    const sortDirection = this.window.sessionStorage.getItem('AccountsSortDirection');
    if ((sortColumn === 'account' || sortColumn === 'asset') && (sortDirection === 'asc' || sortDirection === 'desc')) {
      this.sortColumn = sortColumn;
      this.sortDirection = sortDirection;
    }

    this.isLoading = true;
    this.editPluginService.getAvailableAccounts().pipe(
      untilDestroyed(this),
      filter(() => !this.editPluginService.plugin.LoadingAvailableAccounts)
    ).subscribe(
      (data: any[]) => {
        this.isLoading = false;
        this.accounts = [...data];
        this.totalCount = this.editPluginService.availableAccountsTotalCount;
        this.hideCurrentAccounts();
        this.dataSource.data = this.accounts;
      }
    );
  }

  private hideCurrentAccounts(): void {
    // Don't show current managed accounts
    if (!this.selectVaultAccount) {
      this.pluginAccounts.forEach(account => {
        const indx = this.accounts.findIndex(x => x.Id === account.Id);
        if (indx > -1) {
          this.accounts.splice(indx, 1);
        }
      });
    }
  }

  ngAfterViewInit(): void {
    merge(
      fromEvent(this.assetSearchEl.nativeElement, 'keyup'),
      fromEvent(this.accountSearchEl.nativeElement, 'keyup'),
      this.sort.sortChange,
      this.paginator.page
    ).pipe(
        untilDestroyed(this),
        debounceTime(500),
        distinctUntilChanged(),
        switchMap(() => this.doSearch()),
      ).subscribe(
        (data) => {
          this.accounts = data;
          this.hideCurrentAccounts();
          this.dataSource.data = this.accounts;
          this.isLoading = false;
      });
  }

  doSearch(): Observable<any[]> {
    this.accounts = [];
    this.dataSource.data = [];
    this.isLoading = true;

    let filterStr = '';
    let sortby = '';

    if (this.assetSearchVal?.length > 0) {
      filterStr = `(SystemName contains '${this.assetSearchVal}' or SystemNetworkAddress contains '${this.assetSearchVal}')`;
    }
    if (this.accountSearchVal?.length > 0) {
      if (filterStr.length > 0) {
        filterStr += ' and ';
      }
      filterStr += `(Name contains '${this.accountSearchVal}' or DomainName contains '${this.accountSearchVal}')`;
    }

    if (this.sort.active && this.sort.direction) {
      const dir =  this.sort.direction === 'desc' ? '-' : '';

      if (this.sort.active === 'asset') {
        sortby = `${dir}SystemName,${dir}SystemNetworkAddress`;
      } else if (this.sort.active === 'account') {
        sortby = `${dir}Name,${dir}DomainName`;
      }
    }

    // Save sort settings
    if (this.sort.active !== this.sortColumn) {
      this.window.sessionStorage.setItem('AccountsSortColumn', this.sort.active);
      this.editPluginService.clearAvailableAccounts();
    }
    if (this.sort.direction !== this.sortDirection as SortDirection) {
      this.window.sessionStorage.setItem('AccountsSortDirection', this.sort.direction);
      this.editPluginService.clearAvailableAccounts();
    }

    if (this.currSortby !== sortby || this.currFilterStr !== filterStr) {
      this.page = 0;
      this.currFilterStr = filterStr;
      this.currSortby = sortby;

      return this.serviceClient.getAvailableAccountsCount(filterStr, sortby).pipe(
        switchMap((count) => {
          this.totalCount = count;
          if (this.totalCount > 0) {
            return this.serviceClient.getAvailableAccounts(filterStr, sortby, this.paginator.pageIndex, this.paginator.pageSize);
          } else {
            return of([]);
          }
        })
      );
    }

    return this.serviceClient.getAvailableAccounts(filterStr, sortby, this.paginator.pageIndex, this.paginator.pageSize);
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

  selectRow(event, row): void {
    event.stopPropagation();

    this.selection.toggle(row);

    if (this.selectVaultAccount) {
      this.editPluginService.closeVaultAccount(row);
    }
  }

  close(): void {
    this.selectVaultAccount ?
      this.editPluginService.closeVaultAccount() :
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
