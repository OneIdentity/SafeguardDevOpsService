import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { EditAddonComponent } from './edit-addon.component';

describe('EditAddonComponent', () => {
  let component: EditAddonComponent;
  let fixture: ComponentFixture<EditAddonComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [EditAddonComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(EditAddonComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
