import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { ViewCertificateComponent } from './view-certificate.component';

describe('ViewCertificateComponent', () => {
  let component: ViewCertificateComponent;
  let fixture: ComponentFixture<ViewCertificateComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ ViewCertificateComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(ViewCertificateComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
