import { Component, OnInit, ViewChild, ElementRef, Renderer2 } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { switchMap, map, concatAll, tap, distinctUntilChanged, debounceTime } from 'rxjs/operators';
import { of, Observable, fromEvent } from 'rxjs';
import { StaticInfoService } from '../static-info.service';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { UploadCertificateComponent } from '../upload-certificate/upload-certificate.component';
import { EnterPassphraseComponent } from '../upload-certificate/enter-passphrase/enter-passphrase.component';
import { VaultPlugin } from '../plugin.type';
import * as $ from 'jquery';
import { ViewportScroller } from '@angular/common';
import { UntilDestroy, untilDestroyed } from '@ngneat/until-destroy';

@UntilDestroy()
@Component({
  selector: 'app-main',
  templateUrl: './main.component.html',
  styleUrls: ['./main.component.scss']
})
export class MainComponent implements OnInit {

  constructor(
    public staticInfoService: StaticInfoService,
    private window: Window,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog,
    private router: Router,
    private renderer: Renderer2,
    private elementRef: ElementRef,
    private scroller: ViewportScroller
  ) { }

  UserName: string;
  IdentityProviderName: string;
  A2ARegistrationName: string;
  A2AVaultRegistrationName: string;
  Thumbprint: string;
  DevOpsInstanceId: string;

  plugins = [];
  isLoading: boolean;

  @ViewChild('unconfigured', { static: false }) set contentUnconfigured(content: ElementRef) {
    if (content && !this.isLoading) {
      console.log('after change');
      this.setArrows();
    }
  }

  ngOnInit(): void {
    this.isLoading = true;
    this.staticInfoService.ApplianceAddress =  this.window.sessionStorage.getItem('ApplianceAddress');

    if (!this.staticInfoService.ApplianceAddress) {
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
      //this.setArrows();
    });
  }

  private calculateArrow(A: HTMLElement, B: HTMLElement, index: number, totalArrows: number): string {
    const rectA = A.getBoundingClientRect();
    const rectB = B.getBoundingClientRect();

    console.log(rectA.x + ' ' + rectA.y + ' ' + rectA.height);
    console.log(rectB);
    console.log(window.scrollY);

    const posA = {
      x: A.offsetLeft + 60 - index * 15,
      // x: rectA.left + 60 - index * 15,
      y: A.offsetTop + A.offsetHeight - 20 + this.window.scrollY
      // y: rectA.y + rectA.top + this.window.scrollY + 15
    };

    const posB = {
      x: B.offsetLeft - 50,
      y: B.offsetTop + B.offsetHeight / 2 - 20
      // y: rectB.y / 2 + this.window.scrollY
    };

    return `M ${posA.x},${posA.y} V ${posB.y} a 3,3 0 0 0 3 3 H ${posB.x}`;
  }

  private setArrows(): void {
    const colors = [{ name: 'CorbinOrange', value: '#F4770B' },
      { name: 'MauiSunset', value: '#802981' },
      { name: 'TikiSunrise', value: '#F0DF3F' },
      { name: 'AzaleaPink', value: '#F10C8A' }];

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
        const color =  isUnconfigured ? { name: 'Black9', value: '#999' } :  colors[index % colors.length];

        pathEl.setAttribute('class', isUnconfigured ? 'arrow-unconfigured' : 'arrow');
        pathEl.setAttribute('marker-end', `url(#marker${color.name}`);

        this.renderer.appendChild(pathGroup, pathEl);
      });
    } catch {}
  }

  initializeConfig(config: any): void {
    this.staticInfoService.ApplianceAddress =  config.Appliance.ApplianceAddress;

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
      switchMap((plugin) => {
        return this.serviceClient.getPluginAccounts(plugin.Name).pipe(
          map(accounts => {
            plugin.Accounts = accounts;
            return plugin;
          })
        );
      }),
      tap((plugin) => {
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

  getUserToken(): Observable<any> {
    const userToken = this.window.sessionStorage.getItem('UserToken');

    if (!userToken) {
      const accessToken = this.window.sessionStorage.getItem('AccessToken');
      const applianceAddress = this.staticInfoService.ApplianceAddress;

      return this.serviceClient.getUserToken(applianceAddress, accessToken);
    } else {
      return of({ Status: 'Success', UserToken: userToken });
    }
  }

  loginToDevOpsService(): Observable<any> {
    return this.getUserToken()
      .pipe(
        switchMap((userTokenData) => {
          if (userTokenData?.Status === 'Success') {
            this.window.sessionStorage.setItem('UserToken', userTokenData.UserToken);
            this.window.sessionStorage.removeItem('AccessToken');

            if (!this.staticInfoService.InstanceId) {
              return this.serviceClient.putSafeguard(this.staticInfoService.ApplianceAddress);
            }
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
}

