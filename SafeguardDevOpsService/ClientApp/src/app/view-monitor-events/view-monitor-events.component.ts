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
  monitorStatus: string = "";
  a2aMonitorStatus: string = "";
  reverseFlowMonitorStatus: string = "";
  monitoringStatusMessage: string = "";
  monitoring: string = "";
  monitorStatusMessage: string = "";

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
    ).subscribe({
      next: (data: any[]) => {
        this.isLoading = false;
        this.events = [...data];
        this.dataSource.data = this.events;
      }
    });
    this.serviceClient.getMonitor().pipe(
      untilDestroyed(this),
    ).subscribe({
      next: (status: any) => {
        this.monitorStatus = status.Enabled;
        this.monitorStatusMessage = status.StatusMessage;
        this.reverseFlowMonitorStatus = status.ReverseFlowMonitorState.Enabled;
        this.monitoringStatusMessage = status.MonitoringStatusMessage;
      }
    });
  }

  close(): void {
    this.editPluginService.closeViewMonitorEvents();
  }

  GetMonitorStatus(): string {
    if (this.monitorStatus) {
      return "Running";
    }
    return "Stopped";
  }

  GetReverseFlowMonitorStatus(): string {
    if (this.reverseFlowMonitorStatus) {
      return "Running";
    }
    return "Stopped";
  }

}
