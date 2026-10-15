## 1. Ciele a stav

- **Súčasný stav:** KUKA AMR Fleet Manager (KUKA FM) beží samostatne, bez napojenia na okolité systémy (ERP/PLM/WMS) — obsluha musí missie zadávať/sledovať priamo v jeho UI.
- **Cieľový stav:** middleware sprostredkuje riadenie a sledovanie misií pre iný (fiktívny) systém — zjednodušuje a zjednocuje prístup k funkciám KUKA FM cez vlastné REST rozhranie a webové UI.

## 2. Požiadavky

## Funkcionálne požiadavky (FR)

| ID | Požiadavka |
|---|---|
| FR1 | Middleware umožní zadať (dispatch) novú misiu robotovi cez vlastný endpoint, ktorý interne zavolá `submitMission` na KUKA FM. |
| FR2 | Middleware umožní zrušiť (cancel) rozbehnutú misiu cez vlastný endpoint, ktorý interne zavolá `cancelMission` na KUKA FM. |
| FR3 | Middleware periodicky (pollingom) zisťuje aktuálny stav aktívnych misií cez Query jobs API KUKA FM a ukladá ho do vlastnej databázy. |
| FR4 | Middleware periodicky zisťuje aktuálny stav robotov (dostupný/zaneprázdnený/offline) cez príslušné query API KUKA FM a ukladá ho do vlastnej databázy. |
| FR5 | Middleware uchováva históriu stavov jednotlivých misií (nie len posledný stav) pre základné reportovanie. |
| FR6 | Middleware poskytuje webové rozhranie (frontend) so zoznamom misií a ich aktuálnym/historickým stavom. |
| FR7 | Middleware poskytuje webové rozhranie so zoznamom robotov a ich aktuálnym stavom. |

## Nefunkcionálne požiadavky (NFR)

| ID | Požiadavka |
|---|---|
| NFR1 | Middleware je kontajnerizovaný a používa perzistentnú databázu. |
| NFR2 | Vlastné API middlewaru je chránené — neautorizovaný/neplatný request je odmietnutý. |
| NFR3 | Middleware overuje platnosť KUKA FM `Authorization` tokenu pri každom volaní na KUKA FM (nutné od úplného začiatku vývoja, inak žiadne volanie nefunguje). |
| NFR4 | Middleware zapisuje štruktúrované logy o vlastnej činnosti a chybách. |
| NFR5 | Middleware je nasaditeľný cez CI/CD pipeline (build, test, statická analýza, redeploy). |

## 3. Prípady použitia (use-case diagram)

- Diagram + krátky textový popis pre každý use case (aktér, cieľ, hlavný scenár):
  - správa misií (vrátane ukončených)
  - správa robotov

## 4. Procesný diagram / diagram aktivít

- Diagramy zachytávajú procesný tok pre jednotlivé prípady použitia.

## 5. Dátový model (ERD)

- Entity, ktoré middleware ukladá vo vlastnej DB (napr. `Mission`, `Robot`, `StatusHistory`) a vzťahy medzi nimi.

## 6. Návrh view aplikácie

- Wireframe/mockup webového rozhrania (frontend) — obrazovka pre manažment misií, obrazovka so zoznamom misií a ich stavom, obrazovka so zoznamom robotov a ich stavom. Stačí jednoduchý návrh (napr. vygenerovaný cez AI nástroj na základe požiadaviek), nie hotový grafický dizajn.

## 7. Technológie

Použité technológie pri vývoji middlewaru, napr.:
  - Backend: ASP.NET Core minimal API (C#), Entity Framework Core.
  - DB: PostgreSQL.
  - Frontend: jednoduchý web (napr. Blazor, React alebo aj čistý HTML/JS — podľa preferencie tímu).
  - Kontajnerizácia: Docker + docker-compose.

## 8. Akceptačné kritériá

- [ ] Je možné zadať novú misiu.
- [ ] Je možné zrušiť prebiehajúcu misiu.
- [ ] Je možné prezretie stavu prebiehajúcich, ktoré sa aktualizujú v reálnom čase, a ukončených misií.
- [ ] Je možné prezretie stavu robotov, ktoré sa aktualizujú v reálnom čase.
- [ ] Prístup ku KUKA FM API volaniu je zabezpečený access tokenom.
- [ ] Všetky operácie vykonané používateľom sú logované.
- [ ] Proces testovania a nasadenia je plne automatizovaným využitím CI/CD pipeline.
- [ ] Finálne riešenie je kompletne zdokumentované vo Wiki.