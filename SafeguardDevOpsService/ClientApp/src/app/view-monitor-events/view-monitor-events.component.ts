import { Component, OnInit, ViewChild, AfterViewInit } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { EditPluginService } from '../edit-plugin.service';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';

@UntilDestroy()
@Component({
  selector: 'app-view-monitor-events',
  templateUrl: './view-monitor-events.component.html',
  styleUrls: ['./view-monitor-events.component.scss']
})
export class ViewMonitorEventsComponent implements OnInit, AfterViewInit {

  @ViewChild(MatPaginator) paginator: MatPaginator;

  constructor(
    private serviceClient: DevOpsServiceClient,
    private editPluginService: EditPluginService,
  ) { }

  events: any[];
  displayedColumns: string[];
  isLoading: boolean;
  dataSource = new MatTableDataSource([]);
  totalCount = 0;

  ngOnInit(): void {
    this.displayedColumns = ['Event', 'Date'];
    this.loadData();
  }

  ngAfterViewInit(): void {
    this.dataSource.paginator = this.paginator;
  }

  refresh(): void {
    this.loadData();
  }

  private loadData(): void {
    this.isLoading = true;
    this.serviceClient.getMonitorEvents().pipe(
      untilDestroyed(this),
    ).subscribe(
      (data: any[]) => {
        this.isLoading = false;
        this.events = [...data];
        this.dataSource.data = this.events;
      }
    );
  }

  close(): void {
    this.editPluginService.closeViewMonitorEvents();
  }

}
