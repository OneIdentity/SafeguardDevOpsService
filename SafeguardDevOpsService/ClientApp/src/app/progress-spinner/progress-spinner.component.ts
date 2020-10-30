import { Component, Input, OnInit } from '@angular/core';

@Component({
  selector: 'app-progress-spinner',
  templateUrl: './progress-spinner.component.html',
  styleUrls: ['./progress-spinner.component.scss']
})
export class ProgressSpinnerComponent implements OnInit {
  @Input() visible: boolean = true;
  @Input() noMargin: boolean = false;
  @Input() message : string;
  @Input() color : string = "primary";
  @Input() isWhiteSpinner: boolean = false;
  @Input() diameter: number = 15;

  constructor() { }

  ngOnInit(): void {
    if (!this.message) {
      this.message = "";
    }
  }

}
