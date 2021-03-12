import { Component, OnInit, ViewChild, ViewChildren, ElementRef, Renderer2, HostListener, AfterViewInit, QueryList } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { switchMap, map, tap, finalize, filter } from 'rxjs/operators';
import { of, Observable, forkJoin } from 'rxjs';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { UploadCertificateComponent, UploadCertificateResult } from '../upload-certificate/upload-certificate.component';
import { EnterPassphraseComponent } from '../upload-certificate/enter-passphrase/enter-passphrase.component';
import { CreateCsrComponent, CreateCsrResult } from '../create-csr/create-csr.component';
import { ViewCertificateComponent, ViewCertificateResult } from '../view-certificate/view-certificate.component';
import * as $ from 'jquery';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';
import { EditPluginService, EditPluginMode } from '../edit-plugin.service';
import { MatDrawer } from '@angular/material/sidenav';
import { AuthService } from '../auth.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { EditTrustedCertificatesComponent } from '../edit-trusted-certificates/edit-trusted-certificates.component';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { HttpEventType, HttpResponse } from '@angular/common/http';
import { ViewMonitorEventsComponent } from '../view-monitor-events/view-monitor-events.component';

@UntilDestroy()
@Component({
  selector: 'app-main',
  templateUrl: './main.component.html',
  styleUrls: ['./main.component.scss']
})
export class MainComponent implements OnInit, AfterViewInit {

  private snackBarDuration: number = 5000;
  private viewMonitorEventsRef : ViewMonitorEventsComponent;

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
  DevOpsVersion: string;

  error: any = null;

  plugins = [];
  isLoading: boolean;
  openDrawer: string;

  certificateUploading = {
    Client: false,
    WebServer: false,
    trusted: false
  };
  webServerCertAdded: boolean = false;
  webServerCert: any;

  config: any;
  trustedCertificates = [];

  isMonitoring: boolean;
  isMonitoringAvailable: boolean;

  unconfiguredDiv: any;
  footerAbsolute: boolean = true;

  @ViewChild('drawer', { static: false }) drawer: MatDrawer;

  @ViewChild('fileSelectInputDialog', { static: false }) fileSelectInputDialog: ElementRef;

  @ViewChild('unconfigured', { static: false }) set contentUnconfigured(content: ElementRef) {
    if (content && !this.isLoading) {
      this.unconfiguredDiv = content;
      setTimeout(() => {
        this.setArrows();
        this.setFooter();
      }, 500);
    }
  }

  @ViewChildren(ViewMonitorEventsComponent) viewMonitorEventsRefs: QueryList<ViewMonitorEventsComponent>;

  @HostListener('window:resize', ['$event'])
  onResize() {
    setTimeout(() => {
      this.setFooter();
    }, 500);
  }

  ngOnInit(): void {
    this.isLoading = true;
    this.error = null;

    const reload = this.window.sessionStorage.getItem('reload');
    if (reload) {
      this.window.sessionStorage.removeItem('reload');
      this.window.location.reload();
    }

    this.initializeApplianceAddressAndLogin()
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
      },
      error => {
        this.error = error;
      });
  }

  ngAfterViewInit(): void {
    this.viewMonitorEventsRefs.changes.subscribe((comps: QueryList <ViewMonitorEventsComponent>) =>
    {
        this.viewMonitorEventsRef = comps.first;
    });
  }

  private initializeWebServerCertificate(): Observable<any> {
    this.error = null;
    if (!this.Thumbprint) {
      return of({});
    }
    return this.serviceClient.getWebServerCertificate().pipe(
      tap((cert) => {
        if (cert && cert.Subject !== 'CN=DevOpsServiceServerSSL') {
          this.webServerCertAdded = true;
        }
        this.webServerCert = cert;
      }));
  }

  private initializePlugins(): Observable<any> {
    this.error = null;
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

        this.updateMonitoringAvailable();
      }));
  }

  private initializeMonitoring(): Observable<any> {
    this.error = null;
    if (!this.Thumbprint) {
      return of({});
    }
    return this.serviceClient.getMonitor().pipe(
      tap((status) => {
        this.isMonitoring = status.Enabled;
      }));
  }

  private initializeTrustedCertificates(): Observable<any> {
    this.error = null;
    if (!this.Thumbprint) {
      return of({});
    }
    return this.serviceClient.getTrustedCertificates().pipe(
      tap((trustedCerts) => {
        this.trustedCertificates = trustedCerts;
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

  private setFooter(): void {
    if (!this.unconfiguredDiv) {
      return;
    }
    const elBottom = this.unconfiguredDiv.nativeElement.offsetTop + this.unconfiguredDiv.nativeElement.offsetHeight + 80;
    this.footerAbsolute = (elBottom < this.window.innerHeight);
  }

  private setArrows(): void {
    const colors = [ 'CorbinOrange', 'MauiSunset', 'AspenGreen', 'AzaleaPink' ];

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
        pathEl.setAttribute('marker-end', `url(${this.window.location.href}#marker${color})`);

        this.renderer.appendChild(pathGroup, pathEl);
      });
    } catch {}
  }

  initializeConfig(config: any): void {
    this.ApplianceAddress =  config.Appliance.ApplianceAddress;
    this.DevOpsInstanceId = config.Appliance.DevOpsInstanceId;
    this.DevOpsVersion = config.Appliance.Version;
    this.UserName = config.UserName;
    this.UserDisplayName = config.UserDisplayName;
    this.IdentityProviderName = config.IdentityProviderName;
    this.A2ARegistrationName = config.A2ARegistrationName;
    this.A2AVaultRegistrationName = config.A2AVaultRegistrationName;
    this.Thumbprint = config.Thumbprint;
  }

  initializeApplianceAddressAndLogin(): Observable<any> {
    let saveApplianceAddress = false;

    return this.serviceClient.getSafeguard().pipe(
      tap((safeguardData) => {
        this.ApplianceAddress =  safeguardData?.ApplianceAddress;
        this.DevOpsVersion = safeguardData?.Version;

        if (!this.ApplianceAddress) {
          this.ApplianceAddress = this.window.sessionStorage.getItem('ApplianceAddress');
          saveApplianceAddress = true;
        }

        this.window.sessionStorage.removeItem('ApplianceAddress');
      }),
      filter(() => this.ApplianceAddress && this.ApplianceAddress !== 'null'),
      switchMap(() => this.authService.getUserToken(this.ApplianceAddress)),
      switchMap(() => {
        if (saveApplianceAddress) {
          return this.serviceClient.putSafeguardAppliance(this.ApplianceAddress);
        } else {
          return of({});
        }
      }),
      switchMap(() => this.serviceClient.logon()),
      finalize(() => {
        if (!this.ApplianceAddress || this.ApplianceAddress === 'null') {
          this.router.navigate(['/login']);
        }
      })
    );
  }

  createCSR(certificateType: string): void {
    const dialogRef = this.dialog.open(CreateCsrComponent, {
      data:  { certificateType }
    });

    dialogRef.afterClosed().subscribe(
      result => {
        if (result?.result === CreateCsrResult.AddCertificate) {
          this.addCertificate(null, certificateType);
        }
      }
    );
  }

  addCertificate(e: Event, certificateType: string): void {
    let certificateFileName = '';
    this.error = null;
    e?.preventDefault();

    const dialogRef = this.dialog.open(UploadCertificateComponent, {
      data: { certificateType,
        certificate: certificateType === 'Web Server' ? this.webServerCert : null }
    });

    dialogRef.afterClosed().pipe(
      switchMap((dlgResult: any) => {
        if (dlgResult?.result === UploadCertificateResult.UploadCertificate) {
          return of(dlgResult.data);
        }

        if (dlgResult?.result === UploadCertificateResult.ViewCertificate) {
          this.viewCertificate(null, certificateType);
        } else if (dlgResult?.result === UploadCertificateResult.CreateCSR) {
          this.createCSR(certificateType);
        }
        return of(); // Nothing more to do
      }),
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

          var passphrase = resultArray.length > 1 ? resultArray[1] : '';
          this.certificateUploading[certificateType] = true;

          this.snackBar.open('Uploading certificate...');
          return certificateType === 'Client' ?
            this.serviceClient.postConfiguration(fileContents, passphrase) :
            this.serviceClient.postWebServerCertificate(fileContents, passphrase);
        }
      )
    ).subscribe(
      config => {
        this.snackBar.dismiss();
        if (certificateType === 'Client') {
          this.initializeConfig(config);
          // This is a hack: if you call too quickly after client cert upload, it returns zero
          setTimeout(() => {
            this.initializePlugins().subscribe();
          }, 2000);
          this.initializeMonitoring().subscribe();
          this.viewCertificate(null, 'Client');
        } else {
          this.webServerCertAdded = true;
          // Service restarts after updating web server cert; need to login again
          // Reload is needed to accept new web server cert
          this.window.sessionStorage.setItem('reload', 'true');
          this.authService.login(this.ApplianceAddress);
        }
      },
      error => {
        if (error.error?.Message?.includes('specified network password is not correct')) {
          // bad password, have another try?
          // it's all we get
          this.snackBar.open('The password for the certificate in ' + certificateFileName + ' was not correct.', 'Dismiss', {duration: this.snackBarDuration});
        } else if (error.error?.Message) {
          this.error = 'Unexpected error uploading ' + certificateType + ' certificate: ' + error.error.Message;
        }
      }).add(() => {
        this.certificateUploading['Client'] = this.certificateUploading['WebServer'] = false;
      });
  }

  viewMonitorEvents(): void {
    this.drawer.close();
    this.openDrawer = 'monitorevents';
    this.drawer.open();

    if (this.viewMonitorEventsRef)
      this.viewMonitorEventsRef.refresh();
    this.editPluginService.notifyEvent$.subscribe((data) => {
      if (data.mode === EditPluginMode.ViewMonitorEvents) {
        this.drawer.close();
      }
    });
  }

  editPlugin(plugin: any): void {
    this.error = null;
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
            this.updateMonitoringAvailable();

            if (data.saved === true && this.isMonitoring) {
              this.dialog.open(ConfirmDialogComponent, {
                data: {
                  title: 'Plugin Configuration Changed',
                  message: 'Restart the monitor to apply the new plugin configuration.',
                  showCancel: false,
                  confirmText: 'OK'
              }});
            }
          }
          break;
      }
    });
  }

  uploadPlugin(): void {
    this.error = null;
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
    },
      error => {
        this.error = error;
      });
  }

  updateMonitoringAvailable(): void {
    // Monitoring available when we have plugins and an account for at least one plugin
    this.isMonitoringAvailable = this.isMonitoring || (this.plugins.length > 1 && this.plugins.some(x => x.MappedAccountsCount > 0));
  }

  updateMonitoring(enabled: boolean): void {
    this.error = null;
    this.serviceClient.postMonitor(enabled).pipe(
      untilDestroyed(this)
    ).subscribe(() => {
      this.isMonitoring = enabled;
      this.updateMonitoringAvailable();
      setTimeout(() => {
        this.setArrows();
      }, 100);
    },
      error => {
        this.error = error;
      });
  }

  logout(): void {
    this.error = null;
    this.serviceClient.logout().pipe(
      untilDestroyed(this)
    ).subscribe(() => {
      this.router.navigate(['/login']);
    },
      error => {
        this.error = error;
      });
  }

  downloadLog(): void {
    this.error = null;
    this.serviceClient.getLogFile().pipe(
      tap(() => {
        this.snackBar.open('Downloading log file...');
      })
    ).subscribe((data) => {
      this.downloadFile(data);
      this.snackBar.open('Download complete.', 'Dismiss', {duration: this.snackBarDuration});
    },
      error => {
        this.snackBar.open('Download failed.', 'Dismiss', {duration: this.snackBarDuration});
        this.error = error;
      });
  }

  private downloadFile(data: HttpResponse<Blob>) {
    var contentDisposition = data.headers.get('Content-Disposition');
    var fileName = 'SafeguardSecretsBroker.log';
    if (contentDisposition != null) {
      var result = contentDisposition.split(';')[1].trim().split('=')[1];
      fileName = result.replace(/"/g, '');
    }

    const downloadedFile = new Blob([data.body], { type: data.body.type });
    var a = document.createElement("a");
    a.href = URL.createObjectURL(downloadedFile);
    a.download = fileName;
    a.click();
  }

  restart(): void {
    this.error = null;
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
      switchMap(() => this.initializeMonitoring()),
      tap(() => {
        this.setArrows();
        this.snackBar.open('Safeguard Secrets Broker for DevOps service restarted.', null, { duration: this.snackBarDuration });
        // Hide overlay
        this.drawer.close();
      })
    ).subscribe();
  }

  deleteConfig(): void {
    this.error = null;
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
      switchMap(() => this.serviceClient.deleteConfiguration())
    ).subscribe(() => {
      this.drawer.close();
      this.snackBar.dismiss();
      this.window.sessionStorage.setItem('ApplianceAddress', '');
      // Reload is needed to accept new web server cert
      this.window.sessionStorage.setItem('reload', 'true');
      this.router.navigate(['/login']);
    },
      error => {
        this.error = error;
      });
  }

  viewCertificate(e: Event, certType: string = 'Client'): void {
    this.error = null;
    const dialogRef = this.dialog.open(ViewCertificateComponent, {
      data: { certificateType: certType, certificate: certType === 'Web Server' ? this.webServerCert : null }
    });

    dialogRef.afterClosed().subscribe(
      (result) => {
        if (result?.result === ViewCertificateResult.RemovedCertificate) {
          if (certType === 'Client') {
            window.location.reload();
          } else {
            // Service restarts after removing web server cert; need to login again
            // Reload is needed to accept new web server cert
            this.window.sessionStorage.setItem('reload', 'true');
            this.authService.login(this.ApplianceAddress);
          }
        } else if (result?.result === ViewCertificateResult.AddCertificate) {
          this.addCertificate(null, certType);
        }
      }
    );
  }

  viewTrustedCertificates(e: Event): void {
    this.error = null;
    e.preventDefault();

    const dialogRef = this.dialog.open(EditTrustedCertificatesComponent, {
      width: '500px',
      data: { trustedCertificates: this.trustedCertificates }
    });

    dialogRef.afterClosed().subscribe();
  }
}
