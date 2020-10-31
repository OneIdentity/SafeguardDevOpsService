import { Component, OnInit, AfterViewInit, ElementRef, ViewChild, Inject } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import * as moment from 'moment-timezone';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { filter, switchMap } from 'rxjs/operators';
import { UploadCertificateComponent } from '../upload-certificate/upload-certificate.component';
import { of } from 'rxjs';

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
  spinnerMessage = 'Loading';
  retrievedCert = {};
  error = null;
  showBackButton: boolean;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog,
    private dialogRef: MatDialogRef<ViewCertificateComponent>
  ){}

  ngOnInit(): void {
    let sub = null;
    this.certificateType = this.data?.certificateType ?? '';
    this.retrievedCert = this.data?.certificate;

    switch (this.certificateType) {
      case 'Client':
        sub = this.serviceClient.getClientCertificate();
        this.typeDisplay = 'Client';
        break;
      case 'Web Server':
        sub = this.retrievedCert ? of(this.retrievedCert) : this.serviceClient.getWebServerCertificate();
        this.typeDisplay = 'Web Server';
        break;
    }
    if (sub) {
      sub.subscribe(
        cert => {
          this.retrievedCert = cert;
          this.LocalizedValidFrom = moment(cert.NotBefore).format('LLL (Z)') + ' - ' + moment(cert.NotAfter).format('LLL (Z)');
          this.certificateLoaded = true;

          // Show Back button instead of Remove if this is an auto-generated web server certificate
          this.showBackButton = this.certificateType === 'Web Server' &&
            cert.Subject === 'CN=DevOpsServiceServerSSL' &&
            cert.IssuedBy === cert.Subject;
        },
        error => {
          this.error = error;
        }
      );
    }
  }

  fieldFocus(): void {
    if (this.firstField) {
      this.firstField.nativeElement.focus();
      this.firstField.nativeElement.setSelectionRange(0, 0);
    } else {
      setTimeout(() => {
        this.fieldFocus();
      }, 0);
    }
  }
  ngAfterViewInit(): void {
    this.fieldFocus();
  }

  addCertificate(certType: string): void {
    this.dialogRef.close();

    this.dialog.open(UploadCertificateComponent, {
      data: { certificateType: certType, certificate: this.retrievedCert }
    });
  }

  removeCertificate(): void {
    let dlgData;
    this.error = null;

    if (this.certificateType === 'Client') {
      dlgData = { title: 'Remove Client Certificate',
        message: '<p>Are you sure you want to remove the client certificate?</p><p>Monitoring and pulling passwords will no longer be available if this certificate is removed.</p>',
        confirmText: 'Remove Certificate' };
    } else if (this.certificateType === 'Web Server') {
      dlgData = { title: 'Remove Web Server Certificate',
        message: '<p>Are you sure you want to remove the web server certificate?</p><p>This will generate a new self-signed certificate to take its place and the Safeguard Secrets Broker for DevOps service will restart.</p>',
        confirmText: 'Remove Certificate' };
    }

    if (dlgData) {
      const dialogRef = this.dialog.open(ConfirmDialogComponent, {
        data: dlgData
      });

      dialogRef.afterClosed().pipe(
        filter((dlgResult) => dlgResult?.result === 'OK'),
        switchMap(() => {
          this.removingCertificate = true;
          this.spinnerMessage = 'Removing ' + this.typeDisplay + ' Certificate';
          return this.certificateType === 'Client' ?
            this.serviceClient.deleteClientCertificate() :
            this.serviceClient.deleteWebServerCertificate();
        })
      ).subscribe(
        () => {
          this.dialogRef.close({ removed: true });
        },
        (error) => {
          this.error = error;
        }
      ).add(() => { this.removingCertificate = false; this.spinnerMessage='';});
    }
  }
}
