import { Component, OnInit, ElementRef, ViewChild, Inject } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { Clipboard } from '@angular/cdk/clipboard';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { EnterPassphraseComponent } from './enter-passphrase/enter-passphrase.component';
import * as $ from 'jquery';

@Component({
  selector: 'app-upload-certificate',
  templateUrl: './upload-certificate.component.html',
  styleUrls: ['./upload-certificate.component.scss']
})
export class UploadCertificateComponent implements OnInit {

  subjectName: string;
  dnsSubjectAlternativeNames: string;
  ipSubjectAlternativeNames: string;
  keySize: number = 2048;
  showCsr: boolean;
  certificateType: string = '';

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private serviceClient: DevOpsServiceClient,
    private clipboard: Clipboard,
    private dialog: MatDialog,
    private dialogRef: MatDialogRef<UploadCertificateComponent>) { }

  ngOnInit(): void {
    this.certificateType = this.data?.certificateType ?? '';
  }

  createCSR(e: any, action: string): void {
    e.preventDefault();

    this.serviceClient.getCSR('A2AClient', this.subjectName, this.dnsSubjectAlternativeNames, this.ipSubjectAlternativeNames)
      .subscribe(
        (csr) => {
          if (action === 'download') {
            this.downloadString(csr);
          } else if (action === 'copy') {
            this.copyToClipboard(csr);
          }
        });
  }

  private downloadString(str: string): void {
    const blob = new Blob([str], {type: 'text/plain'});

    const link = document.createElement('a');
    link.download = 'CSR.txt';
    link.href = window.URL.createObjectURL(blob);
    link.click();
  }

  private copyToClipboard(str: string): void {
    const pending = this.clipboard.beginCopy(str);

    let remainingAttempts = 3;
    const attempt = () => {
      const result = pending.copy();
      if (!result && --remainingAttempts) {
        setTimeout(attempt);
      } else {
        pending.destroy();
      }
    };
    attempt();
  }

  browse(): void {
    var fileInput = $('<input class="requestedCertInput" type="file" accept=".cer,.crt,.der,.pfx,.p12,.pem" />');

    fileInput.on('change',() => {
      const fileSelected = fileInput.prop('files')[0];
      
      if (!fileSelected) {
        return;
      }

      const fileReader = new FileReader();
      fileReader.onloadend = (e) => {
        let arrayBufferToString = (buffer) => {
          var binary = '';
          var bytes = new Uint8Array( buffer );
          var len = bytes.byteLength;
          for (var i = 0; i < len; i++) {
            binary += String.fromCharCode( bytes[ i ] );
          }
          return binary;
        };

        var pkcs12Der = arrayBufferToString(fileReader.result);
        let cert:string = btoa(pkcs12Der);
        this.dialogRef.close({
          fileType: fileSelected.type,
          fileContents: cert,
          fileName: fileSelected.name
        });
      };
      fileReader.readAsArrayBuffer(fileSelected);
    });
    $(".requestedCertInput").append(fileInput);
    fileInput.trigger("click");
  }

}
