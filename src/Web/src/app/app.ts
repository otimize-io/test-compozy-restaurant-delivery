import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** The application root — renders the routed shell. */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {}
