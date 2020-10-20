import { Component, OnInit, ViewChild, ElementRef, Renderer2 } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { switchMap, map, concatAll, tap, distinctUntilChanged, debounceTime, finalize } from 'rxjs/operators';
import { of, Observable, fromEvent } from 'rxjs';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { UploadCertificateComponent } from '../upload-certificate/upload-certificate.component';
import { EnterPassphraseComponent } from '../upload-certificate/enter-passphrase/enter-passphrase.component';
import * as $ from 'jquery';
import { ViewportScroller } from '@angular/common';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';
import { EditPluginService } from '../edit-plugin.service';
import { MatDrawer } from '@angular/material/sidenav';
import { AuthService } from '../auth.service';
import { MatSnackBar } from '@angular/material/snack-bar';

@UntilDestroy()
@Component({
  selector: 'app-main',
  templateUrl: './main.component.html',
  styleUrls: ['./main.component.scss']
})
export class MainComponent implements OnInit {

  constructor(
    private window: Window,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog,
    private router: Router,
    private renderer: Renderer2,
    private elementRef: ElementRef,
    private scroller: ViewportScroller,
    public editPluginService: EditPluginService,
    private authService: AuthService,
    private snackBar: MatSnackBar
  ) { }

  UserName: string;
  IdentityProviderName: string;
  A2ARegistrationName: string;
  A2AVaultRegistrationName: string;
  Thumbprint: string;
  DevOpsInstanceId: string;
  ApplianceAddress: string;

  plugins = [];
  isLoading: boolean;

  @ViewChild('drawer', { static: false }) drawer: MatDrawer;

  @ViewChild('fileSelectInputDialog', { static: false }) fileSelectInputDialog: ElementRef;

  @ViewChild('unconfigured', { static: false }) set contentUnconfigured(content: ElementRef) {
    if (content && !this.isLoading) {
      this.setArrows();
    }
  }

  ngOnInit(): void {
    this.isLoading = true;
    this.ApplianceAddress =  this.window.sessionStorage.getItem('ApplianceAddress');

    if (!this.ApplianceAddress) {
      this.router.navigate(['/login']);
    } else {
      this.loginToDevOpsService()
        .pipe(
          untilDestroyed(this),
          switchMap(() => this.serviceClient.getConfiguration()),
          tap((config) => this.initializeConfig(config)),
          switchMap((config) => {
            if (config.Thumbprint) {
              return this.initializePlugins();
            } else {
              return of({});
            }
          })
        ).subscribe(() => {
          this.isLoading = false;
        });
    }

    fromEvent(window, 'scroll').pipe(
      untilDestroyed(this),
      debounceTime(50),
      distinctUntilChanged()
    ).subscribe(() => {
      console.log('scroll');
    });
  }

  private calculateArrow(A: HTMLElement, B: HTMLElement, index: number, totalArrows: number): string {
    const posA = {
      x: A.offsetLeft + 150 - index * 15,
      y: A.offsetTop + A.offsetHeight - 20 + this.window.scrollY
    };

    const posB = {
      x: B.offsetLeft - 50,
      y: B.offsetTop + B.offsetHeight / 2 - 20
    };

    return `M ${posA.x},${posA.y} V ${posB.y} a 3,3 0 0 0 3 3 H ${posB.x}`;
  }

  private setArrows(): void {
    const colors = [ 'CorbinOrange', 'MauiSunset', 'TikiSunrise', 'AzaleaPink' ];

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
        const color =  isUnconfigured ? 'Black9' :  colors[index % colors.length];

        pathEl.setAttribute('class', isUnconfigured ? 'arrow-unconfigured' : 'arrow');
        pathEl.setAttribute('marker-end', `url(#marker${color})`);

        this.renderer.appendChild(pathGroup, pathEl);
      });
    } catch {}
  }

  initializeConfig(config: any): void {
    this.ApplianceAddress =  config.Appliance.ApplianceAddress;
    this.DevOpsInstanceId = config.Appliance.DevOpsInstanceId;
    this.UserName = config.UserName;
    this.IdentityProviderName = config.IdentityProviderName;
    this.A2ARegistrationName = config.A2ARegistrationName;
    this.A2AVaultRegistrationName = config.A2AVaultRegistrationName;
    this.Thumbprint = config.Thumbprint;
  }

  initializePlugins(): Observable<any> {
    const knownPlugins = [
      ['AzureKeyVault', 'Azure Key Vault'],
      ['SmsTextEmail', 'SMS'],
      ['HashiCorpVault', 'Hashi Corp Vault'],
      ['JenkinsSecrets', 'Jenkins Secrets'],
      ['KubernetesSecrets', 'Kubernetes Secrets']
    ];

    const custom = {
      DisplayName: 'Upload Custom Plugin',
      IsUploadCustom: true,
      Accounts: []
    };
    this.plugins.push(custom);

    return this.serviceClient.getPlugins().pipe(
      // Flatten array so each plugin is emitted individually
      concatAll(),
      // Get the plugin accounts
      switchMap((plugin: any) => {
        console.log(plugin);
        return this.serviceClient.getPluginAccounts(plugin.Name).pipe(
          map(accounts => {
            plugin.Accounts = accounts;
            return plugin;
          })
        );
      }),
      tap((plugin: any) => {
        const knownPlugin = knownPlugins.find(p => p[0] === plugin.Name);
        if (knownPlugin) {
          plugin.DisplayName = knownPlugin[1];
        } else {
          plugin.DisplayName = plugin.Name;
        }

        // Consider a plugin configured if any accounts are set or any configuration properties are set
        if (plugin.Accounts.length > 0) {
          plugin.IsConfigured = true;
        } else {
          Object.keys(plugin.Configuration).forEach(key => {
            if (plugin.Configuration[key]) {
              plugin.IsConfigured = true;
            }
          });
        }
        this.plugins.push(plugin);
      })
    );
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
            return this.serviceClient.putSafeguard(this.ApplianceAddress);
          }
          return of(undefined);
        }),
        switchMap(() => this.serviceClient.logon())
      );
  }

  addClientCertificate(e: Event): void {
    e.preventDefault();

    const dialogRef = this.dialog.open(UploadCertificateComponent, {
      // disableClose: true
    });

    dialogRef.afterClosed().pipe(
      switchMap(
        (fileData) => {
          if (fileData?.fileType !== 'application/x-pkcs12') {
            return of([fileData]);
          }

          const ref = this.dialog.open(EnterPassphraseComponent, {
            data: { fileName: fileData.fileName }
          });

          return ref.afterClosed().pipe(
            // Emit fileData as well as passphrase
            map(passphraseData => [fileData, passphraseData])
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
          return this.serviceClient.postConfiguration(fileContents, passphrase);
        }
      )
    ).subscribe(config => {
      this.initializeConfig(config);
    });
  }

  editConfiguration(plugin: any): void {
    this.editPluginService.setEdit(plugin);
    this.drawer.open();
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

    this.serviceClient.postPluginFile(fileSelected).pipe(
      finalize(() => {
        // Clear the selection
        const input = this.fileSelectInputDialog.nativeElement as HTMLInputElement;
        input.value = null;
      })
    ).subscribe(
      (x: any) => {
        if (typeof x === 'string') {
          this.snackBar.open(x, 'OK', { duration: 10000 });
        } else {
          x.IsConfigured = false;
          x.Accounts = [];
          x.DisplayName = x.Name;
          this.plugins.push(x);
          console.log('plugin added');
        }
      });
  }
}

