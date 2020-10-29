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
  @ViewChild('subject', {static:true}) subjectInput: ElementRef;

  countryCodes = [
    { Code: "AF", Name: "Afghanistan" },
    { Code: "AX", Name: "\xc5land Islands" }, // Åland Islands
    { Code: "AL", Name: "Albania" },
    { Code: "DZ", Name: "Algeria" },
    { Code: "AS", Name: "American Samoa" },
    { Code: "AD", Name: "Andorra" },
    { Code: "AO", Name: "Angola" },
    { Code: "AI", Name: "Anguilla" },
    { Code: "AQ", Name: "Antarctica" },
    { Code: "AG", Name: "Antigua and Barbuda" },
    { Code: "AR", Name: "Argentina" },
    { Code: "AM", Name: "Armenia" },
    { Code: "AW", Name: "Aruba" },
    { Code: "AU", Name: "Australia" },
    { Code: "AT", Name: "Austria" },
    { Code: "AZ", Name: "Azerbaijan" },
    { Code: "BS", Name: "Bahamas" },
    { Code: "BH", Name: "Bahrain" },
    { Code: "BD", Name: "Bangladesh" },
    { Code: "BB", Name: "Barbados" },
    { Code: "BE", Name: "Belgium" },
    { Code: "BZ", Name: "Belize" },
    { Code: "BJ", Name: "Benin" },
    { Code: "BM", Name: "Bermuda" },
    { Code: "BT", Name: "Bhutan" },
    { Code: "BO", Name: "Bolivia" },
    { Code: "BA", Name: "Bosnia and Herzegovina" },
    { Code: "BW", Name: "Botswana" },
    { Code: "BV", Name: "Bouvet Island" },
    { Code: "BR", Name: "Brazil" },
    { Code: "IO", Name: "British Indian Ocean Territory" },
    { Code: "BN", Name: "Brunei Darussalam" },
    { Code: "BG", Name: "Bulgaria" },
    { Code: "BF", Name: "Burkina Faso" },
    { Code: "BI", Name: "Burundi" },
    { Code: "KH", Name: "Cambodia" },
    { Code: "CM", Name: "Cameroon" },
    { Code: "CA", Name: "Canada" },
    { Code: "CV", Name: "Cape Verde" },
    { Code: "KY", Name: "Cayman Islands" },
    { Code: "CF", Name: "Central African Republic" },
    { Code: "TD", Name: "Chad" },
    { Code: "CL", Name: "Chile" },
    { Code: "CN", Name: "China" },
    { Code: "CX", Name: "Christmas Island" },
    { Code: "CC", Name: "Cocos (Keeling) Islands" },
    { Code: "CO", Name: "Colombia" },
    { Code: "KM", Name: "Comoros" },
    { Code: "CK", Name: "Cook Islands" },
    { Code: "CR", Name: "Costa Rica" },
    { Code: "CI", Name: "Cote D'Ivoire (Ivory Coast)" },
    { Code: "HR", Name: "Croatia (Hrvatska)" },
    { Code: "CY", Name: "Cyprus" },
    { Code: "CZ", Name: "Czech Republic" },
    { Code: "CS", Name: "Czechoslovakia (former)" },
    { Code: "DK", Name: "Denmark" },
    { Code: "DJ", Name: "Djibouti" },
    { Code: "DM", Name: "Dominica" },
    { Code: "DO", Name: "Dominican Republic" },
    { Code: "TP", Name: "East Timor" },
    { Code: "EC", Name: "Ecuador" },
    { Code: "EG", Name: "Egypt" },
    { Code: "SV", Name: "El Salvador" },
    { Code: "GQ", Name: "Equatorial Guinea" },
    { Code: "ER", Name: "Eritrea" },
    { Code: "EE", Name: "Estonia" },
    { Code: "ET", Name: "Ethiopia" },
    { Code: "FK", Name: "Falkland Islands (Malvinas)" },
    { Code: "FO", Name: "Faroe Islands" },
    { Code: "FJ", Name: "Fiji" },
    { Code: "FI", Name: "Finland" },
    { Code: "FR", Name: "France" },
    { Code: "FX", Name: "France, Metropolitan" },
    { Code: "GF", Name: "French Guiana" },
    { Code: "PF", Name: "French Polynesia" },
    { Code: "TF", Name: "French Southern Territories" },
    { Code: "GA", Name: "Gabon" },
    { Code: "GM", Name: "Gambia" },
    { Code: "GE", Name: "Georgia" },
    { Code: "DE", Name: "Germany" },
    { Code: "GH", Name: "Ghana" },
    { Code: "GI", Name: "Gibraltar" },
    { Code: "GB", Name: "Great Britain (UK)" },
    { Code: "GR", Name: "Greece" },
    { Code: "GL", Name: "Greenland" },
    { Code: "GD", Name: "Grenada" },
    { Code: "GP", Name: "Guadeloupe" },
    { Code: "GU", Name: "Guam" },
    { Code: "GT", Name: "Guatemala" },
    { Code: "GG", Name: "Guernsey" },
    { Code: "GN", Name: "Guinea" },
    { Code: "GW", Name: "Guinea-Bissau" },
    { Code: "GY", Name: "Guyana" },
    { Code: "HT", Name: "Haiti" },
    { Code: "HM", Name: "Heard and McDonald Islands" },
    { Code: "HN", Name: "Honduras" },
    { Code: "HK", Name: "Hong Kong" },
    { Code: "HU", Name: "Hungary" },
    { Code: "IS", Name: "Iceland" },
    { Code: "IN", Name: "India" },
    { Code: "ID", Name: "Indonesia" },
    { Code: "IE", Name: "Ireland" },
    { Code: "IM", Name: "Isle of Man" },
    { Code: "IL", Name: "Israel" },
    { Code: "IT", Name: "Italy" },
    { Code: "JM", Name: "Jamaica" },
    { Code: "JP", Name: "Japan" },
    { Code: "JE", Name: "Jersey" },
    { Code: "JO", Name: "Jordan" },
    { Code: "KZ", Name: "Kazakhstan" },
    { Code: "KE", Name: "Kenya" },
    { Code: "KI", Name: "Kiribati" },
    { Code: "KR", Name: "Korea (South)" },
    { Code: "KW", Name: "Kuwait" },
    { Code: "KG", Name: "Kyrgyzstan" },
    { Code: "LA", Name: "Laos" },
    { Code: "LV", Name: "Latvia" },
    { Code: "LS", Name: "Lesotho" },
    { Code: "LY", Name: "Libya" },
    { Code: "LI", Name: "Liechtenstein" },
    { Code: "LT", Name: "Lithuania" },
    { Code: "LU", Name: "Luxembourg" },
    { Code: "MO", Name: "Macau" },
    { Code: "MK", Name: "Macedonia" },
    { Code: "MG", Name: "Madagascar" },
    { Code: "MW", Name: "Malawi" },
    { Code: "MY", Name: "Malaysia" },
    { Code: "MV", Name: "Maldives" },
    { Code: "ML", Name: "Mali" },
    { Code: "MT", Name: "Malta" },
    { Code: "MH", Name: "Marshall Islands" },
    { Code: "MQ", Name: "Martinique" },
    { Code: "MR", Name: "Mauritania" },
    { Code: "MU", Name: "Mauritius" },
    { Code: "YT", Name: "Mayotte" },
    { Code: "MX", Name: "Mexico" },
    { Code: "FM", Name: "Micronesia" },
    { Code: "MD", Name: "Moldova" },
    { Code: "MC", Name: "Monaco" },
    { Code: "MN", Name: "Mongolia" },
    { Code: "ME", Name: "Montenegro" },
    { Code: "MS", Name: "Montserrat" },
    { Code: "MA", Name: "Morocco" },
    { Code: "MZ", Name: "Mozambique" },
    { Code: "MM", Name: "Myanmar" },
    { Code: "NA", Name: "Namibia" },
    { Code: "NR", Name: "Nauru" },
    { Code: "NP", Name: "Nepal" },
    { Code: "AN", Name: "Netherlands Antilles" },
    { Code: "NL", Name: "Netherlands" },
    { Code: "NT", Name: "Neutral Zone" },
    { Code: "NC", Name: "New Caledonia" },
    { Code: "NZ", Name: "New Zealand (Aotearoa)" },
    { Code: "NI", Name: "Nicaragua" },
    { Code: "NE", Name: "Niger" },
    { Code: "NG", Name: "Nigeria" },
    { Code: "NU", Name: "Niue" },
    { Code: "NF", Name: "Norfolk Island" },
    { Code: "MP", Name: "Northern Mariana Islands" },
    { Code: "NO", Name: "Norway" },
    { Code: "OM", Name: "Oman" },
    { Code: "PK", Name: "Pakistan" },
    { Code: "PW", Name: "Palau" },
    { Code: "PS", Name: "Palestinian Territory" },
    { Code: "PA", Name: "Panama" },
    { Code: "PG", Name: "Papua New Guinea" },
    { Code: "PY", Name: "Paraguay" },
    { Code: "PE", Name: "Peru" },
    { Code: "PH", Name: "Philippines" },
    { Code: "PN", Name: "Pitcairn" },
    { Code: "PL", Name: "Poland" },
    { Code: "PT", Name: "Portugal" },
    { Code: "PR", Name: "Puerto Rico" },
    { Code: "QA", Name: "Qatar" },
    { Code: "RE", Name: "Reunion" },
    { Code: "RO", Name: "Romania" },
    { Code: "RU", Name: "Russian Federation" },
    { Code: "RW", Name: "Rwanda" },
    { Code: "GS", Name: "S. Georgia and S. Sandwich Isls." },
    { Code: "KN", Name: "Saint Kitts and Nevis" },
    { Code: "LC", Name: "Saint Lucia" },
    { Code: "VC", Name: "Saint Vincent and the Grenadines" },
    { Code: "WS", Name: "Samoa" },
    { Code: "SM", Name: "San Marino" },
    { Code: "ST", Name: "Sao Tome and Principe" },
    { Code: "SA", Name: "Saudi Arabia" },
    { Code: "SN", Name: "Senegal" },
    { Code: "RS", Name: "Serbia" },
    { Code: "SC", Name: "Seychelles" },
    { Code: "SL", Name: "Sierra Leone" },
    { Code: "SG", Name: "Singapore" },
    { Code: "SK", Name: "Slovak Republic" },
    { Code: "SI", Name: "Slovenia" },
    { Code: "SB", Name: "Solomon Islands" },
    { Code: "ZA", Name: "South Africa" },
    { Code: "ES", Name: "Spain" },
    { Code: "LK", Name: "Sri Lanka" },
    { Code: "SH", Name: "St. Helena" },
    { Code: "PM", Name: "St. Pierre and Miquelon" },
    { Code: "SR", Name: "Suriname" },
    { Code: "SJ", Name: "Svalbard and Jan Mayen Islands" },
    { Code: "SZ", Name: "Swaziland" },
    { Code: "SE", Name: "Sweden" },
    { Code: "CH", Name: "Switzerland" },
    { Code: "TW", Name: "Taiwan" },
    { Code: "TJ", Name: "Tajikistan" },
    { Code: "TZ", Name: "Tanzania" },
    { Code: "TH", Name: "Thailand" },
    { Code: "TG", Name: "Togo" },
    { Code: "TK", Name: "Tokelau" },
    { Code: "TO", Name: "Tonga" },
    { Code: "TT", Name: "Trinidad and Tobago" },
    { Code: "TN", Name: "Tunisia" },
    { Code: "TR", Name: "Turkey" },
    { Code: "TM", Name: "Turkmenistan" },
    { Code: "TC", Name: "Turks and Caicos Islands" },
    { Code: "TV", Name: "Tuvalu" },
    { Code: "UM", Name: "US Minor Outlying Islands" },
    { Code: "SU", Name: "USSR (former)" },
    { Code: "UG", Name: "Uganda" },
    { Code: "UA", Name: "Ukraine" },
    { Code: "AE", Name: "United Arab Emirates" },
    { Code: "US", Name: "United States of America" },
    { Code: "UY", Name: "Uruguay" },
    { Code: "UZ", Name: "Uzbekistan" },
    { Code: "VU", Name: "Vanuatu" },
    { Code: "VA", Name: "Vatican City State (Holy See)" },
    { Code: "VE", Name: "Venezuela" },
    { Code: "VN", Name: "Viet Nam" },
    { Code: "VG", Name: "Virgin Islands (British)" },
    { Code: "VI", Name: "Virgin Islands (U.S.)" },
    { Code: "WF", Name: "Wallis and Futuna Islands" },
    { Code: "EH", Name: "Western Sahara" },
    { Code: "YE", Name: "Yemen" },
    { Code: "ZM", Name: "Zambia" },
  ];

  subjectName: string;
  savedSubjectName: string;
  dnsSubjectAlternativeNames: string;
  ipSubjectAlternativeNames: string;
  keySize: number = 2048;
  certificateType: string = '';
  showDistinguishedNameBuilder: boolean = false;
  creatingCSR: boolean = false;
  csr = { DnsNames: [], IpAddresses: [], Text: '' };
  dnBuilder = {
    FullyQualifiedDomainName: '',
    Department: '',
    Organization: '',
    City: '',
    State: '',
    Country: ''
  };

  readonly Ipv4Regex = /^((^\s*|\.)((25[0-5])|(2[0-4]\d)|(1\d\d)|([1-9]?\d))){4}\s*$/;
  readonly Ipv6Regex = /^\s*([0-9a-fA-F]{1,4}|:)(:[0-9a-fA-F]{0,4}){1,7}\s*$/;
  readonly Ipv6PrefixRegex = /^[0-9]{1,3}$/;
  readonly CertValidSubjectCharsRegex = /^CN=[^?@#$%^&]+$/i;
  readonly CertValidCharsRegex = /^[^?@#$%^&*]+$/;

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
    this.creatingCSR = true;
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
        }).add(() => {this.creatingCSR = false;});
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

  useDNBuilder(state: boolean): void {
    this.showDistinguishedNameBuilder = state;
    if (state) {
      this.savedSubjectName = this.subjectName;
      this.dnBuilder = {
        FullyQualifiedDomainName: '',
        Department: '',
        Organization: '',
        City: '',
        State: '',
        Country: ''
      };
      setTimeout(()=>{$('#FullyQualifiedDomainName').focus();},0);
    } else {
      this.savedSubjectName = '';
      setTimeout(() => {
        this.subjectInput.nativeElement.focus();
        this.subjectInput.nativeElement.setSelectionRange(0,this.subjectName.length);
      },0);
    }
  }

  cancelDNBuilder(): void {
    this.subjectName = this.savedSubjectName;
    this.useDNBuilder(false);
  }
  
  buildDn(ev: Event=null): void {
    var fqdn = this.dnBuilder.FullyQualifiedDomainName.trim();
    this.subjectName = fqdn ? 'CN=' + fqdn : '';
    if (fqdn) {
      this.subjectName += (this.dnBuilder.Department ? ', OU=' + this.dnBuilder.Department : '') +
        (this.dnBuilder.Organization ? ', O=' + this.dnBuilder.Organization : '') +
        (this.dnBuilder.City ? ', L=' + this.dnBuilder.City : '') +
        (this.dnBuilder.State ? ', S=' + this.dnBuilder.State : '') +
        (this.dnBuilder.Country ? ', C=' + this.dnBuilder.Country : '');
    }
  }
}
