import { Component, OnInit, ElementRef, ViewChild, Inject } from '@angular/core';
import { DevOpsServiceClient } from '../service-client.service';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import * as $ from 'jquery';

import { SaveCsrComponent } from '../save-csr/save-csr.component';

@Component({
  selector: 'app-create-csr',
  templateUrl: './create-csr.component.html',
  styleUrls: ['./create-csr.component.scss']
})
export class CreateCsrComponent implements OnInit {

  subjectName: string;
  dnsSubjectAlternativeNames: string;
  ipSubjectAlternativeNames: string;
  keySize: number = 2048;
  certificateType: string = '';
  showDistinguishedNameBuilder: boolean = false;
  csr = { DnsNames: [], IpAddresses: [], Text: '' };

  private readonly Ipv4Regex = /^((^\s*|\.)((25[0-5])|(2[0-4]\d)|(1\d\d)|([1-9]?\d))){4}\s*$/;
  private readonly Ipv6Regex = /^\s*([0-9a-fA-F]{1,4}|:)(:[0-9a-fA-F]{0,4}){1,7}\s*$/;
  private readonly Ipv6PrefixRegex = /^[0-9]{1,3}$/;
  private readonly CertValidSubjectCharsRegex = /^CN=[^?@#$%^&]+$/i;
  private readonly CertValidCharsRegex = /^[^?@#$%^&*]+$/;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private serviceClient: DevOpsServiceClient,
    private dialog: MatDialog,
    private dialogRef: MatDialogRef<CreateCsrComponent>
  ) { }

  ngOnInit(): void {
    this.certificateType = this.data?.certificateType ?? 'Client';
  }

  createCSR() { 
    this.serviceClient.getCSR('A2AClient', this.subjectName, this.csr.DnsNames.join(','), this.csr.IpAddresses.join(','), this.keySize)
      .subscribe(
        (csr) => {
          this.csr.Text = csr;
          const saveModal = this.dialog.open(SaveCsrComponent,
            { data: {
                CertificateType: this.certificateType,
                Base64RequestData: csr,
              }
             });
          saveModal.afterClosed().subscribe(result=>{ 
            this.dialogRef.close();
          });
        });
  }

  goBack() {
    this.dialogRef.close(null);
  }

  addSubjectAlt(whichalt,event?):void {
    if ((!event || event.key === 'Enter' || event.key === ',' || event.key === ' ') &&
        ((whichalt == 'name' ? this.dnsSubjectAlternativeNames : this.ipSubjectAlternativeNames)??'').trim()) {
      var values = (whichalt=='name' ? this.dnsSubjectAlternativeNames : this.ipSubjectAlternativeNames).split(/[ ,]/);
      this.csr.DnsNames = this.csr.DnsNames ?? [];
      this.csr.IpAddresses = this.csr.IpAddresses ?? [];
      for(var v of values) {
        if (whichalt == 'name' && this.csr.DnsNames.indexOf(v)==-1 && this.CertValidCharsRegex.test(v)) {
          this.csr.DnsNames.push(v);
        } else if (whichalt == 'ip' && this.csr.IpAddresses.indexOf(v)==-1 && (this.Ipv4Regex.test(v) || this.Ipv6Regex.test(v))) {
          this.csr.IpAddresses.push(v);
        }
      }
      if (event?.key === 'Enter') {
        event.preventDefault();
      }
      setTimeout(()=>{this.dnsSubjectAlternativeNames='';this.ipSubjectAlternativeNames='';});
    }
  }

  removeSubjectAlt(whichalt:string,value:string,allFlag?:boolean):void {
    if (allFlag) {
      if (whichalt == 'name') {
        this.csr.DnsNames = [];
      } else {
        this.csr.IpAddresses = [];
      }
    } else if ((value??'').trim()) {
      var idx = (whichalt=='name' ? this.csr.DnsNames : this.csr.IpAddresses)?.indexOf(value);
      if ((idx??-1) >= 0) {
        if (whichalt == 'name') {
          this.csr.DnsNames.splice(idx,1);
        } else {
          this.csr.IpAddresses.splice(idx,1);
        }
      }
    }
  }

}
