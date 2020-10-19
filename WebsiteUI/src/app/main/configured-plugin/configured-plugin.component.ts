import { Component, OnInit, Input } from '@angular/core';

@Component({
  selector: 'app-configured-plugin',
  templateUrl: './configured-plugin.component.html',
  styleUrls: ['./configured-plugin.component.scss', '../main.component.scss']
})
export class ConfiguredPluginComponent implements OnInit {
  @Input() plugin: any;

  constructor() { }

  ngOnInit(): void {
  }

}
