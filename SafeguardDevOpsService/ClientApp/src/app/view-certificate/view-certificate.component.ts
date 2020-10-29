import { Component, OnInit, AfterViewInit, ElementRef, ViewChild, Inject } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { MatInput } from '@angular/material/input';
import * as $ from 'jquery';
import * as moment from 'moment-timezone';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { filter, switchMap } from 'rxjs/operators';

@Component({
  selector: 'app-view-certificate',
  templateUrl: './view-certificate.component.html',
  styleUrls: ['./view-certificate.component.scss']
})
export class ViewCertificateComponent implements OnInit, AfterViewInit {
  @ViewChild('subject', {static:false}) firstField: ElementRef;

  certificateType: string = '';
  certificateLoaded: boolean = false;
  removingCertificate: boolean = false;
  LocalizedValidFrom: string = '';
  typeDisplay = '';
  retrievedCert = {
  }

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog,
    private dialogRef: MatDialogRef<ViewCertificateComponent>
  ){}

  ngOnInit(): void {
    var sub = null;
    this.certificateType = this.data?.certificateType ?? '';
    switch (this.certificateType) {
      case 'Client':
        sub = this.serviceClient.getClientCertificate();
        this.typeDisplay = 'Client';
        break;
      case 'WebServer':
        sub = this.serviceClient.getWebServerCertificate();
        this.typeDisplay = 'Web Server';
        break;
    }
    if (sub) {
      sub.subscribe(
        cert => {
          this.retrievedCert = cert;
          this.LocalizedValidFrom = moment(cert.NotBefore).format('LLL (Z)') + ' - ' + moment(cert.NotAfter).format('LLL (Z)');
          this.certificateLoaded = true;
        },
        error => {
          var foo = error;
        }
      );
    }
  }

  fieldFocus() {
    if (this.firstField) {
      this.firstField.nativeElement.focus();
      this.firstField.nativeElement.setSelectionRange(0,0);
    } else {
      setTimeout(() => {
        this.fieldFocus();
      }, 0);
    }
  }
  ngAfterViewInit(): void {
    this.fieldFocus();
  }

  removeCertificate(): void {
    let dlgData;

    if (this.certificateType === 'Client') {
      dlgData = { title: 'Remove Client Certificate',
        message: 'Monitoring and pulling passwords will no longer be available if this certificate is removed.',
        confirmText: 'Remove Certificate' };
    } else if (this.certificateType === 'WebServer') {
      dlgData = { title: 'Delete Web Server Certificate',
        message: 'This will remove the current web server certificate and will generate a new self-signed certificate to take its place.',
        confirmText: 'Remove Certificate'};
    }

    if (dlgData) {
      const dialogRef = this.dialog.open(ConfirmDialogComponent, {
        data: dlgData
      });

      dialogRef.afterClosed().pipe(
        filter((dlgResult) => dlgResult.result === 'OK'),
        switchMap(() => {
          this.removingCertificate = true;
          return this.certificateType === 'Client' ?
            this.serviceClient.deleteClientCertificate() :
            this.serviceClient.deleteWebServerCertificate();
        })
      ).subscribe(
        () => {
          this.dialogRef.close({ removed: true });
        }
      ).add(() => { this.removingCertificate = false;});
    }
  }
}
