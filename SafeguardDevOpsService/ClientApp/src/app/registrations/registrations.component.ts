import { Component, OnInit, ViewChild, ElementRef, AfterViewInit, Output, EventEmitter } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { SelectionModel } from '@angular/cdk/collections';
import { fromEvent, Observable, merge, of } from 'rxjs';
import { distinctUntilChanged, debounceTime, switchMap, catchError } from 'rxjs/operators';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';
import { MatSort } from '@angular/material/sort';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';

@UntilDestroy()
@Component({
  selector: 'app-registrations',
  templateUrl: './registrations.component.html',
  styleUrls: ['./registrations.component.scss']
})
export class RegistrationsComponent implements OnInit, AfterViewInit {
  @ViewChild('registrationSearch', { static: false }) registrationSearchEl: ElementRef;
  @ViewChild(MatSort) sort: MatSort;
  @ViewChild(MatPaginator) paginator: MatPaginator;
  @Output() createNew: EventEmitter<number> = new EventEmitter();

  constructor(
    private serviceClient: DevOpsServiceClient
  ) { }

  registrations: any[];
  displayedColumns = ['select', 'AppName', 'Created', 'CreatedBy'];
  selection: SelectionModel<any> = new SelectionModel<any>(false, []);
  registrationSearchVal: string;
  sortColumn: string;
  sortDirection: string;
  isLoading: boolean;
  dataSource = new MatTableDataSource([]);
  totalCount = 0;
  currFilterStr: string;
  currSortby: string;
  page = 0;

  ngOnInit(): void {
    this.isLoading = true;
    this.serviceClient.getAvailableA2ARegistrations().pipe(
      untilDestroyed(this)
    ).subscribe((data: any[]) => {
      this.isLoading = false;
      this.registrations = [...data];
      this.totalCount = data.length;
      this.dataSource.data = this.registrations;

      if (this.totalCount == 0) {
        this.createNew.emit(0);
      }
    });
  }

  ngAfterViewInit(): void {
    merge(
      fromEvent(this.registrationSearchEl.nativeElement, 'keyup'),
      this.sort.sortChange,
      this.paginator.page
    ).pipe(
      untilDestroyed(this),
      debounceTime(500),
      distinctUntilChanged(),
      switchMap(() => this.doSearch()),
    ).subscribe((data) => {
      this.registrations = data;
      this.dataSource.data = this.registrations;
      this.isLoading = false;
    });
  }

  doSearch(): Observable<any[]> {
    this.registrations = [];
    this.dataSource.data = [];
    this.isLoading = true;

    let filterStr = '';
    let sortby = '';

    if (this.registrationSearchVal?.length > 0) {
      filterStr = `(AppName icontains '${this.registrationSearchVal}' or CreatedByUserDisplayName icontains '${this.registrationSearchVal}')`;
    }

    if (this.sort.active && this.sort.direction) {
      const dir = this.sort.direction === 'desc' ? '-' : '';

      if (this.sort.active === 'AppName') {
        sortby = `${dir}AppName`;
      }
    }

    if (this.currSortby !== sortby || this.currFilterStr !== filterStr) {
      this.page = 0;
      this.currFilterStr = filterStr;
      this.currSortby = sortby;

      return this.serviceClient.getAvailableA2ARegistrationsCount(filterStr, sortby).pipe(
        switchMap((count) => {
          this.totalCount = count;
          if (this.totalCount > 0) {
            return this.serviceClient.getAvailableA2ARegistrations(filterStr, sortby, this.paginator.pageIndex, this.paginator.pageSize);
          } else {
            return of([]);
          }
        }),
        catchError((error) => {
          return of([]);
        })
      );
    }

    return this.serviceClient.getAvailableA2ARegistrations(filterStr, sortby, this.paginator.pageIndex, this.paginator.pageSize);
  }

  selectRegistration(isNew: boolean): void {
    if (isNew) {
      this.createNew.emit(0);
    }
    else if (this.selection.selected.length == 1) {
      this.createNew.emit(this.selection.selected[0].Id);
    }
  }
}
