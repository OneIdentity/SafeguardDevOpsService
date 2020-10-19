import { HttpClient } from '@angular/common/http';
import { ServiceClientHelper as SCH } from './service-client-helper';
import { catchError } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })

export class DevOpsServiceClient {

  BASE = '/service/devops/v1/';

  constructor(
    private http: HttpClient,
    private window: Window) {
  }

  private authHeader(additionalHeaders?: any): any {
    const header = { Authorization: 'spp-token ' + this.window.sessionStorage.getItem('UserToken') };

    if (!additionalHeaders) {
      return { headers: header };
    } else {
      const arr = [header];
      arr.push(additionalHeaders);
      return { headers: arr };
    }
  }

  getUserToken(applianceAddress: string, accessToken: string): Observable<any> {
    const url = 'https://' + applianceAddress + '/service/core/v3/Token/LoginResponse';

    return this.http.post(url, { StsAccessToken: accessToken })
      .pipe(catchError(SCH.error('DevOpsServiceClient', 'getSafeguard')));
  }

  getSafeguard(): Observable<any> {
    const url = this.BASE + 'Safeguard';

    return this.http.get(url)
      .pipe(catchError(SCH.error<any>('DevOpsServiceClient', 'getSafeguard')));
  }

  putSafeguard(applianceAddress: string): Observable<any> {
    const url = this.BASE + 'Safeguard';
    const payload = {
      ApplianceAddress: applianceAddress,
      ApiVersion: 3,
      IgnoreSsl: true
    };
    return this.http.put(url, payload, this.authHeader())
      .pipe(catchError(SCH.error<any>('DevOpsServiceClient', 'putSafeguard')));
  }

  logon(): Observable<any> {
    return this.http.get(this.BASE + 'Safeguard/Logon', this.authHeader())
      .pipe(catchError(SCH.error<any>('DevOpsServiceClient', 'logon')));
  }

  getCSR(certType: string, subjectName?: string, dnsSubjectAlternativeNames?: string, ipSubjectAlternativeNames?: string): Observable<any> {
    let url = this.BASE + 'Safeguard/CSR?certType=' + certType;

    if (subjectName) {
      url +=  '&subjectName=' + encodeURIComponent(subjectName);
    }
    if (dnsSubjectAlternativeNames) {
      url +=  '&sanDns=' + encodeURIComponent(dnsSubjectAlternativeNames);
    }
    if (ipSubjectAlternativeNames) {
      url +=  '&sanIp=' + encodeURIComponent(ipSubjectAlternativeNames);
    }

    const options = Object.assign({ responseType: 'text' }, this.authHeader());
    console.log(options);
    return this.http.get(url, options)
      .pipe(catchError(SCH.error<any>('DevOpsServiceClient', 'getCSR')));
  }

  getConfiguration(): Observable<any> {
    return this.http.get(this.BASE + 'Safeguard/Configuration', this.authHeader())
      .pipe(catchError(SCH.error<any>('DevOpsServiceClient', 'getConfiguration')));
  }

  postConfiguration(base64CertificateData?: string, passphrase?: string): Observable<any> {
    const url = this.BASE + 'Safeguard/Configuration';
    const payload = {
      Base64CertificateData: base64CertificateData,
      Passphrase: passphrase
    };
    return this.http.post(url, payload, this.authHeader())
      .pipe(catchError(SCH.error('DevOpsServiceClient', 'postConfiguration')));
  }

  getPlugins(): Observable<any[]> {
    return this.http.get(this.BASE + 'Plugins', this.authHeader())
      .pipe(catchError(SCH.error<any>('DevOpsServiceClient', 'getPlugins')));
  }

  getPluginAccounts(name: string): Observable<any[]> {
    return this.http.get(this.BASE + 'Plugins/' + encodeURIComponent(name) + '/Accounts', this.authHeader())
      .pipe(catchError(SCH.error<any>('DevOpsServiceClient', 'getPluginAccounts')));
  }

  putPluginAccounts(name: string, accounts: any[]): Observable<any[]> {
    return this.http.put(this.BASE + 'Plugins/' + encodeURIComponent(name) + '/Accounts', accounts, this.authHeader())
      .pipe(catchError(SCH.error<any>('DevOpsServiceClient', 'putPluginAccounts')));
  }

  putPluginConfiguration(name: string, config: any): Observable<any> {
    return this.http.put(this.BASE + 'Plugins/' + encodeURIComponent(name), config, this.authHeader())
    .pipe(catchError(SCH.error<any>('DevOpsServiceClient', 'putPluginConfiguration')));
  }
}
