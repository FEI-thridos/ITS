import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header>
      <span class="brand">KUKA FM Middleware</span>
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
      height: 52px;
      padding: 0 16px;
      color: #fff;
      background: #2b2f36;
    }
    .brand {
      font-weight: 600;
      white-space: nowrap;
    }
    nav {
      display: flex;
      white-space: nowrap;
    }
    nav a {
      margin-right: 12px;
      padding: 16px 4px;
      border-bottom: 3px solid transparent;
      color: #c9ced6;
      text-decoration: none;
    }
    nav a.active {
      color: #fff;
      border-bottom-color: var(--primary);
    }
    main {
      max-width: 1280px;
      margin: 0 auto;
      padding: 16px;
    }
  `,
})
export class App {}
