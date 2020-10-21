import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { SaveCsrComponent } from './save-csr.component';

describe('SaveCsrComponent', () => {
  let component: SaveCsrComponent;
  let fixture: ComponentFixture<SaveCsrComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ SaveCsrComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(SaveCsrComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
