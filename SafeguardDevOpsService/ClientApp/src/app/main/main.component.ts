import { Component, OnInit, ViewChild, ElementRef, Renderer2 } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { switchMap, map, concatAll, tap, distinctUntilChanged, debounceTime, finalize, catchError, filter } from 'rxjs/operators';
import { of, Observable, fromEvent, forkJoin } from 'rxjs';
import { Router } from '@angular/router';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { UploadCertificateComponent } from '../upload-certificate/upload-certificate.component';
import { EnterPassphraseComponent } from '../upload-certificate/enter-passphrase/enter-passphrase.component';
import { CreateCsrComponent } from '../create-csr/create-csr.component';
import { ViewCertificateComponent } from '../view-certificate/view-certificate.component';
import * as $ from 'jquery';
import { ViewportScroller } from '@angular/common';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';
import { EditPluginService, EditPluginMode } from '../edit-plugin.service';
import { MatDrawer } from '@angular/material/sidenav';
import { AuthService } from '../auth.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { EditTrustedCertificatesComponent } from '../edit-trusted-certificates/edit-trusted-certificates.component';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';

@UntilDestroy()
@Component({
  selector: 'app-main',
  templateUrl: './main.component.html',
  styleUrls: ['./main.component.scss']
})
export class MainComponent implements OnInit {

  private snackBarDuration: number = 5000;

  constructor(
    private window: Window,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog,
    private router: Router,
    private renderer: Renderer2,
    public editPluginService: EditPluginService,
    private authService: AuthService,
    private snackBar: MatSnackBar
  ) { }

  UserName: string;
  UserDisplayName: string;
  IdentityProviderName: string;
  A2ARegistrationName: string;
  A2AVaultRegistrationName: string;
  Thumbprint: string;
  DevOpsInstanceId: string;
  ApplianceAddress: string;

  plugins = [];
  isLoading: boolean;
  openDrawer: string;

  certificateUploading = {
    Client: false,
    WebServer: false,
    trusted: false
  };
  webServerCertAdded: boolean = false;
  trustedCertsAdded: boolean = false;

  config: any;
  trustedCertificates = [];

  isMonitoring: boolean;
  isMonitoringAvailable: boolean;

  @ViewChild('drawer', { static: false }) drawer: MatDrawer;

  @ViewChild('fileSelectInputDialog', { static: false }) fileSelectInputDialog: ElementRef;

  @ViewChild('unconfigured', { static: false }) set contentUnconfigured(content: ElementRef) {
    if (content && !this.isLoading) {
      setTimeout(() => {
        this.setArrows();
      }, 500);
    }
  }

  ngOnInit(): void {
    this.isLoading = true;
    this.ApplianceAddress =  this.window.sessionStorage.getItem('ApplianceAddress');

    if (!this.ApplianceAddress || this.ApplianceAddress === 'null') {
      this.router.navigate(['/login']);
    } else {
      this.loginToDevOpsService()
        .pipe(
          untilDestroyed(this),
          switchMap(() => this.serviceClient.getConfiguration()),
          tap((config) => this.initializeConfig(config)),
          switchMap(() => forkJoin([
            this.initializeMonitoring(),
            this.initializeTrustedCertificates(),
            this.initializeWebServerCertificate(),
            this.initializePlugins()
          ])),
          finalize(() => this.isLoading = false)
        ).subscribe(() => {
          // Monitoring available when we have plugins and an account for at least one plugin
          this.isMonitoringAvailable = this.plugins.length > 1 && this.plugins.some(x => x.MappedAccountsCount > 0);
        });
    }
  }

  private initializeWebServerCertificate(): Observable<any> {
    if (!this.Thumbprint) {
      return of({});
    }
    return this.serviceClient.getWebServerCertificate().pipe(
      tap((cert) => {
        if (cert && cert.Subject !== 'CN=DevOpsServiceServerSSL') {
          this.webServerCertAdded = true;
        }
      }));
  }

  private initializePlugins(): Observable<any> {
    if (!this.Thumbprint) {
      return of({});
    }

    this.plugins.splice(0);
    const custom = {
      DisplayName: 'Upload Custom Plugin',
      IsUploadCustom: true,
      Accounts: []
    };
    this.plugins.push(custom);

    return this.serviceClient.getPlugins().pipe(
      tap((plugins: any[]) => {
        plugins.forEach(plugin => {
          plugin.IsConfigurationSetup = true;
          this.plugins.push(plugin);
        });
      }));
  }

  private initializeMonitoring(): Observable<any> {
    if (!this.Thumbprint) {
      return of({});
    }
    return this.serviceClient.getMonitor().pipe(
      tap((status) => {
        this.isMonitoring = status.Enabled;
      }));
  }

  private initializeTrustedCertificates(): Observable<any> {
    if (!this.Thumbprint) {
      return of({});
    }
    return this.serviceClient.getTrustedCertificates().pipe(
      tap((trustedCerts) => {
        this.trustedCertificates = trustedCerts;
        this.trustedCertsAdded = this.trustedCertificates?.length > 0;
      }));
  }

  private calculateArrow(A: HTMLElement, B: HTMLElement, index: number, totalArrows: number): string {
    const isUnconfigured = index === totalArrows - 1;

    const posA = {
      x: A.offsetLeft + 150 - index * 15,
      y: A.offsetTop + A.offsetHeight - 15 + this.window.scrollY
    };

    const markerOffset = this.isMonitoring && !isUnconfigured ? 22 : 50;
    const posB = {
      x: B.offsetLeft - markerOffset,
      y: B.offsetTop + B.offsetHeight / 2 - 5
    };

    return `M ${posA.x},${posA.y} V ${posB.y} a 3,3 0 0 0 3 3 H ${posB.x}`;
  }

  private setArrows(): void {
    const colors = [ 'CorbinOrange', 'MauiSunset', 'AspenGreenn', 'AzaleaPink' ];

    try {
      const configured = $('.configured');
      const unconfigured = $('.unconfigured')[0];
      const startEl =  $('.info-container')[0];
      const pathGroup = $('#svgGroup')[0];

      $('#svgGroup path').remove();

      const total = configured.length + 1;
      const all = configured.toArray();
      all.push(unconfigured);

      all.forEach((item, index) => {
        const dStr = this.calculateArrow(startEl, item, index, total);

        const pathEl = this.renderer.createElement('path', 'svg');
        pathEl.setAttribute('d', dStr);

        const isUnconfigured = index === total - 1;
        const color =  isUnconfigured || !this.isMonitoring ? 'Black9' :  colors[index % colors.length];

        pathEl.setAttribute('class', isUnconfigured || !this.isMonitoring ? 'arrow-unconfigured' : 'arrow');
        pathEl.setAttribute('marker-end', `url(#marker${color})`);

        this.renderer.appendChild(pathGroup, pathEl);
      });
    } catch {}
  }

  initializeConfig(config: any): void {
    this.ApplianceAddress =  config.Appliance.ApplianceAddress;
    this.DevOpsInstanceId = config.Appliance.DevOpsInstanceId;
    this.UserName = config.UserName;
    this.UserDisplayName = config.UserDisplayName;
    this.IdentityProviderName = config.IdentityProviderName;
    this.A2ARegistrationName = config.A2ARegistrationName;
    this.A2AVaultRegistrationName = config.A2AVaultRegistrationName;
    this.Thumbprint = config.Thumbprint;
  }

  loginToDevOpsService(): Observable<any> {
    return this.authService.getUserToken(this.ApplianceAddress)
      .pipe(
        switchMap((userTokenData) => {
          if (userTokenData?.Status === 'Success') {
            return this.serviceClient.getSafeguard();
          }
          return of();
        }),
        switchMap((safeguardData) => {
          if (!safeguardData.ApplianceAddress) {
            return this.serviceClient.putSafeguardAppliance(this.ApplianceAddress);
          }
          return of(undefined);
        }),
        switchMap(() => this.serviceClient.logon())
      );
  }

  createCSR(certificateType: string) {
    const dialogRef = this.dialog.open(CreateCsrComponent, {
      // disableClose: true
      data: {certificateType: certificateType}
    });

    dialogRef.afterClosed().subscribe(
      result => {
        if (result) {
        }
      }
    );
  }

  addCertificate(e: Event, certificateType: string): void {
    let certificateFileName: string = '';
    e.preventDefault();

    const dialogRef = this.dialog.open(UploadCertificateComponent, {
      data: { certificateType }
    });

    dialogRef.afterClosed().pipe(
      switchMap(
        (fileData) => {
          certificateFileName = fileData.fileName;

          if (fileData?.fileType !== 'application/x-pkcs12') {
            return of([fileData]);
          }

          const ref = this.dialog.open(EnterPassphraseComponent, {
            data: { fileName: fileData.fileName }
          });

          return ref.afterClosed().pipe(
            // Emit fileData as well as passphrase
            // if passphraseData == undefined then they canceled the dialog
            map(passphraseData => (passphraseData === undefined) ? [] : [fileData, passphraseData])
          );
        }
      ),
      switchMap(
        (resultArray) => {
          const fileContents = resultArray[0]?.fileContents;
          if (!fileContents) {
            return of();
          }

          const passphrase = resultArray.length > 1 ? resultArray[1] : '';
          this.certificateUploading[certificateType] = true;
          return certificateType === 'Client' ?
            this.serviceClient.postConfiguration(fileContents, passphrase) :
            this.serviceClient.postWebServerCertificate(fileContents, passphrase);
        }
      )
    ).subscribe(
      config => {
        if (certificateType === 'Client') {
          this.initializeConfig(config);
          this.initializePlugins();
          this.viewCertificate(null, 'Client');
        } else {
          this.webServerCertAdded = true;
        }
      },
      error => {
        if (error.error?.Message?.includes('specified network password is not correct')) {
          // bad password, have another try?
          // it's all we get
          this.snackBar.open('The password for the certificate in ' + certificateFileName + ' was not correct.', 'Dismiss', {duration: this.snackBarDuration});
        }
      }).add(() => {
        this.certificateUploading['Client'] = this.certificateUploading['WebServer'] = false;
      });
  }

  editPlugin(plugin: any): void {
    this.editPluginService.openProperties(plugin);
    this.openDrawer = 'properties';
    this.drawer.open();

    this.editPluginService.notifyEvent$.subscribe((data) => {
      switch (data.mode) {
        case EditPluginMode.Accounts:
          this.drawer.close();
          this.openDrawer = 'accounts';
          this.drawer.open();
          break;

        case EditPluginMode.VaultAccount:
          this.drawer.close();
          this.openDrawer = 'vaultaccount';
          this.drawer.open();
          break;

        case EditPluginMode.Properties:
          this.drawer.close();
          this.openDrawer = 'properties';
          this.drawer.open();
          break;

        case EditPluginMode.None:
          this.drawer.close();
          this.openDrawer = '';
          const indx = this.plugins.findIndex(x => x.Name === plugin.Name);
          if (indx > -1) {
            if (data.plugin) {
              this.plugins[indx] = data.plugin;
            } else {
              this.plugins.splice(indx, 1);
            }
            // Monitoring available when we have plugins and an account for at least one plugin
            this.isMonitoringAvailable = this.plugins.length > 1 && this.plugins.some(x => x.MappedAccountsCount > 0);
          }
          break;
      }
    });
  }

  uploadPlugin(): void {
    const e: HTMLElement = this.fileSelectInputDialog.nativeElement;
    e.click();
  }

  onChangeFile(files: FileList): void {
    if (!files[0]) {
      return;
    }

    const fileSelected = files[0];

    this.snackBar.open('Uploading plugin...');

    this.serviceClient.postPluginFile(fileSelected).pipe(
      finalize(() => {
        // Clear the selection
        const input = this.fileSelectInputDialog.nativeElement as HTMLInputElement;
        input.value = null;
      })
    ).subscribe((x: any) => {
      if (typeof x === 'string') {
        this.snackBar.open(x, 'OK', { duration: 10000 });
      } else {
        // This is a hack: if you call too quickly after uploading plugin, it returns zero
        setTimeout(() => {
          this.initializePlugins().subscribe();
          this.snackBar.dismiss();
        }, 2000);
      }
    });
  }

  updateMonitoring(enabled: boolean): void {
    this.serviceClient.postMonitor(enabled).pipe(
      untilDestroyed(this)
    ).subscribe(() => {
      this.isMonitoring = enabled;
      this.setArrows();
    });
  }

  logout(): void {
    this.serviceClient.logout().pipe(
      untilDestroyed(this)
    ).subscribe(() => {
      this.router.navigate(['/login']);
    });
  }

  restart(): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Restart Service',
        message: '<p>Are you sure you want to restart the Safeguard Secrets Broker for DevOps service?</p?>'
      }
    });

    dialogRef.afterClosed().pipe(
      filter((dlgResult: any) => dlgResult.result === 'OK'),
      tap(() => {
        this.snackBar.open('Restarting Safeguard Secrets Broker for DevOps service...');
        // Show overlay to disable clicking on anything
        this.drawer.open();
      }),
      switchMap(() => this.serviceClient.restart()),
      switchMap(() => this.serviceClient.logon()),
      finalize(() => {
        this.snackBar.open('Safeguard Secrets Broker for DevOps service restarted.', null, { duration: this.snackBarDuration });
        // Hide overlay
        this.drawer.close();
      })
    ).subscribe();
  }

  deleteConfig(): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete Configuration',
        message: '<p>Are you sure you want to remove all configuration for Safeguard Secrets Broker for DevOps?</p?>' +
          '<p>This removes all A2A credential retrievals, the A2A registration and the A2A user from Safeguard for Privileged Passwords.</p>' +
          '<p>It will also remove Safeguard Secrets Broker for DevOps configuration database and restart the DevOps service</p>'
      }
    });

    dialogRef.afterClosed().pipe(
      filter((dlgResult) => dlgResult.result === 'OK'),
      tap(() => {
        this.snackBar.open('Deleting configuration...');
        // Show overlay to disable clicking on anything
        this.drawer.open();
      }),
      switchMap(() => this.serviceClient.deleteConfiguration()),
      switchMap(() => this.serviceClient.logon()),
      switchMap(() => this.serviceClient.deleteSafeguard()),
      finalize(() => this.drawer.close())
    ).subscribe(() => {
      this.router.navigate(['/login']);
    });
  }

  viewCertificate(e: Event, certType: string = 'Client'): void {
    const dialogRef = this.dialog.open(ViewCertificateComponent, {
      data: { certificateType: certType }
    });

    dialogRef.afterClosed().subscribe(
      result => {
        if (result && result['removed']) {
          if (certType === 'Client') {
            window.location.reload();
          } else {
            this.webServerCertAdded = false;
          }
        }
      }
    );
  }

  viewTrustedCertificates(e: Event): void {
    e.preventDefault();

    const dialogRef = this.dialog.open(EditTrustedCertificatesComponent, {
      width: '500px',
      minHeight: '500px',
      data: { trustedCertificates: this.trustedCertificates }
    });

    dialogRef.afterClosed().subscribe();
  }
}
