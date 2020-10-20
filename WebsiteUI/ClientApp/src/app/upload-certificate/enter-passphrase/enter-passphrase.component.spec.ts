import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { EnterPassphraseComponent } from './enter-passphrase.component';

describe('EnterPassphraseComponent', () => {
  let component: EnterPassphraseComponent;
  let fixture: ComponentFixture<EnterPassphraseComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ EnterPassphraseComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(EnterPassphraseComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
