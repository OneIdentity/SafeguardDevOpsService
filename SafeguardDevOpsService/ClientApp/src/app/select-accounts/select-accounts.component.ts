import { Component, OnInit, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { SelectionModel } from '@angular/cdk/collections';
import { EditPluginService } from '../edit-plugin.service';
import { fromEvent, Observable } from 'rxjs';
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

  ngOnInit(): void {
    this.serviceClient.getAvailableAccounts().subscribe(
      (data) => {
        this.accounts = data;
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

  close(accounts: any[]): void {
    this.editPluginService.closeAccounts(accounts);
  }
}
