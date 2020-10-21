import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { EditPluginComponent } from './edit-plugin.component';

describe('EditPluginComponent', () => {
  let component: EditPluginComponent;
  let fixture: ComponentFixture<EditPluginComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ EditPluginComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(EditPluginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
