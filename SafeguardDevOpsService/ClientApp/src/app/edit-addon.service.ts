import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';
import { UntilDestroy } from '@ngneat/until-destroy';

@UntilDestroy()
@Injectable({ providedIn: 'root' })
export class EditAddonService {
  private notifyEventSource = new Subject<EditAddonEvent>();
  public notifyEvent$ = this.notifyEventSource.asObservable();

  constructor() {
  }

  public addon: any;
  private originalAddon: any;

  openProperties(addon: any): void {
    this.originalAddon = addon;
    this.addon = Object.assign({}, addon);

    this.notifyEventSource.next({
      addon: this.addon,
      mode: EditAddonMode.Properties
    });
  }

  deleteAddon(): void {
    this.notifyEventSource.next({
      addon: null,
      mode: EditAddonMode.None
    });
  }

  closeProperties(addon?: any): void {
    this.notifyEventSource.next({
      addon: addon ? Object.assign(this.addon, addon) : this.originalAddon,
      mode: EditAddonMode.None
    });
    this.notifyEventSource.complete();
    this.notifyEventSource = new Subject<EditAddonEvent>();
    this.notifyEvent$ = this.notifyEventSource.asObservable();
  }
}

export class EditAddonEvent {
  addon: any;
  mode: EditAddonMode;
}

export enum EditAddonMode {
  None,
  Properties
}
