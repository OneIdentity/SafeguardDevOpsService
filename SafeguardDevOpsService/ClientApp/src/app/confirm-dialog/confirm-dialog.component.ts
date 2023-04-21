import { Component, OnInit, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

@Component({
  selector: 'app-confirm-dialog',
  templateUrl: './confirm-dialog.component.html',
  styleUrls: ['./confirm-dialog.component.scss']
})
export class ConfirmDialogComponent implements OnInit {

  title = '';
  message = '';
  confirmText = 'OK';
  showCancel = true;
  showNo = false;
  showRestart = false;
  showSecretsBrokerOnly = false;
  showPassphrase = false;
  passwordHidden = true;
  restart = true;
  secretsBrokerOnly = true;
  passphrase = '';

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private dialogRef: MatDialogRef<ConfirmDialogComponent>) { }

  ngOnInit(): void {
    this.title = this.data.title ?? this.title;
    this.message = this.data.message ?? this.message;
    this.confirmText = this.data.confirmText ?? ((this.title.length > 0) ? this.title : this.confirmText);
    this.showCancel = this.data.showCancel ?? this.showCancel;
    this.showNo = this.data.showNo ?? this.showNo;
    this.showRestart = this.data.showRestart ?? this.showRestart;
    this.showSecretsBrokerOnly = this.data.showSecretsBrokerOnly ?? this.showSecretsBrokerOnly;
    this.showPassphrase = this.data.showPassphrase ?? this.showPassphrase;
  }

  close(): void {
    this.dialogRef.close();
  }

  confirm(confirm: boolean): void {
    this.dialogRef.close({ result: confirm ? 'OK' : 'No', restart: this.restart, secretsBrokerOnly: this.secretsBrokerOnly, passphrase: this.passphrase });
  }
}
