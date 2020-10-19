import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { ConfiguredPluginComponent } from './configured-plugin.component';

describe('ConfiguredPluginComponent', () => {
  let component: ConfiguredPluginComponent;
  let fixture: ComponentFixture<ConfiguredPluginComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ ConfiguredPluginComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(ConfiguredPluginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
