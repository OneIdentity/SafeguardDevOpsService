import { HttpClient, HttpEventType, HttpHeaders, HttpResponse } from '@angular/common/http';
import { ServiceClientHelper as SCH } from './service-client-helper';
import { catchError, filter as filterRx, finalize, map, tap } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { Injectable } from '@angular/core';
import { DevOpsServiceClient } from './service-client.service';

@Injectable({ providedIn: 'root' })

export class StaticInfoService {

  InstanceId: string;
  ApplianceAddress: string;

  constructor(
    private serviceClient: DevOpsServiceClient) {
  }

  load(): Observable<any> {
    return this.serviceClient.getSafeguard()
      .pipe(
        tap((data) => {
          this.InstanceId = data?.DevOpsInstanceId;
          this.ApplianceAddress = data?.ApplianceAddress;
      }));
  }
}
