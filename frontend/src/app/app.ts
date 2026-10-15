import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header>
      <span class="title">KUKA FM Middleware</span>
      <nav>
        <a routerLink="/missions" routerLinkActive="active">Missions</a>
        <a routerLink="/robots" routerLinkActive="active">Robots</a>
      </nav>
    </header>
    <main>
      <router-outlet />
    </main>
  `,
  styles: `
    header {
      display: flex;
      align-items: center;
      gap: 24px;
      padding: 12px 24px;
      background: #1f2937;
      color: #fff;
    }
    .title {
      font-weight: 600;
      font-size: 16px;
    }
    nav {
      display: flex;
      gap: 16px;
    }
    nav a {
      color: #d1d5db;
      text-decoration: none;
    }
    nav a.active {
      color: #fff;
      font-weight: 600;
    }
    main {
      padding: 24px;
    }
  `,
})
export class App {}
