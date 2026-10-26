# Refacerea UI: sidebar, taburi, dashboard

## Context

Acum aplicația are un `AppHeader` sus și pagini lungi. Pe pagina proiectului, etapele, livrabilele, actualizările, fișierele, formularul și arhivarea stau una sub alta, iar în portal e la fel. Vrem o structură inspirată din Clienter și TaskView, fără să le copiem:

- **un sidebar în stânga**, cu secțiuni clare și un **switcher** de firmă / workspace (și pentru client);
- **taburi** acolo unde o pagină amestecă informații și acțiuni;
- un **dashboard / Home** pentru freelancer și unul pentru client;
- freelancerul și clientul văd o structură asemănătoare, dar fiecare doar ce îl ajută.

Deciziile luate deja împreună:

- **Funcțiile viitoare apar în sidebar ca intrări dezactivate „Soon”** (Messages, Meetings, Calendar, Tasks). Nu se construiește nimic din ele acum.
- **Un singur switcher** cu două grupuri: „Your workspaces” și „Client of”. Alegerea schimbă meniul din sidebar: de freelancer sau de client.
- **Taburi în pagină** (shadcn `Tabs`), nu câte un URL pe tab. Tabul inițial se poate da prin `?tab=`, ca un link („See what to approve”, „Edit project”) să deschidă direct tabul potrivit.
- **Aspectul rămâne presetul neutru base-nova**, cu **dark implicit** (în loc de `system`). Light și System rămân disponibile.
- **Dashboard-ul freelancerului se face întâi din datele existente.** Aprobările în așteptare și modificările cerute vin într-un pas de backend separat, la final.

Fiecare pas de mai jos e mic, se verifică și se poate face commit separat, conform regulilor din `CLAUDE.md`.

## Pas 0: salvarea planului (făcut)

- Copiez planul în `.claude/docs/ui-redesign.md`, ca să fie în repo și pe celelalte device-uri.
- Adaug în `product-plan.md` → `Progres` punctul „Refacerea UI (sidebar, taburi, dashboard)”, cu sub-pașii de mai jos.

## Structura țintă

**Sidebar-ul freelancerului** (`/w/{id}/...`)

```
[N] Nova Studio  ⌄     ← switcher (Your workspaces / Client of / + Create workspace)
  Home
  Projects
  Clients
Collaborate
  Messages     Soon
  Meetings     Soon
Plan
  Calendar     Soon
  Tasks        Soon
Active projects        ← cel mult 5 linkuri, ca în TaskView
  Website
  Brand refresh
────────────
  Settings             ← setările workspace-ului
[RV] Rares  ⌄          ← meniul contului: Account, Theme, Sign out
```

**Sidebar-ul clientului** (`/portal/{workspaceId}/...`): același switcher, apoi Home, Projects, Messages (Soon) și „Your projects” (lista scurtă). În subsol e doar meniul contului. Navigarea clientului rămâne mică, cum cere planul.

**Intrările „Soon”** arată direcția produsului, dar nu sunt linkuri. Fiecare e un `SidebarMenuButton` cu `aria-disabled`, în afara ordinii de tab, cu însemnul `SidebarMenuBadge` „Soon”. Lista lor stă într-un singur array de configurare: când o funcție e gata, intrarea primește `href` și pierde însemnul. Ce e în ea: Messages, Meetings (cu Google Calendar), Calendar, Tasks, toate din „Dorite după MVP”.

**Conținutul paginii:** o bară subțire cu `SidebarTrigger` (care deschide sidebar-ul ca sheet pe mobil) și un `Breadcrumb` (de ex. Projects › Website). Breadcrumb-ul înlocuiește linkurile „← Projects” / „← Clients”. Conținutul e mai lat decât azi (`max-w-3xl`): cam `max-w-6xl` pe paginile cu două coloane.

## Pas 1: shell-ul aplicației (sidebar, switcher, meniul contului) — făcut

Abateri față de plan, ca să nu fie o surpriză la pașii următori:

- `/workspaces/new` stă și el în shell (grupul `(shell)`), nu fără: un cont nou fără workspace are nevoie de meniul contului (Sign out, tema).
- Pagina are un singur component de cadru, `Page` (bara de sus cu breadcrumb și coloana de conținut), în loc de breadcrumb în layout: layout-ul nu știe numele proiectului. Lățimea implicită e `max-w-5xl`, formularele folosesc `narrow` (`max-w-3xl`).
- În portal, lista „Client of” duce provizoriu la `/portal` pentru toate firmele (`portalHref` în `ContextSwitcher.tsx`); Pasul 2 o înlocuiește cu `/portal/{workspaceId}`.
- Sidebar-ul portalului are Home, Messages (Soon) și „Your projects”; „Projects” apare la Pasul 2, când există pagina de listă pe firmă.
- `use-mobile.ts` (generat de shadcn) e rescris cu `useSyncExternalStore`, fiindcă varianta cu `setState` într-un effect pică la `pnpm lint` (`react-hooks/set-state-in-effect`).
- Sidebar-ul își ține starea (deschis / închis) într-un cookie, citit pe server în `AppShell`.
- După crearea unui proiect, `ProjectForm` apelează și `router.refresh()`, ca lista din sidebar să se actualizeze.

- Adaug componentele shadcn: `pnpm dlx shadcn@latest add sidebar tabs breadcrumb avatar` (din `frontend/`). `sidebar` aduce și `sheet`, `tooltip`, `skeleton` și hook-ul `use-mobile`. Înainte de a le adăuga citesc regulile din skill-ul `shadcn` despre Base UI (`render`, nu `asChild`).
- Componente noi în `src/components/shell/`:
  - `AppShell.tsx`: `SidebarProvider` + `AppSidebar` + `SidebarInset`, plus bara de sus cu trigger și breadcrumb (componentă de server, cu props pentru context).
  - `AppSidebar.tsx`: secțiunile de mai sus. Contextul e `{ kind: "workspace", workspace } | { kind: "portal", workspaceId } | { kind: "none" }`.
  - `ContextSwitcher.tsx` (client): un `DropdownMenu` cu cele două grupuri. Elementul activ e marcat cu `aria-current`.
  - `NavUser.tsx` (client): avatar cu inițiale, nume și email. Meniul are Account, Theme (Light / Dark / System, din actualul `theme-toggle.tsx`) și Sign out (logica din `SignOutButton.tsx`).
  - `NavLink`: link activ după `usePathname()`, pentru starea `isActive` din `SidebarMenuButton`.
- **Datele shell-ului** se încarcă în layout-uri noi: `src/app/w/[workspaceId]/layout.tsx` și `src/app/portal/layout.tsx`, prin funcțiile existente (`getMyWorkspaces`, `getPortalWorkspaces`, `getCurrentUser`, `getWorkspace`, `getProjects`). Toate sunt `cache`, deci se reutilizează în pagină. Verificarea de acces rămâne `requireUser()` în fiecare pagină, cum cere ghidul Next.js; layout-ul doar afișează. Înainte de cod citesc ghidurile de layout din `node_modules/next/dist/docs/`.
- `/account` și `/workspaces` folosesc shell-ul cu `kind: "none"`, adică switcher și meniul contului, fără meniu de workspace. `/workspaces/new` și paginile `(auth)` rămân fără shell.
- `theme-provider.tsx`: `defaultTheme="dark"`.
- Șterg `AppHeader.tsx`. Butonul `ThemeToggle` rămâne doar pe paginile de autentificare.
- **E2E:** selectorii `getByRole("banner")` trec pe `getByRole("navigation", { name: ... })` / sidebar (`account`, `workspaces`, `projects`, `clients`), iar „← Clients” devine linkul din breadcrumb. `theme.spec.ts` se adaptează la dark implicit.

## Pas 2: portalul pe firmă (URL-uri noi) — făcut

Home-ul și lista de proiecte arată deocamdată același lucru (`PortalProjectList`); Home primește conținut propriu la Pasul 7. Layout-ul `portal/[workspaceId]/layout.tsx` încarcă firma și proiectele ei pentru sidebar; fiecare pagină verifică firma și ea (`getPortalWorkspace`), nu doar layout-ul.

- Rute noi: `/portal/[workspaceId]` (Home-ul clientului la acea firmă, deocamdată lista proiectelor de la ea, ca azi), `/portal/[workspaceId]/projects` și `/portal/[workspaceId]/projects/[projectId]`. Ultima apelează `getPortalProject` și dă `notFound()` dacă `project.workspace.id !== workspaceId`. Backend-ul nu se schimbă: proiectul are deja `workspace`.
- `/portal` devine doar redirect: 0 firme → pagină goală explicativă, 1 → `/portal/{id}`, mai multe → prima (switcher-ul le arată pe toate).
- `/` trimite clientul pur la `/portal`, ca azi. `AcceptInvitationButton` și `InvitationRegisterForm` merg la `/portal`, iar acesta redirecționează mai departe.
- Mut `src/app/portal/projects/[projectId]/` sub `[workspaceId]` și actualizez linkurile din `client-portal.spec.ts` / `files.spec.ts`.
- **Decizie nouă** în `decisions.md`: portalul are firma în URL (înlocuiește „Fără workspace în rută” pentru frontend; API-ul rămâne fără).

## Pas 3: pagina proiectului (freelancer) pe taburi — făcut

Abateri față de planul inițial, după prima variantă văzută în aplicație:

- **Doar două taburi:** Overview (etape, **Updates**, cardurile Deadline și Approvals) și **Deliverables & Files** (cele două liste alăturate pe ecran lat). Updates a trecut în Overview, iar Files lângă Deliverables.
- **Fără tab Settings:** editarea proiectului și arhivarea sunt într-un `Sheet` (panel lateral) deschis din butonul „Edit project”, în dreapta numelui. Panelul învelește pagina, deci și „Set dates” din cardul Deadline îl deschide.
- Tabul vine din `useSearchParams` și stă într-o stare locală (`ProjectTabs` cu `key` pe proiect); `GoToTab` e un buton, nu un link, fiindcă schimbă doar tabul. Pe ecran îngust cardurile „Deadline” și „Approvals” apar înaintea etapelor. Cardul „Deadline” nu are bară de progres (doar zilele rămase și datele).
- La Pasul 4, aceeași idee: fișa clientului cu un buton „Edit client” (panel) în loc de tab „Details”, de confirmat înainte de implementare.

`src/app/w/[workspaceId]/projects/[projectId]/page.tsx`:

- **Antet** deasupra taburilor: nume, `ProjectStatusBadge`, clientul (link), mențiunea „Not visible in their portal yet” la Planning și alerta de proiect arhivat.
- **Taburi** (`ProjectTabs`, componentă de client care primește panourile ca `children` randate pe server):
  - **Overview**: în stânga, pe ecran lat, `StageList` (etapele cu progresul). În dreapta un card „Deadline” (zile rămase, din `startDate` / `dueDate`) și un card „Approvals” calculat din livrabilele deja încărcate (în așteptare / modificări cerute, cu link spre tabul Deliverables).
  - **Deliverables**: `DeliverableList`. **Updates**: `UpdateList`. **Files**: `FileList`.
  - **Settings**: `ProjectForm` și secțiunea de arhivare.
- Tabul inițial vine din `?tab=`; schimbarea tabului actualizează URL-ul cu `history.replaceState` (Next 16 îl sincronizează cu `useSearchParams`, de verificat în docs). Componentele existente rămân neschimbate, doar se mută.
- Taburile cu număr afișează un contor discret (de ex. „Deliverables 3”).
- **E2E** (`projects.spec.ts`, `files.spec.ts`): clic pe tab înainte de a lucra într-o secțiune.

## Pas 4: pagina clientului (freelancer), cu editarea în panel lateral — făcut

Abateri față de planul inițial (Overview + Details), după ce am ales panelul în loc de tab:

- **Fără taburi:** pagina are antetul (nume, buton „Edit client”, o linie cu email · companie · telefon, alertele), apoi pe ecran lat proiectele clientului în stânga și cardul „Client portal” în dreapta (`ClientPortalCard`: starea invitației, descrierea și acțiunile). Pe ecran îngust cardul apare primul.
- `ClientForm` și arhivarea / restaurarea sunt într-un `Sheet`, ca la proiect. Insigna de stare a portalului stă în card, lângă titlu, nu în antet.
- **E2E:** helper nou `openClientEditor` în `e2e/helpers.ts`; `clients.spec.ts` și `invitations.spec.ts` lucrează în panel. Panelul e modal, deci conținutul din spate nu mai e vizibil pentru role-queries cât timp e deschis.

**Retușare ulterioară:** pagina Clients are acum căutare, taburi Active / Archived și numărul de proiecte pe rând, iar pagina clientului un antet de profil și cardul „Notes”. Decizia e în `decisions.md`.

## Pas 5: proiectul în portal (client) pe taburi — făcut

`/portal/[workspaceId]/projects/[projectId]`, mai simplu decât la freelancer:

- **Overview**: descrierea, bannerul „Waiting for you” (butonul lui, `GoToTab`, deschide tabul Deliverables), „Where things stand”, ultimele 3 actualizări (cu „See all updates (N)”) și etapele.
- **Deliverables**: livrabilele cu `PortalApprovalPanel`. Tabul are un însemn evidențiat cu numărul celor care așteaptă clientul.
- **Updates**: `PortalUpdateList`. **Files**: `PortalFileList`.
- Deliverables și Files se ascund cât timp nu au nimic partajat; Updates rămâne, pentru că starea lui goală spune la ce să se aștepte.
- `ProjectTabs` primește acum lista de taburi (`tabs`: valoare, etichetă, contor, `highlight`) în loc de una fixă, deci îl folosesc ambele pagini; primul tab e cel implicit.
- Bannerul e doar pe Overview, deci testele urmăresc starea aprobărilor prin însemnul tabului Deliverables.
- **E2E:** `client-portal.spec.ts`, `files.spec.ts` (deschid tabul înainte de a citi o secțiune).

## Pas 6: dashboard-ul freelancerului (`/w/[workspaceId]`) — făcut

Abateri: „Waiting on clients” stă într-o coloană îngustă în dreapta (pe ecran îngust vine după celelalte), iar starea goală înlocuiește „Coming up” și „Active projects” cât timp workspace-ul n-are niciun proiect nearhivat. Logica scadențelor e în `lib/dashboard/deadlines.ts`, blocurile în `components/dashboard/`.

Doar din datele existente (`getProjects` cu etape, `getClients`):

- un salut scurt cu numele workspace-ului;
- **„Coming up”**: deadline-urile proiectelor și ale etapelor din următoarele 14 zile, sortate, plus cele depășite (marcate);
- **„Active projects”**: proiectele Active / On hold, cu progres, client și deadline, cu link spre proiect;
- **„Waiting on clients”**: invitațiile în așteptare sau expirate (din statusul portalului fiecărui client), cu link spre client;
- o stare goală utilă pentru un workspace nou („Add your first client” → „Create a project”).

Fără rânduri de „stat cards” decorative: fiecare bloc e o listă pe care se poate acționa. Test e2e nou: dashboard-ul arată un proiect creat și deadline-ul lui.

## Pas 7: Home-ul clientului (`/portal/[workspaceId]`)

- „Welcome back, {prenume}. Here’s where things stand with {firmă}.”
- **„Waiting for you”**: livrabilele care cer aprobare, cu link direct la tabul Deliverables al proiectului. Răspunde la „trebuie să fac eu ceva?”.
- **Proiectele active**, cu progres și „Right now / Up next” (`summarize` din `lib/portal/summary.ts`).
- **Ultimele actualizări** de la toate proiectele firmei.
- Detaliile vin din `getPortalProject` pentru proiectele acelei firme, în paralel (un client are puține). De reconsiderat dacă ajung să fie multe.
- Test e2e în `client-portal.spec.ts`.

## Pas 8 (backend): aprobările în sumarul proiectului

- Adaug în `ProjectResponse` / sumarul din listă `approvals: { pending, changesRequested }`, calculate în repository dintr-o singură interogare. Respect regulile din `backend/CLAUDE.md`, plus teste de integrare.
- Frontend: blocul „Needs your attention” pe dashboard (modificări cerute primele) și un însemn în `ProjectsTable`.
- Verificare: `dotnet build` fără warning-uri și `dotnet test` verde.

## Fișiere atinse (reprezentative)

- Noi: `src/components/shell/*`, `src/components/projects/ProjectTabs.tsx` (sau un `UrlTabs` generic, refolosit la pașii 3–5), `src/app/w/[workspaceId]/layout.tsx`, `src/app/portal/layout.tsx`, `src/app/portal/[workspaceId]/**`.
- Modificate: toate paginile care folosesc `AppHeader` (`w/**`, `portal/**`, `account`, `workspaces`), `theme-provider.tsx`, `e2e/*.spec.ts`.
- Refolosite fără schimbări: `StageList`, `DeliverableList`, `UpdateList`, `FileList`, `ProjectForm`, `ArchiveProjectButton`, `ClientForm`, `ClientPortalActions`, `PortalApprovalPanel`, `PortalUpdateList`, `PortalFileList`, `ProjectProgress`, `summarize`, `formatDate`.
- Documentație, la fiecare pas: secțiunea Frontend din `CLAUDE.md` (shell, taburi, URL-urile portalului), o secțiune nouă „Interfață” în `decisions.md` și bifele din `product-plan.md`.

## Verificare (la fiecare pas)

- `pnpm lint` și `pnpm build` din `frontend/`.
- `pnpm test:e2e` (cere `docker compose up -d` în `backend/`: Postgres, Mailpit, RustFS). Spun explicit dacă nu a putut rula.
- Verificare manuală cu `pnpm dev`: dark și light, sidebar restrâns / deschis, mobil (sheet), tastatură (focus vizibil, taburile se schimbă cu săgețile), un cont care e și freelancer, și client (switcher-ul arată ambele grupuri).
- Pasul 8: și `dotnet build` / `dotnet test`.
- După fiecare pas propun un mesaj de commit; nu fac commit singur.
