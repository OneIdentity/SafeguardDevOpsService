import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { SelectAccountsComponent } from './select-accounts.component';

describe('SelectAccountsComponent', () => {
  let component: SelectAccountsComponent;
  let fixture: ComponentFixture<SelectAccountsComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ SelectAccountsComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(SelectAccountsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
