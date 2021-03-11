import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { ViewMonitorEventsComponent } from './view-monitor-events.component';

describe('ViewMonitorEventsComponent', () => {
  let component: ViewMonitorEventsComponent;
  let fixture: ComponentFixture<ViewMonitorEventsComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ ViewMonitorEventsComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(ViewMonitorEventsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
