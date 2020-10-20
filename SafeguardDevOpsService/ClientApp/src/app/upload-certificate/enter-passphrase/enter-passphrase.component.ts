import { Component, OnInit, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

@Component({
  selector: 'app-enter-passphrase',
  templateUrl: './enter-passphrase.component.html',
  styleUrls: ['./enter-passphrase.component.scss']
})
export class EnterPassphraseComponent implements OnInit {

  fileName: string;
  passphrase: string;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private dialogRef: MatDialogRef<EnterPassphraseComponent>
  ) { }

  ngOnInit(): void {
    this.fileName = this.data.fileName;
  }

  submit(): void {
    this.dialogRef.close(this.passphrase);
  }

  handleKeyUp(e): void {
    if (e.keyCode === 13) { // Enter key
       this.submit();
    }
  }
}
