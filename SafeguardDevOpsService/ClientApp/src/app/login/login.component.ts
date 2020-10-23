import { Component, OnInit } from '@angular/core';
import { AuthService } from '../auth.service';
import { DevOpsServiceClient } from '../service-client.service';
import { switchMap, map } from 'rxjs/operators';
import { Observable, of } from 'rxjs';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {

  applianceAddress: string;
  pendingRemoval: boolean;
  oldApplianceAddress: string;

  constructor(
    private devOpsServiceClient: DevOpsServiceClient,
    private authService: AuthService) { }

  ngOnInit(): void {

    this.devOpsServiceClient.getSafeguard().subscribe(
      (data) => {
        if (data?.ApplianceAddress) {
          this.pendingRemoval = data.PendingRemoval;

          if (!this.pendingRemoval) {
            this.applianceAddress = data.ApplianceAddress;
            this.connect();
          } else {
            this.oldApplianceAddress = data.ApplianceAddress;
          }
        }
      });
  }

  deleteConfig(data: any): Observable<any> {
    return this.devOpsServiceClient.deleteConfiguration().pipe(
      //switchMap(() => this.devOpsServiceClient.deleteSafeguard().pipe(
        map(() => data)
      //))
    );
  }

  connect(): void {
    if (this.pendingRemoval) {
      this.devOpsServiceClient.deleteConfiguration().subscribe(
        () => this.authService.login(this.applianceAddress)
      );
    } else {
      this.authService.login(this.applianceAddress);
    }
  }

  handleKeyUp(e): void {
    if (e.keyCode === 13) { // Enter key
       this.connect();
    }
  }
}
