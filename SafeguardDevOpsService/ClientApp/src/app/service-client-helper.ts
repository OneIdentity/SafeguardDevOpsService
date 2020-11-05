import { Observable, throwError } from 'rxjs';
import { Injectable } from '@angular/core';

@Injectable()

export class ServiceClientHelper {

  static parseError(value: any): string {
    if (!value) {
      return '';
    }
    let message = '';
    const error = value;
    if (error.error && error.error.ModelState) {
      // Have seen some generic "The request is invalid" coming back from APIs
      // with more detailed information in a sub-object called ModelState.
      // This will attempt to collect a resonable number of errors assuming
      // that each member of ModelState is either a string or an array of strings.
      // If there's more to it than that this can be revisited.
      // If no ModelState info gets extracted we'll just fall back to whatever
      // the generic message is.
      const msgarray = [];
      Object.keys(error.error.ModelState).forEach(
        k => {
          if (Array.isArray(error.error.ModelState[k]) || typeof (error.error.ModelState[k]) === 'string') {
            msgarray.push(error.error.ModelState[k]);
          }
        }
      );
      message = msgarray.reduce((acc, val) => acc.concat(val), []).join(' ');
    } else if (error.error && (error.error.message || error.error.Message)) {
      message = error.error.message || error.error.Message;
    }
    else if (error.message || error.Message) {
      message = error.message || error.Message;
    }
    else {
      message = error.split('\n').join('<br\>');
    }
    return message;
  }

}
