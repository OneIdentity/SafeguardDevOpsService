import { Component, OnInit, ElementRef, ViewChild, Inject } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import * as $ from 'jquery';
import * as moment from 'moment-timezone';

@Component({
  selector: 'app-view-certificate',
  templateUrl: './view-certificate.component.html',
  styleUrls: ['./view-certificate.component.scss']
})
export class ViewCertificateComponent implements OnInit {
  certificateType: string = '';
  certificateLoaded: boolean = false;
  retrievedCert = {
  }

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog,
  ){}

  ngOnInit(): void {
    this.certificateType = this.data?.certificateType ?? '';
    this.serviceClient.getClientCertificate().subscribe(
      cert => {
        this.retrievedCert = cert;
        this.retrievedCert.LocalizedValidFrom = moment(cert.NotBefore).format('LLL (Z)') + ' - ' + moment(cert.NotAfter).format('LLL (Z)');
        this.certificateLoaded = true;
      },
      error => {
        var foo = error;
      }
    );
  }

  removeCertificate(): void {
    if (this.certificateType == 'Client' && confirm('Deleting this certificate will break things. Press "OK" to continue.')) {
      this.serviceClient.deleteClientCertificate().subscribe();
    }
  }
}
