import { Component, OnInit, Inject } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { StaticInfoService } from '../static-info.service';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {

  applianceAddress: string;
  isConfigured = false;

  constructor(
    private window: Window,
    private staticInfoService: StaticInfoService) { }

  ngOnInit(): void {
    this.staticInfoService.load().subscribe(
      (data) => {
        if (data?.ApplianceAddress) {
          this.isConfigured = true;
          this.applianceAddress = this.staticInfoService.ApplianceAddress;
        }
      });
  }

  connect(): void {
    // Save this to storage since we are reloading
    this.window.sessionStorage.setItem('ApplianceAddress', this.applianceAddress);

    const redirect = encodeURIComponent(location.protocol + '//' + location.host + '/main');
    this.window.location.href = 'https://' + this.applianceAddress + '/RSTS/Login?response_type=token&redirect_uri=' + redirect;
  }

  handleKeyUp(e): void {
    if (e.keyCode === 13) { // Enter key
       this.connect();
    }
  }
}
