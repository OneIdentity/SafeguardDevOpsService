import { HttpClient } from '@angular/common/http';
import { catchError, tap } from 'rxjs/operators';
import { Observable, throwError, of } from 'rxjs';
import { Injectable } from '@angular/core';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })

export class DevOpsServiceClient {

  BASE = '/service/devops/v1/';
  applianceAddress: string;

  constructor(
    private http: HttpClient,
    private window: Window,
    private authService: AuthService) {
  }

  private authHeader(additionalHeaders?: any): any {
    const header = { Authorization: 'spp-token ' + this.window.sessionStorage.getItem('UserToken') };

    if (!additionalHeaders) {
      return { headers: header };
    } else {
      const allHeaders = Object.assign(header, additionalHeaders);
      return { headers: allHeaders };
    }
  }

  private error<T>(method: string) {
    return (error): Observable<T> => {
      if (error.status === 401) {
        this.authService.login(this.applianceAddress);
      }
      console.log(`[DevOpsServiceClient.${method}]: ${error.message}`);
      return throwError(error);
    };
  }

  private buildQueryArguments(countOnly: boolean, filter?: string, orderby?: string, page?: number, pageSize?: number): string {
    let query = '';

    if (filter?.length > 0) {
      query += '?filter=' + encodeURIComponent(filter);
    }
    if (orderby?.length > 0) {
      query += ((query.length > 0) ? '&' : '?') +
        'orderby=' + encodeURIComponent(orderby);
    }
    if (page >= 0) {
      query += ((query.length > 0) ? '&' : '?') +
        'page=' + encodeURIComponent(page);
    }
    if (pageSize) {
      query += ((query.length > 0) ? '&' : '?') +
        'limit=' + encodeURIComponent(pageSize);
    }
    query += ((query.length > 0) ? '&' : '?') +
      'count=' + (countOnly ? 'true' : 'false');

    return query;
  }

  getSafeguard(): Observable<any> {
    const url = this.BASE + 'Safeguard';

    return this.http.get(url)
      .pipe(
        tap((data: any) => this.applianceAddress = data.ApplianceAddress),
        catchError(this.error<any>('getSafeguard')));
  }

  putSafeguardAppliance(applianceAddress: string): Observable<any> {
    const url = this.BASE + 'Safeguard';
    const payload = {
      ApplianceAddress: applianceAddress,
      IgnoreSsl: true
    };
    return this.http.put(url, payload, this.authHeader())
      .pipe(catchError(this.error<any>('putSafeguard')));
  }

  putPendingRemoval(): Observable<any> {
    const url = this.BASE + 'Safeguard';
    const payload = {
      ApplianceAddress: this.applianceAddress,
      IgnoreSsl: true,
      PendingRemoval: true
    };
    return this.http.put(url, payload, this.authHeader())
      .pipe(catchError(this.error<any>('putSafeguard')));
  }

  putSafeguardUseSsl(useSsl: boolean): Observable<any> {
    const url = this.BASE + 'Safeguard';
    const payload = {
      ApplianceAddress: this.applianceAddress,
      IgnoreSsl: !useSsl
    };
    return this.http.put(url, payload, this.authHeader())
      .pipe(catchError(this.error<any>('putSafeguardUseSsl')));
  }

  logon(): Observable<any> {
    return this.http.get(this.BASE + 'Safeguard/Logon', this.authHeader())
      .pipe(catchError(this.error<any>('logon')));
  }

  logout(): Observable<any> {
    return this.http.get(this.BASE + 'Safeguard/Logoff', this.authHeader())
      .pipe(catchError(this.error<any>('logout')));
  }

  restart(): Observable<any> {
    return this.http.post(this.BASE + 'Safeguard/Restart', '', this.authHeader())
      .pipe(catchError(this.error<any>('restart')));
  }

  getCSR(certType: string, subjectName?: string, dnsSubjectAlternativeNames?: string, ipSubjectAlternativeNames?: string, keySize?: any): Observable<any> {
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
    if (keySize) {
      url +=  '&size=' + encodeURIComponent(keySize.toString());
    }

    const options = Object.assign({ responseType: 'text' }, this.authHeader());

    return this.http.get(url, options)
      .pipe(catchError(this.error<any>('getCSR')));
  }

  deleteSafeguard(): Observable<any> {
    return this.http.delete(this.BASE + 'Safeguard/?confirm=yes', this.authHeader())
      .pipe(catchError(this.error<any>('deleteSafeguard')));
  }

  deleteConfiguration(): Observable<any> {
    return this.http.delete(this.BASE + 'Safeguard/Configuration?confirm=yes', this.authHeader())
      .pipe(catchError(this.error<any>('deleteConfiguration')));
  }

  getConfiguration(): Observable<any> {
    return this.http.get(this.BASE + 'Safeguard/Configuration', this.authHeader())
      .pipe(catchError(this.error<any>('getConfiguration')));
  }

  postConfiguration(base64CertificateData?: string, passphrase?: string): Observable<any> {
    const url = this.BASE + 'Safeguard/Configuration';
    const payload = {
      Base64CertificateData: base64CertificateData,
      Passphrase: passphrase
    };
    return this.http.post(url, payload, this.authHeader())
      .pipe(catchError(this.error('postConfiguration')));
  }

  getPlugins(): Observable<any> {
    return this.http.get(this.BASE + 'Plugins', this.authHeader())
      .pipe(catchError(this.error<any>('getPlugins')));
  }

  postPluginFile(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('formFile', file);
    formData.append('type', file.type);

    const options = Object.assign({ responseType: 'text' }, this.authHeader());

    return this.http.post(this.BASE + 'Plugins/File', formData, options)
      .pipe(catchError(this.error<any>('postPluginFile')));
  }

  postPlugin(base64PluginData: string): Observable<any> {
    const payload = {
      Base64PluginData: base64PluginData
    };
    return this.http.post(this.BASE + 'Plugins', payload, this.authHeader())
      .pipe(catchError(this.error<any>('postPlugin')));
  }

  getPluginAccounts(name: string): Observable<any[]> {
    return this.http.get(this.BASE + 'Plugins/' + encodeURIComponent(name) + '/Accounts', this.authHeader())
      .pipe(catchError(this.error<any>('getPluginAccounts')));
  }

  putPluginAccounts(name: string, accounts: any[]): Observable<any[]> {
    return this.http.put(this.BASE + 'Plugins/' + encodeURIComponent(name) + '/Accounts', accounts, this.authHeader())
      .pipe(catchError(this.error<any>('putPluginAccounts')));
  }

  deletePluginAccounts(name: string, accounts: any[]): Observable<any[]> {
    const options = Object.assign({ body: accounts }, this.authHeader());

    return this.http.request('delete', this.BASE + 'Plugins/' + encodeURIComponent(name) + '/Accounts', options)
      .pipe(catchError(this.error<any>('putPluginAccounts')));
  }

  getPluginVaultAccount(name: string): Observable<any> {
    return this.http.get(this.BASE + 'Plugins/' + encodeURIComponent(name) + '/VaultAccount', this.authHeader())
      // Ignore 404 Not Found, when there is no vault account
      .pipe(catchError((err) => err.status === 404 ? of(undefined) : this.error<any>('getPluginVaultAccount')));
  }

  deletePluginVaultAccount(name: string): Observable<any> {
    return this.http.delete(this.BASE + 'Plugins/' + encodeURIComponent(name) + '/VaultAccount', this.authHeader())
      .pipe(catchError(this.error<any>('deletePluginVaultAccount')));
  }

  putPluginVaultAccount(name: string, account: any): Observable<any> {
    return this.http.put(this.BASE + 'Plugins/' + encodeURIComponent(name) + '/VaultAccount', account, this.authHeader())
      .pipe(catchError(this.error<any>('putPluginVaultAccount')));
  }

  putPluginConfiguration(name: string, config: any): Observable<any> {
    const payload = { Configuration: config };
    return this.http.put(this.BASE + 'Plugins/' + encodeURIComponent(name), payload, this.authHeader())
    .pipe(catchError(this.error<any>('putPluginConfiguration')));
  }

  deletePluginConfiguration(name: string): Observable<any> {
    return this.http.delete(this.BASE + 'Plugins/' + encodeURIComponent(name), this.authHeader())
    .pipe(catchError(this.error<any>('deletePluginConfiguration')));
  }

  getAvailableAccountsCount(filter?: string, orderby?: string): Observable<number> {
    const url = this.BASE + 'Safeguard/AvailableAccounts' + this.buildQueryArguments(true, filter, orderby);
    return this.http.get(url, this.authHeader())
      .pipe(catchError(this.error<any>('getAvailableAccountsCount')));
  }

  getAvailableAccounts(filter?: string, orderby?: string, page?: number, pageSize?: number): Observable<any[]> {
    const url = this.BASE + 'Safeguard/AvailableAccounts' + this.buildQueryArguments(false, filter, orderby, page, pageSize);
    return this.http.get(url, this.authHeader())
      .pipe(catchError(this.error<any>('getAvailableAccounts')));
  }

  getRetrievableAccounts(): Observable<any[]> {
    return this.http.get(this.BASE + 'Safeguard/A2ARegistration/RetrievableAccounts', this.authHeader())
      .pipe(catchError(this.error<any>('getRetrievableAccounts')));
  }

  getClientCertificate(): Observable<any> {
    return this.http.get(this.BASE + 'Safeguard/ClientCertificate', this.authHeader())
      .pipe(catchError(this.error<any>('getClientCertificate')));
  }

  postClientCertificate(base64CertificateData: string, passphrase?:string): Observable<any> {
    const url = this.BASE + 'Safeguard/ClientCertificate';
    const payload = {
      Base64CertificateData: base64CertificateData,
      Passphrase: passphrase
    };
    return this.http.post(url, payload, this.authHeader())
      .pipe(catchError(this.error<any>('postClientCertificate')));
  }

  deleteClientCertificate(): Observable<any> {
    return this.http.delete(this.BASE + 'Safeguard/ClientCertificate', this.authHeader())
      .pipe(catchError(this.error<any>('deleteClientCertificate')));
  }

  getWebServerCertificate(): Observable<any> {
    return this.http.get(this.BASE + 'Safeguard/WebServerCertificate', this.authHeader())
      .pipe(catchError(this.error<any>('getWebServerCertificate')));
  }

  postWebServerCertificate(base64CertificateData: string, passphrase?: string): Observable<any> {
    const url = this.BASE + 'Safeguard/WebServerCertificate';
    const payload = {
      Base64CertificateData: base64CertificateData,
      Passphrase: passphrase
    };
    return this.http.post(url, payload, this.authHeader())
      .pipe(catchError(this.error<any>('postWebServerCertificate')));
  }

  deleteWebServerCertificate(): Observable<any> {
    return this.http.delete(this.BASE + 'Safeguard/WebServerCertificate', this.authHeader())
      .pipe(catchError(this.error<any>('deleteWebServerCertificate')));
  }

  putRetrievableAccounts(accounts: any[]): Observable<any[]> {
    return this.http.put(this.BASE + 'Safeguard/A2ARegistration/RetrievableAccounts', accounts, this.authHeader())
      .pipe(catchError(this.error<any>('putPluginAccounts')));
  }

  getMonitor(): Observable<any> {
    return this.http.get(this.BASE + 'Monitor', this.authHeader())
      .pipe(catchError(this.error<any>('getMonitor')));
  }

  postMonitor(enabled: boolean): Observable<any> {
    return this.http.post(this.BASE + 'Monitor', { Enabled: enabled}, this.authHeader())
      .pipe(catchError(this.error<any>('putMonitor')));
  }

  getTrustedCertificates(): Observable<any[]> {
    return this.http.get(this.BASE + 'Safeguard/TrustedCertificates', this.authHeader())
      .pipe(catchError(this.error<any>('getTrustedCertificates')));
  }

  postTrustedCertificates(importFromSafeguard: boolean, certificateBase64Data?: string, passphrase?: string): Observable<any[]> {
    let url = this.BASE + 'Safeguard/TrustedCertificates';
    let payload = {};

    if (importFromSafeguard) {
      url += '?importFromSafeguard=true';
    } else {
      payload = {
        Base64CertificateData: certificateBase64Data,
        Passphrase: passphrase
      };
    }

    return this.http.post(url, payload, this.authHeader())
      .pipe(catchError(this.error<any>('postTrustedCertificates')));
  }

  deleteTrustedCertificate(thumbprint: string): Observable<any> {
    return this.http.delete(this.BASE + 'Safeguard/TrustedCertificates/' + encodeURIComponent(thumbprint), this.authHeader())
      .pipe(catchError(this.error<any>('deleteTrustedCertificate')));
  }
}
