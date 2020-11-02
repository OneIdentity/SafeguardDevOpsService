import { Component, OnInit, Input, EventEmitter, Output } from '@angular/core';

@Component({
  selector: 'app-error-bar',
  templateUrl: './error-bar.component.html',
  styleUrls: ['./error-bar.component.scss']
})
export class ErrorBarComponent implements OnInit {
  @Input() icon: string = 'error';
  @Input() 
  set error(value: any) {
    if (!value) {
      this.message = "";
      return;
    }

    var error = value;
    if (error.error && (error.error.message || error.error.Message)) {
      this.message = error.error.message || error.error.Message;
    }
    else if (error.message || error.Message) {
      this.message = error.message || error.Message;
    }
    else {
      this.message = error.split("\n").join("<br\>");
    }
  }

  @Input() dismissable: boolean = true;

  @Output() dismissed: EventEmitter<any> = new EventEmitter();

  message = '';

  constructor() { }

  ngOnInit(): void {
  }

  dismiss() {
    this.message = '';
    this.dismissed.emit();
  }
}
