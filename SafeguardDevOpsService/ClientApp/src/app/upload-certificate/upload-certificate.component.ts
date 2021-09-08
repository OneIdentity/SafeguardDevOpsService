import { Component, OnInit, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import * as $ from 'jquery';

@Component({
  selector: 'app-upload-certificate',
  templateUrl: './upload-certificate.component.html',
  styleUrls: ['./upload-certificate.component.scss']
})
export class UploadCertificateComponent implements OnInit {

  certificateType: string = '';
  certificate: any;
  needsWebCertificate: boolean;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private dialogRef: MatDialogRef<UploadCertificateComponent>) { }

  ngOnInit(): void {
    this.certificateType = this.data?.certificateType ?? '';
    this.certificate = this.data?.certificate;
    this.needsWebCertificate = this.data?.needsWebCertificate;
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
          result: UploadCertificateResult.UploadCertificate,
          data: {
            fileType: fileSelected.type,
            fileContents: cert,
            fileName: fileSelected.name
          }
        });
      };
      fileReader.readAsArrayBuffer(fileSelected);
    });
    $(".requestedCertInput").append(fileInput);
    fileInput.trigger("click");
  }

  createCSR(): void {
    this.dialogRef.close({ result: UploadCertificateResult.CreateCSR });
  }

  viewCertificate(): void {
    this.dialogRef.close({ result: UploadCertificateResult.ViewCertificate });
  }
}

export enum UploadCertificateResult {
  ViewCertificate,
  UploadCertificate,
  CreateCSR
}
