import { Component, OnInit, AfterViewInit, ElementRef, ViewChild, Inject } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { MatInput } from '@angular/material/input';
import * as $ from 'jquery';
import * as moment from 'moment-timezone';

@Component({
  selector: 'app-view-certificate',
  templateUrl: './view-certificate.component.html',
  styleUrls: ['./view-certificate.component.scss']
})
export class ViewCertificateComponent implements OnInit, AfterViewInit {
  @ViewChild('subject', {static:false}) firstField: ElementRef;

  certificateType: string = '';
  certificateLoaded: boolean = false;
  LocalizedValidFrom: string = '';
  retrievedCert = {
  }

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog,
  ){}

  ngOnInit(): void {
    var sub = null;
    this.certificateType = this.data?.certificateType ?? '';
    switch (this.certificateType) {
      case 'Client':
        sub = this.serviceClient.getClientCertificate();
        break;
      case 'Web':
        sub = null;
        break;
      case 'Trusted': 
        sub = null
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
    if (this.certificateType == 'Client' && confirm('Deleting this certificate will break things. Press "OK" to continue.')) {
      this.serviceClient.deleteClientCertificate().subscribe();
    }
  }
}
