import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { LoginComponent } from './login/login.component';
import { MainComponent } from './main/main.component';
import { RegistrationsComponent } from './registrations/registrations.component';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'registrations', component: RegistrationsComponent },
  { path: 'main', component: MainComponent },
  { path: '', pathMatch: 'full', component: MainComponent }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
