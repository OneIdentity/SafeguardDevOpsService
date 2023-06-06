import { Component, OnInit, ViewChild, ViewChildren, ElementRef, Renderer2, HostListener, AfterViewInit, QueryList } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { switchMap, map, tap, finalize, filter, catchError } from 'rxjs/operators';
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
import { EditAddonService, EditAddonMode } from '../edit-addon.service';
import { MatDrawer } from '@angular/material/sidenav';
import { AuthService } from '../auth.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { EditTrustedCertificatesComponent } from '../edit-trusted-certificates/edit-trusted-certificates.component';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { HttpResponse } from '@angular/common/http';
import { ViewMonitorEventsComponent } from '../view-monitor-events/view-monitor-events.component';
import { cloneDeep } from 'lodash';

@UntilDestroy()
@Component({
  selector: 'app-main',
  templateUrl: './main.component.html',
  styleUrls: ['./main.component.scss']
})
export class MainComponent implements OnInit, AfterViewInit {
  @ViewChild('drawer', { static: false }) drawer: MatDrawer;
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

  LoggedInUserName: string;
  LoggedInUserDisplayName: string;
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
  addons = [];
  isLoading: boolean = false;
  isUploading = { Plugin: false, Addon: false, Certificate: false, Registration: false, Backup: false };
  isRestarting: boolean = false;
  openDrawer: string;
  openWhat: string;
  webServerCertAdded: boolean = false;
  webServerCert: any;
  config: any;
  trustedCertificates = [];
  isMonitoring: boolean;
  isMonitoringAvailable: boolean;
  unconfiguredDiv: any;
  footerAbsolute: boolean = true;
  restartingProgress: string = 'Restarting Service';
  logFileName: string = 'SafeguardSecretsBroker.log';
  backupFileName: string = 'SafeguardDevOpsService.sbbf';
  hasAvailableRegistrations: boolean = false;
  showAvailableRegistrations: boolean = false;
  needsClientCertificate: boolean = true;
  needsWebCertificate: boolean = true;
  needsTrustedCertificates: boolean = true;
  needsSSLEnabled: boolean = true;
  isLicensed: boolean = false;
  isAssetAdmin: boolean = false;
  certificateUploaded: boolean = false;
  passedTrustChainValidation: boolean = false;
  passphrase: string;
  pluginInstances = [];

  certificateUploading = {
    Client: false,
    WebServer: false,
    trusted: false
  };

  private snackBarDuration: number = 5000;
  private maxPostAddonRefreshCount: number = 15;
  private viewMonitorEventsRef: ViewMonitorEventsComponent;

  constructor(
    private window: Window,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog,
    private router: Router,
    private renderer: Renderer2,
    public editPluginService: EditPluginService,
    public editAddonService: EditAddonService,
    private authService: AuthService,
    private snackBar: MatSnackBar
  ) { }

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
          this.initializePlugins(),
          this.initializeAddons()
        ])),
        finalize(() => this.isLoading = false)
      ).subscribe(() => { },
        error => this.error = error
      );
  }

  ngAfterViewInit(): void {
    this.viewMonitorEventsRefs.changes.subscribe((comps: QueryList<ViewMonitorEventsComponent>) => {
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

    this.pluginInstances = [];
    return this.serviceClient.getPlugins().pipe(
      tap((plugins: any[]) => {
        plugins.forEach(plugin => {
          plugin.IsConfigurationSetup = true;
          this.plugins.push(plugin);

          if (!this.pluginInstances[plugin.RootPluginName]) {
            this.pluginInstances[plugin.RootPluginName] = { Count: 0, AllMappedAccountsCount: 0, Disabled: 0 };
          } else {
            plugin.Rendered = true;
          }
          this.pluginInstances[plugin.RootPluginName].Count++;
          this.pluginInstances[plugin.RootPluginName].AllMappedAccountsCount += plugin.MappedAccountsCount;

          if (plugin.IsDisabled) {
            this.pluginInstances[plugin.RootPluginName].Disabled++;
          }
        });

        this.updateMonitoringAvailable();
      }));
  }

  private initializeAddons(): Observable<any> {
    this.error = null;
    if (!this.Thumbprint) {
      return of({});
    }

    this.addons.splice(0);
    const custom = {
      Manifest: {
        DisplayName: 'Upload'
      },
      IsUploadCustom: true
    };
    this.addons.push(custom);

    return this.serviceClient.getAddons().pipe(
      tap((addons: any[]) => {
        addons.forEach(addon => {
          addon.IsConfigurationSetup = true;
          addon.IsAssetAdmin = this.isAssetAdmin;
          this.serviceClient.getAddonStatus(addon.Name).subscribe(result => addon.Status = result);
          this.addons.push(addon);
        });
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

  private calculateArrow(A: HTMLElement, B: HTMLElement, index: number, totalArrows: number, isDisabled: boolean): string {
    const isUnconfigured = index === totalArrows - 1;

    const posA = {
      x: A.offsetLeft + 150 - index * 15,
      y: A.offsetTop + A.offsetHeight - 15 + this.window.scrollY
    };

    const markerOffset = this.isMonitoring && !isUnconfigured && !isDisabled ? 22 : 50;
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
    const colors = ['CorbinOrange', 'MauiSunset', 'AspenGreen', 'AzaleaPink'];

    try {
      const configured = $('.configured');
      const unconfigured = $('.unconfigured')[0];
      const startEl = $('.info-container')[0];
      const pathGroup = $('#svgGroup')[0];

      $('#svgGroup path').remove();

      const total = configured.length + 1;
      const all = configured.toArray();
      all.push(unconfigured);

      all.forEach((item, index) => {
        const isDisabled = item.classList.contains('disabled:true');

        const dStr = this.calculateArrow(startEl, item, index, total, isDisabled);

        const pathEl = this.renderer.createElement('path', 'svg');
        pathEl.setAttribute('d', dStr);

        const isUnconfigured = index === total - 1;
        const color = isUnconfigured || isDisabled || !this.isMonitoring ? 'Black9' : colors[index % colors.length];

        pathEl.setAttribute('class', isUnconfigured || isDisabled || !this.isMonitoring ? 'arrow-unconfigured' : 'arrow');
        pathEl.setAttribute('marker-end', `url(${this.window.location.href}#marker${color})`);

        this.renderer.appendChild(pathGroup, pathEl);
      });
    } catch { }
  }

  initializeConfig(config: any): void {
    this.isLicensed = config.IsLicensed;
    this.isAssetAdmin = config.Appliance.AdminRoles.some(r => r == "AssetAdmin");
    this.LoggedInUserName = config.Appliance.UserName;
    this.LoggedInUserDisplayName = config.Appliance.UserDisplayName;
    this.ApplianceAddress = config.Appliance.ApplianceAddress;
    this.DevOpsInstanceId = config.Appliance.DevOpsInstanceId;
    this.DevOpsVersion = config.Appliance.Version;
    this.UserName = config.A2AUser?.UserName;
    this.UserDisplayName = config.A2AUser?.DisplayName;
    this.IdentityProviderName = config.A2AUser?.IdentityProviderName;
    this.A2ARegistrationName = config.A2ARegistration?.AppName;
    this.A2AVaultRegistrationName = config.A2AVaultRegistration?.AppName;
    this.Thumbprint = config.A2ARegistration?.CertificateUserThumbPrint;
  }

  initializeApplianceAddressAndLogin(): Observable<any> {
    let saveApplianceAddress = false;

    return this.serviceClient.getSafeguard().pipe(
      tap((safeguardData) => {
        this.ApplianceAddress = safeguardData?.ApplianceAddress;
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
      tap((logon: any) => {
        this.hasAvailableRegistrations = logon.HasAvailableA2ARegistrations;
        this.needsClientCertificate = logon.NeedsClientCertificate;
        this.needsWebCertificate = logon.NeedsWebCertificate;
        this.needsTrustedCertificates = logon.NeedsTrustedCertificates;
        this.needsSSLEnabled = logon.NeedsSSLEnabled;
        this.passedTrustChainValidation = logon.PassedTrustChainValidation;
      }),
      switchMap(() => this.checkA2ARegistration()),
      tap((nullRegistration: any) => {
        this.showAvailableRegistrations = typeof nullRegistration === 'boolean' && nullRegistration && this.hasAvailableRegistrations;
      }),
      finalize(() => {
        if (!this.ApplianceAddress || this.ApplianceAddress === 'null') {
          this.router.navigate(['/login']);
        }
      })
    );
  }

  createRegistration(registrationId: number) {
    if (registrationId > 0) {
      this.isUploading.Registration = true;

      this.serviceClient.logon()
        .subscribe(() => {
          this.serviceClient.putA2ARegistration(registrationId)
            .subscribe(() => {
              this.isUploading.Registration = false;
              this.showAvailableRegistrations = false;
              this.window.location.reload();
            },
              error => {
                this.isUploading.Registration = false;
                this.error = error;
              }
            );
        },
          error => {
            this.isUploading.Registration = false;
            this.error = error;
          }
        );
    } else {
      this.showAvailableRegistrations = false;
    }
  }

  // If we already have an A2A registration then just return nothing, else
  // if 404 then get available A2A registrations to choose from.
  checkA2ARegistration(): Observable<any> {
    return this.serviceClient.getA2ARegistration()
      .pipe(
        map(() => {
          return of(false);
        }),
        catchError((error) => {
          return of(error.status === 404);
        })
      );
  }

  checkAvailableA2ARegistrations(): Observable<any> {
    return this.serviceClient.getAvailableA2ARegistrations()
      .pipe(
        map((registrations) => {
          return registrations;
        }),
        catchError(() => {
          return of();
        })
      );
  }

  createCSR(certificateType: string): void {
    const dialogRef = this.dialog.open(CreateCsrComponent, {
      data: { certificateType }
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
      data: {
        certificateType,
        certificate: certificateType === 'Web Server' ? this.webServerCert : null,
        needsWebCertificate: this.needsWebCertificate
      }
    });

    dialogRef.afterClosed().pipe(
      switchMap((dlgResult: any) => {
        if (dlgResult?.result === UploadCertificateResult.UploadCertificate) {
          this.certificateUploaded = true;
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
          this.isUploading.Certificate = true;

          return certificateType === 'Client' ?
            this.serviceClient.postConfiguration(fileContents, passphrase) :
            this.serviceClient.postWebServerCertificate(fileContents, passphrase);
        }
      )
    ).subscribe(
      config => {
        this.isUploading.Certificate = false;
        if (certificateType === 'Client') {
          this.initializeConfig(config);
          // This is a hack: if you call too quickly after client cert upload, it returns zero
          setTimeout(() => {
            forkJoin([
              this.initializeMonitoring(),
              this.initializePlugins(),
              this.initializeAddons()
            ]).subscribe();
          }, 2000);
          this.viewCertificate(null, 'Client', true, this.certificateUploaded);
        } else {
          this.webServerCertAdded = true;
          this.window.location.reload();
        }
      },
      error => {
        this.isUploading.Certificate = false;
        if (error.error?.Message?.includes('specified network password is not correct')) {
          // bad password, have another try?
          // it's all we get
          this.snackBar.open('The password for the certificate in ' + certificateFileName + ' was not correct.', 'Dismiss', { duration: this.snackBarDuration });
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

    if (this.viewMonitorEventsRef) {
      this.viewMonitorEventsRef.refresh();
    }
    this.editPluginService.notifyEvent$.subscribe((data) => {
      if (data.mode === EditPluginMode.ViewMonitorEvents) {
        this.drawer.close();
      }
    });
  }

  editAddon(addon: any): void {
    if (this.isUploading.Plugin || this.isUploading.Addon || this.isRestarting) {
      return;
    }

    this.error = null;
    this.editAddonService.openProperties(addon);
    this.openWhat = 'addon';
    this.openDrawer = 'properties';
    this.drawer.open();

    this.editAddonService.notifyEvent$.subscribe((data) => {
      switch (data.mode) {
        case EditAddonMode.Properties:
          this.drawer.close();
          this.openDrawer = 'properties';
          this.drawer.open();
          break;

        case EditAddonMode.None:
          this.drawer.close();
          this.openDrawer = '';
          const indx = this.addons.findIndex(x => x.Name === addon.Name);
          if (indx > -1) {
            if (data.addon) {
              this.addons[indx] = data.addon;
            } else {
              this.addons.splice(indx, 1);
            }
          }
          break;
      }
    });
  }

  editPlugin(plugin: any): void {
    if (this.isUploading.Plugin || this.isUploading.Addon || this.isRestarting || !plugin.IsLoaded) {
      return;
    }

    this.error = null;
    let pluginInstances = cloneDeep(this.plugins.filter(p => p.RootPluginName == plugin.RootPluginName));
    this.editPluginService.openProperties(pluginInstances);
    this.openWhat = 'plugin';
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

          if (data.restartMonitoring === true) {
            this.updateMonitoringAvailable();

            if (this.isMonitoring) {
              const dialogRef = this.dialog.open(ConfirmDialogComponent, {
                data: {
                  title: 'Plugin Configuration Changed',
                  message: 'Restart the monitor to apply the new plugin configuration.',
                  showCancel: false,
                  confirmText: 'OK'
                }
              });

              dialogRef.afterClosed().pipe(
                filter((dlgResult) => dlgResult?.result === 'OK'),
              ).subscribe(() => {
                if (data.reload === true) {
                  this.window.location.reload();
                }
              });
            } else if (data.reload === true) {
              this.window.location.reload();
            }
          } else if (data.reload === true) {
            this.window.location.reload();
          }
          break;
      }
    });
  }

  uploadPlugin(): void {
    this.error = null;
    var fileInput = $('<input type="file" accept=".zip" />');

    fileInput.on('change', () => {
      var file = fileInput.prop('files')[0];

      if (!file) {
        return;
      }

      this.isUploading.Plugin = true;

      this.serviceClient.postPluginFile(file)
        .subscribe((x: any) => {
          if (typeof x === 'string') {
            this.isUploading.Plugin = false;
            this.snackBar.open(x, 'OK', { duration: 10000 });
          } else {
            setTimeout(() => {
              this.isUploading.Plugin = false;
              this.initializePlugins().subscribe();
            }, 3000);
          }
        },
          error => {
            this.isUploading.Plugin = false;
            this.error = this.parseError(error);
          });
    });

    fileInput.trigger('click');
  }

  uploadAddon() {
    this.error = null;
    var fileInput = $('<input type="file" accept=".sbao" />');

    fileInput.on('change', () => {
      var file = fileInput.prop('files')[0];

      if (!file) {
        return;
      }

      this.isUploading.Addon = true;

      this.serviceClient.postAddonFile(file)
        .subscribe((x: any) => {
          if (typeof x === 'string') {
            this.isUploading.Addon = false;
            this.snackBar.open(x, 'OK', { duration: 10000 });
          } else {
            this.isUploading.Addon = false;
            this.isRestarting = true;
            setTimeout(() => {
              this.postAddonRefresh(0);
            }, 3000);
          }
        },
          error => {
            this.isUploading.Addon = false;
            this.error = this.parseError(error);
          });
    });

    fileInput.trigger('click');
  }

  // The service restarts in 2-3 seconds but it's 10+ seconds before Logon is not 504
  private postAddonRefresh(postAddonRefreshTries: number) {
    this.serviceClient.logon()
      .subscribe(() => this.window.location.reload(),
        error => {
          if (error.status == 504) {
            this.restartingProgress += '.';
            postAddonRefreshTries += 1;
            if (postAddonRefreshTries < this.maxPostAddonRefreshCount) {
              setTimeout(() => {
                this.postAddonRefresh(postAddonRefreshTries);
              }, 1000);
            } else {
              this.window.location.reload();
            }
          } else {
            this.isRestarting = false;
            this.error = error;
          }
        }
      );
  }

  updateMonitoringAvailable(): void {
    // Monitoring available when we have plugins and an account for at least one plugin
    this.isMonitoringAvailable = this.isMonitoring || (this.plugins.length > 1 && this.plugins.some(x => x.MappedAccountsCount > 0) && !this.needsClientCertificate);
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
      this.window.sessionStorage.removeItem('UserToken');
      this.router.navigate(['/login']);
    },
      error => this.error = error
    );
  }

  downloadLog(): void {
    this.error = null;
    this.serviceClient.getLogFile().pipe(
      tap(() => {
        this.snackBar.open('Downloading log file...');
      })
    ).subscribe((data) => {
      this.downloadFile(data, this.logFileName);
      this.snackBar.open('Download complete.', 'Dismiss', { duration: this.snackBarDuration });
    },
      error => {
        this.snackBar.open('Download failed.', 'Dismiss', { duration: this.snackBarDuration });
        this.error = error;
      });
  }

  private downloadFile(data: HttpResponse<Blob>, fileName: string) {
    var contentDisposition = data.headers.get('Content-Disposition');
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

  backupConfiguration(): void {
    this.error = null;
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        showPassphrase: true,
        title: 'Backup Configuration',
        message: '<p>Please provide a passphrase to encrypt the database key stored in the backup data. If no passphrase is provided, the backup will be generated with no encryption.</p?>' +
          '<p>Once the backup file has been generated, the file will be downloaded automatically.</p>'
      }
    });

    dialogRef.afterClosed().pipe(
      filter((dlgResult) => dlgResult?.result === 'OK'),
      tap((dlgResult) => {
        this.passphrase = dlgResult?.passphrase;
        this.snackBar.open('Generating backup file...');
        // Show overlay to disable clicking on anything
        this.drawer.open();
      }),
      switchMap((dlgResult) => this.serviceClient.getBackup(dlgResult?.passphrase))
    ).subscribe((data) => {
      this.downloadFile(data, this.backupFileName);
      this.drawer.close();
      this.snackBar.dismiss();
    },
      error => {
        this.error = error;
      });
  }

  restoreConfiguration(): void {
    this.error = null;
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        showPassphrase: true,
        title: 'Restore Configuration',
        message: '<p>Please provide a passphrase to decrypt the database key stored in the backup data. If no passphrase is provided, the restore process will assume that the key was stored unencrypted.</p?>' +
          '<p>Once the restore is finished, Secrets Broker will automatically restart the service.</p>'
      }
    });

    dialogRef.afterClosed().pipe(
      filter((dlgResult) => dlgResult?.result === 'OK'),
      tap((dlgResult) => {
        this.uploadAndRestore(dlgResult?.passphrase);
        this.snackBar.open('Restoring configuration...');
        // Show overlay to disable clicking on anything
        this.drawer.open();
      }),
    ).subscribe(() => {
      this.drawer.close();
      this.snackBar.dismiss();
      //this.window.sessionStorage.setItem('ApplianceAddress', '');
    },
      error => {
        this.error = error;
      });
  }

  private uploadAndRestore(passphrase: string) {
    var fileInput = $('<input type="file" accept=".sbbf" />');

    fileInput.on('change', () => {
      var file = fileInput.prop('files')[0];

      if (!file) {
        return;
      }

      this.isUploading.Backup = true;

      this.serviceClient.postRestore(file, passphrase)
        .subscribe((x: any) => {
          if (typeof x === 'string') {
            this.snackBar.open(x, 'OK', { duration: 10000 });
          } else {
            setTimeout(() => {
              this.isUploading.Backup = false;
              this.window.location.reload();
            }, 3000);
          }
        },
          error => {
            this.isUploading.Backup = false;
            this.error = this.parseError(error);
          });
    });

    fileInput.trigger('click');
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
        this.window.location.reload();
      })
    ).subscribe();
  }

  deleteConfig(): void {
    this.error = null;
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        showSecretsBrokerOnly: true,
        showRestart: true,
        title: 'Delete Configuration',
        message: '<p>Are you sure you want to remove all configuration for Safeguard Secrets Broker for DevOps?</p?>' +
          '<p>This removes all A2A credential retrievals, the A2A registration and the A2A user from Safeguard for Privileged Passwords.</p>' +
          '<p>It will also remove Safeguard Secrets Broker for DevOps configuration database and restart the DevOps service</p>'
      }
    });

    dialogRef.afterClosed().pipe(
      filter((dlgResult) => dlgResult?.result === 'OK'),
      tap((dlgResult) => {
        this.isRestarting = dlgResult?.restart;
        this.snackBar.open('Deleting configuration...');
        // Show overlay to disable clicking on anything
        this.drawer.open();
      }),
      switchMap((dlgResult) => this.serviceClient.deleteConfiguration(dlgResult?.secretsBrokerOnly, dlgResult?.restart))
    ).subscribe(() => {
      this.drawer.close();
      this.snackBar.dismiss();
      this.window.sessionStorage.setItem('ApplianceAddress', '');
      if (this.isRestarting) {
        this.window.location.reload();
      }
    },
      error => {
        this.isRestarting = false;
        this.error = error;
      });
  }

  viewCertificate(e: Event, certType: string = 'Client', reload: boolean = false, isUpload: boolean = false): void {
    this.error = null;
    const dialogRef = this.dialog.open(ViewCertificateComponent, {
      data: {
        certificateType: certType,
        certificate: certType === 'Web Server' ? this.webServerCert : null,
        isUpload: isUpload
      }
    });

    dialogRef.afterClosed().subscribe(
      (result) => {
        if (result?.result === ViewCertificateResult.RemovedCertificate) {
          this.window.location.reload();
        } else if (result?.result === ViewCertificateResult.AddCertificate) {
          this.addCertificate(null, certType);
        } else if (reload) {
          this.window.location.reload();
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

    dialogRef.afterClosed().subscribe(() => {
      this.window.location.reload();
    });
  }

  parseError(error: any) {
    var message = "";

    if (error.error) {
      try {
        let e = JSON.parse(error.error);

        if (e.message || e.Message) {
          message = e.message || e.Message;
        }
      } catch {
        if (error.message || error.Message) {
          message = error.message || error.Message;
        }
        else {
          message = error + '';
        }
      }
    }
    else if (error.message || error.Message) {
      message = error.message || error.Message;
    }
    else {
      message = error + '';
    }

    return message;
  }
}
