import { Component, OnInit, ElementRef, ViewChild } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { Clipboard } from '@angular/cdk/clipboard';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { EnterPassphraseComponent } from './enter-passphrase/enter-passphrase.component';

@Component({
  selector: 'app-upload-certificate',
  templateUrl: './upload-certificate.component.html',
  styleUrls: ['./upload-certificate.component.scss']
})
export class UploadCertificateComponent implements OnInit {
  @ViewChild('fileSelectInputDialog', { static: false }) fileSelectInputDialog: ElementRef;

  subjectName: string;
  dnsSubjectAlternativeNames: string;
  ipSubjectAlternativeNames: string;
  showCsr: boolean;

  constructor(
    private serviceClient: DevOpsServiceClient,
    private clipboard: Clipboard,
    private dialogRef: MatDialogRef<UploadCertificateComponent>) { }

  ngOnInit(): void {
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
    const e: HTMLElement = this.fileSelectInputDialog.nativeElement;
    e.click();
  }

  onChangeFile(files: FileList): void {
    if (!files[0]) {
      return;
    }

    const fileSelected = files[0];

    const reader = new FileReader();
    reader.onloadend = (e) => {
      const matches = reader.result.toString().match(/^(data.*base64,)(.*)$/);

      this.dialogRef.close({
        fileType: fileSelected.type,
        fileContents: matches[2],
        fileName: fileSelected.name
      });
    };

    reader.readAsDataURL(fileSelected);
  }
}
