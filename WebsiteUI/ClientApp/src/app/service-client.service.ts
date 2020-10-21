import { HttpClient } from '@angular/common/http';
import { catchError, tap } from 'rxjs/operators';
import { Observable, throwError } from 'rxjs';
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
        alert(error);
        this.authService.login(this.applianceAddress);
      }
      console.log(`[DevOpsServiceClient.${method}]: ${error.message}`);
      return throwError(error);
    };
  }

  getSafeguard(): Observable<any> {
    const url = this.BASE + 'Safeguard';

    return this.http.get(url)
      .pipe(
        tap((data: any) => this.applianceAddress = data.ApplianceAddress),
        catchError(this.error<any>('getSafeguard')));
  }

  putSafeguard(applianceAddress: string): Observable<any> {
    const url = this.BASE + 'Safeguard';
    const payload = {
      ApplianceAddress: applianceAddress,
      ApiVersion: 3,
      IgnoreSsl: true
    };
    return this.http.put(url, payload, this.authHeader())
      .pipe(catchError(this.error<any>('putSafeguard')));
  }

  logon(): Observable<any> {
    return this.http.get(this.BASE + 'Safeguard/Logon', this.authHeader())
      .pipe(catchError(this.error<any>('logon')));
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

    return this.http.get(url, options)
      .pipe(catchError(this.error<any>('getCSR')));
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

  putPluginConfiguration(name: string, config: any): Observable<any> {
    return this.http.put(this.BASE + 'Plugins/' + encodeURIComponent(name), config, this.authHeader())
    .pipe(catchError(this.error<any>('putPluginConfiguration')));
  }

  getAvailableAccounts(filter?: string): Observable<any[]> {
    let url = this.BASE + 'Safeguard/AvailableAccounts';

    if (filter?.length > 0) {
      url += '?filter=' + encodeURIComponent(filter);
    }

    return this.http.get(url, this.authHeader())
      .pipe(catchError(this.error<any>('getAvailableAccounts')));
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
}
