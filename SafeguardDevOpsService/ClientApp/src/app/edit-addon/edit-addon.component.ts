import { Component, OnInit } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { UntilDestroy } from '@ngneat/until-destroy';
import { switchMap, filter, tap } from 'rxjs/operators';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { EditAddonService } from '../edit-addon.service';

@Component({
  selector: 'app-edit-addon',
  templateUrl: './edit-addon.component.html',
  styleUrls: ['./edit-addon.component.scss']
})

@UntilDestroy()
export class EditAddonComponent implements OnInit {

  constructor(
    private window: Window,
    private editAddonService: EditAddonService,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog
  ) { }

  addon: any;
  error: any;
  isRestarting = false;
  isDeleting = false;

  ngOnInit(): void {
    this.addon = this.editAddonService.addon;
  }

  close(): void {
    this.editAddonService.closeProperties();
  }

  delete(): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        showRestart: true,
        title: 'Delete Add-on',
        message:
          '<p>Are you sure you want to remove the configuration for this system owned plugin and unregister the add-on from Safeguard Secrets Broker for DevOps?</p>' +
          '<p>This does not remove the plugin from the \\ProgramData\\SafeguardDevOpsService\\ExternalPlugins folder at this point.</p>' +
          '<p>The Safeguard Secrets Broker for DevOps service must be restarted to completely remove the deleted add-on.</p>',
        confirmText: 'Delete Add-on'
      }
    });

    dialogRef.afterClosed().pipe(
      filter((dlgResult) => dlgResult?.result === 'OK'),
      tap((dlgResult) => {
        this.isRestarting = dlgResult?.restart;
        this.isDeleting = true;
      }),
      switchMap((dlgResult) => this.serviceClient.deleteAddonConfiguration(this.addon.Name, dlgResult?.restart))
    ).subscribe(() => {
      if (!this.isRestarting) {
        this.dialog.open(ConfirmDialogComponent, {
          data: {
            title: 'Next Steps',
            message: 'The Safeguard Secrets Broker for DevOps service must be restarted to complete the add-on and plugin removal from the \\ProgramData\\SafeguardDevOpsService\\ExternalPlugins folder. Select the "Restart Secrets Broker" option from the settings menu.',
            showCancel: false,
            confirmText: 'OK'
          }
        });
      }
    },
      error => {
        setTimeout(() => {
          this.window.location.reload();
        }, 3000);
    });
  }
}
