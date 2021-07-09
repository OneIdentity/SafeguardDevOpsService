import { Component, OnInit } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { UntilDestroy } from '@ngneat/until-destroy';
import { switchMap, filter, tap } from 'rxjs/operators';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { MatSnackBar } from '@angular/material/snack-bar';
import { EditAddonService } from '../edit-addon.service';

@Component({
  selector: 'app-edit-addon',
  templateUrl: './edit-addon.component.html',
  styleUrls: ['./edit-addon.component.scss']
})

@UntilDestroy()
export class EditAddonComponent implements OnInit {

  constructor(
    private editAddonService: EditAddonService,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog,
    private snackBar: MatSnackBar
    ) { }

  addon: any;
  error: any;

  ngOnInit(): void {
    this.addon = this.editAddonService.addon;
  }

  close(): void {
    this.editAddonService.closeProperties();
  }

  delete(): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete Addon',
        message:
          '<p>Are you sure you want to remove the configuration for this system owned plugin and unregister the addon from Safeguard Secrets Broker for DevOps?</p>' +
          '<p>This does not remove the plugin from the \\ProgramData\\SafeguardDevOpsService\\ExternalPlugins folder at this point.</p>' +
          '<p>The Safeguard Secrets Broker for DevOps service must be restarted to completely remove the deleted addon. Select the "Restart Secrets Broker" option from the settings menu.</p>',
        confirmText: 'Delete Addon'
      }
    });

    dialogRef.afterClosed().pipe(
      filter((dlgResult) => dlgResult?.result === 'OK'),
      tap(() => {
        this.editAddonService.deleteAddon();
        this.snackBar.open('Deleting addon...');
      }),
      switchMap(() => this.serviceClient.deleteAddonConfiguration(this.addon.Name))
    ).subscribe(
      () => {
        this.snackBar.dismiss();
        this.dialog.open(ConfirmDialogComponent, {
          data: {
            title: 'Next Steps',
            message: 'The Safeguard Secrets Broker for DevOps service must be restarted to complete the addon and plugin removal from the \\ProgramData\\SafeguardDevOpsService\\ExternalPlugins folder. Select the "Restart Secrets Broker" option from the settings menu.',
            showCancel: false,
            confirmText: 'OK'
        }});
      }
    );
  }
}
