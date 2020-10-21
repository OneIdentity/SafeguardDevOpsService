import { Component, OnInit, OnDestroy, Inject } from '@angular/core';
import { MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';

import * as moment from 'moment-timezone';
import { saveAs } from 'file-saver';

export interface DialogSaveCsr {
   CertificateType: string;
   Base64RequestData: string;
}

@Component({
  selector: 'app-save-csr',
  templateUrl: './save-csr.component.html',
  styleUrls: ['./save-csr.component.scss']
})
export class SaveCsrComponent implements OnInit {

  constructor(
     public dialogRef: MatDialogRef<SaveCsrComponent>,
     @Inject(MAT_DIALOG_DATA) public csr: DialogSaveCsr
 ) { }

  ngOnInit(): void {
  }

  save() {
    var data = new Blob([this.csr.Base64RequestData], {type:'text/plain;charset=utf-8'});
    saveAs(data, this.csr.CertificateType + '__' + moment().format('YYYYMMDD[T]HHMMSS') + '.csr');
    this.dialogRef.close();
  }
}
