import { HttpClient, HttpEventType, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { Injectable } from '@angular/core';

@Injectable()

export class ServiceClientHelper {

  public static error<T>(className: string, method: string){
    return (error): Observable<T> => {
      console.log(`[${className}.${method}]: ${error.message}`);

      return throwError(error);
    };
  }

}
