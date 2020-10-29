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

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private dialogRef: MatDialogRef<ConfirmDialogComponent>) { }

  ngOnInit(): void {
    this.title = this.data.title ?? this.title;
    this.message = this.data.message ?? this.message;
    this.confirmText = this.data.confirmText ?? ((this.title.length > 0) ? this.title : this.confirmText);
    this.showCancel = this.data.showCancel ?? this.showCancel;
  }

  close(): void {
    this.dialogRef.close();
  }

  confirm(): void {
    this.dialogRef.close({ result: 'OK' });
  }
}
