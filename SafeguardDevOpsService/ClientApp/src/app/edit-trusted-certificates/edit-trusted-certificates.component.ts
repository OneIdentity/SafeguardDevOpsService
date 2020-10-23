import { Component, OnInit, Inject, ViewChild, AfterViewInit, ElementRef } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog } from '@angular/material/dialog';
import { DevOpsServiceClient } from '../service-client.service';
import { MatSelectionList } from '@angular/material/list';
import { MatDateFormats } from '@angular/material/core';
import { EnterPassphraseComponent } from '../upload-certificate/enter-passphrase/enter-passphrase.component';
import { Observable, of } from 'rxjs';
import { map, switchMap, tap, catchError, finalize } from 'rxjs/operators';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDrawer } from '@angular/material/sidenav';

@Component({
  selector: 'app-edit-trusted-certificates',
  templateUrl: './edit-trusted-certificates.component.html',
  styleUrls: ['./edit-trusted-certificates.component.scss']
})
export class EditTrustedCertificatesComponent implements OnInit, AfterViewInit {

  trustedCertificates: any[];
  useSsl: boolean;
  sel: any;

  @ViewChild('certificates', { static: false }) certList: MatSelectionList;
  @ViewChild('fileSelectInputDialog', { static: false }) fileSelectInputDialog: ElementRef;
  @ViewChild('drawer', { static: false }) drawer: MatDrawer;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog,
    private snackbar: MatSnackBar
  ) { }

  ngOnInit(): void {
    this.trustedCertificates = this.data?.trustedCertificates ?? [];
  }

  ngAfterViewInit(): void {
    this.certList.selectionChange.subscribe((x) => {
      this.sel = x;
      this.drawer.open();
    });
  }

  browse(): void {
    const e: HTMLElement = this.fileSelectInputDialog.nativeElement;
    e.click();
  }

  updateUseSsl(): void {
    this.serviceClient.putSafeguardUseSsl(this.useSsl).subscribe();
  }

  onChangeFile(files: FileList): void {
    if (!files[0]) {
      return;
    }

    const fileSelected = files[0];

    const fileReader = new FileReader();
    fileReader.onloadend = (e) => {
      const arrayBufferToString = (buffer) => {
        let binary = '';
        const bytes = new Uint8Array( buffer );
        const len = bytes.byteLength;
        for (let i = 0; i < len; i++) {
          binary += String.fromCharCode( bytes[ i ] );
        }
        return binary;
      };

      const pkcs12Der = arrayBufferToString(fileReader.result);
      const cert: string = btoa(pkcs12Der);

      const fileData = {
        fileType: fileSelected.type,
        fileContents: cert,
        fileName: fileSelected.name
      };

      this.getPassphrase(fileData).pipe(
        switchMap((data) => this.saveCertificate(data)),
        switchMap(() => this.refreshCertificates()),
        finalize(() => {
          // Clear the selection
          const input = this.fileSelectInputDialog.nativeElement as HTMLInputElement;
          input.value = null;
        })
      ).subscribe();
    };
    fileReader.readAsArrayBuffer(fileSelected);
  }

  private getPassphrase(fileData: any): Observable<any[]> {
    if (fileData?.fileType !== 'application/x-pkcs12') {
      return of([fileData]);
    }

    const ref = this.dialog.open(EnterPassphraseComponent, {
      data: { fileName: fileData.fileName }
    });

    return ref.afterClosed().pipe(
      // Emit fileData as well as passphrase
      // if passphraseData == undefined then they canceled the dialog
      map(passphraseData => (!passphraseData) ? [] : [fileData, passphraseData])
    );
  }

  private saveCertificate(resultArray: any[]): Observable<any> {
    const fileContents = resultArray[0]?.fileContents;
    if (!fileContents) {
      return of();
    }

    const passphrase = resultArray.length > 1 ? resultArray[1] : '';
    return this.serviceClient.postTrustedCertificates(false, fileContents, passphrase).pipe(
      catchError((err) => {
        if (err.error?.Message?.includes('specified network password is not correct')) {
          // bad password, have another try?
          // it's all we get
          this.snackbar.open(`The password for the certificate in ${resultArray[0].fileName} was not correct.`,
            'Dismiss', { duration: 5000 });
        }
        return of();
      })
    );
  }

  private refreshCertificates(): Observable<any[]> {
    return this.serviceClient.getTrustedCertificates().pipe(
      tap((certs) => {
        this.trustedCertificates.splice(0);
        this.trustedCertificates.push(...certs);
      })
    );
  }

  import(): void {
    this.serviceClient.postTrustedCertificates(true).subscribe(
      (data) => {
        this.trustedCertificates.splice(0);
        this.trustedCertificates.push(...data);
      }
    );
  }
}
