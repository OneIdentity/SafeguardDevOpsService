import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { EditTrustedCertificatesComponent } from './edit-trusted-certificates.component';

describe('EditTrustedCertificatesComponent', () => {
  let component: EditTrustedCertificatesComponent;
  let fixture: ComponentFixture<EditTrustedCertificatesComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ EditTrustedCertificatesComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(EditTrustedCertificatesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
