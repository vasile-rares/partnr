# Plan: separarea în microservicii

Stare: **în discuție** (început 2026-11-17). Nimic din acest plan nu e implementat; backend-ul e încă monolitul descris în `backend/CLAUDE.md`. Documentul se completează pe măsură ce deciziile se închid; când un pas se face, se bifează aici, iar regulile rămase valabile se mută în `backend/CLAUDE.md` și `decisions.md`.

## Decis

- **Scop: ce e mai bun pentru aplicație**, ca la o aplicație de industrie (nu învățare / CV; răspuns dat 2026-11-17, după ce inițial s-a ales „ambele"). Consecință: un serviciu separat se justifică printr-o forță concretă (încărcare sau scalare diferită, izolarea unei dependențe externe, ciclu de deploy propriu), nu prin „un serviciu per feature". Ce e strâns legat prin reguli de acces rămâne împreună.
- **Gateway YARP** ca singur punct de intrare: browserul rămâne pe REST/JSON, cookie-ul rămâne pe originea frontend-ului, frontend-ul se schimbă doar prin `BACKEND_URL`. gRPC doar între servicii.
- **Autentificare: gateway-ul validează cookie-ul** (la serviciul care deține conturile, prin gRPC) și trimite serviciilor un **JWT intern scurt** cu contul. Browserul nu vede JWT; serviciile au încredere în gateway și în cheia lui.
- **Service registry: Consul** (health checks incluse). Echivalentul Eureka în .NET, fără Java în stack. Serviciile se înregistrează la pornire și se găsesc după nume.
- **Servicii-țintă** (decis 2026-11-17, ținând cont de planul de produs: Stripe, integrări externe, automatizări, webhook-uri):
  - **Core**: conturi, workspace-uri, clienți, invitații, proiecte, livrabile, actualizări, fișiere, mesaje, întâlniri (ca date). Aici stau regulile de acces strâns legate; nu se rupe mai departe fără o forță concretă.
  - **Notifications**: inbox, emailuri, push live.
  - **Integrations**: toate conexiunile externe (OAuth, tokenuri criptate, clienți de provider, workeri de sincronizare), nu doar Google; întâlnirile se sincronizează pe baza evenimentelor, datele rămân în Core.
  - **Billing**: se construiește direct ca serviciu când se ajunge la pasul Plăți (Stripe, webhook-uri cu idempotență, date financiare izolate). Nu se extrage nimic, nu există încă.
  - **Automatizări / webhook-uri**: mai târziu, ca ascultători de evenimente, peste sistemul de evenimente din planul de produs.
- **Baza de date proprie per serviciu** (instanță Postgres comună în dev), fără join-uri între servicii; datele altui serviciu vin prin gRPC sau evenimente. Migrările fiecărui serviciu sunt independente.
- **Message broker: RabbitMQ**, necesar (nu „dacă e nevoie"): bus-ul de evenimente de domeniu din planul de produs. Core scrie evenimentul în aceeași tranzacție cu schimbarea (tabel outbox) și un worker îl publică; asta înlocuiește `INotificationWriter`, care scrie rânduri în aceeași tranzacție și nu poate trece de granița unui serviciu. Se introduce odată cu primul serviciu extras.
- **gRPC pentru comunicarea între servicii.**
- **Rulare locală: `dotnet run` per serviciu** (compound launch în VS Code, debugger în fiecare), infrastructura în `docker-compose` (Postgres, Mailpit, RustFS, Consul, eventual RabbitMQ). Un compose complet cu serviciile se adaugă mai târziu, pentru e2e.
- **Strategie incrementală (strangler)**: monolitul rămâne funcțional, se scoate un serviciu pe rând, testele rămân verzi la fiecare pas. Nu se face o rescriere completă.
- **Ordine** (decis 2026-11-17): pasul 0 = gateway YARP + Consul în fața monolitului, fără nicio separare; apoi **Integrations** (cuplaj mic, dovedește tot lanțul outbox + RabbitMQ + un serviciu în Consul); apoi **Notifications** (atinge aproape toate use-case-urile, deci după ce bus-ul e dovedit).
- **Structură în repo**: un monorepo, un singur `Partnr.slnx`. Proiectele actuale rămân **Core** (fără redenumire). Fiecare serviciu nou are folderul lui cu aceleași 4 straturi și teste de arhitectură. `Partnr.Contracts` partajat (doar `.proto` și tipuri de evenimente, fără logică) și `Partnr.Gateway` separat.
- **Sesiune**: gateway-ul întreabă Core prin gRPC la **fiecare cerere**, fără cache (același comportament ca azi: un cont șters sau o deconectare au efect imediat, vezi `backend/CLAUDE.md`, Authentication). Se măsoară și se relaxează doar dacă se simte.
- **Date între servicii**: munca în fundal (notificări, sincronizare) primește prin eveniment tot ce-i trebuie (destinatari, titlul întâlnirii etc.), ca serviciul care consumă să nu depindă de disponibilitatea lui Core; gRPC doar când utilizatorul așteaptă un răspuns sincron.
- **Bus și outbox: Wolverine** (MIT, outbox cu EF Core în aceeași tranzacție, RabbitMQ, .NET 10). Verificat 2026-11-17: MassTransit v9 e comercial (v8 doar patch-uri de securitate), de aceea nu e ales; CAP e alternativa dacă Wolverine nu ține.
- **Teste**: fiecare serviciu își are testele (unit, integrare cu Testcontainers, inclusiv RabbitMQ); teste de contract pentru `.proto` și evenimentele din `Partnr.Contracts`; Playwright pornește gateway-ul și toate serviciile pe porturi proprii, în baze `_e2e` (regula `start-api.mjs` rămâne valabilă pentru fiecare serviciu).
- **Operare**: tracing și loguri cu OpenTelemetry (id de corelație de la gateway prin gRPC și evenimente); deadline, retry (doar pe operații idempotente) și circuit breaker pe apelurile gRPC; health check minim (`/health`) pe fiecare serviciu, pentru că Consul scoate din rutare doar instanțele care îl au.
- Țintă de producție: **nedecisă** (K8s, VPS etc.). Influențează doar rolul Consul-ului (în K8s DNS-ul îl poate înlocui).

- **Reguli de acces**: `WorkspaceAccess` / `ProjectAccess` / `ClientAccess` (inclusiv regula scrisă dublu, în C# și în SQL `SharedWithClient`) rămân în Core. Rutele din Integrations și Notifications se autorizează doar după contul din JWT (conexiunea Google a contului, inbox-ul contului), deci nicio regulă de workspace nu trece granița (decis 2026-11-18).
- **Notificări**: `NotificationWriter` se rupe în două. Core decide destinatarii (`IsOpenToClient`, `RoleOf`, numele contului) și publică prin outbox evenimentul cu destinatarul în payload; Notifications doar stochează, trimite emailul și face push. Retragerea unei cereri de aprobare (`ApprovalRequestWithdrawn`) e tot eveniment, iar Notifications marchează notificarea ca învechită după `approvalRequestId`. `InboxPushInterceptor` se mută întreg în Notifications, neschimbat. Inbox-ul devine eventual consistent (decis 2026-11-18).
- **Întâlniri și Google**: la creare, Core întreabă Integrations prin gRPC dacă organizatorul are conexiunea `Connected` (409 „Connect your Google account first"; 503 dacă Integrations e jos). Coada de sincronizare trece în Integrations: Core publică `MeetingChanged` cu tot ce trebuie (titlu, ore, emailul clientului, organizator, versiune), Integrations își ține coada (worker, claim `SKIP LOCKED`, backoff) și întoarce rezultatul ca eveniment. Core păstrează doar starea afișată (`SyncStatus`, `MeetUrl`). Deconectarea și reconectarea Google devin evenimente din Integrations către Core, care parchează / repune în coadă întâlnirile (decis 2026-11-18).

- **Seed de dezvoltare**: fiecare serviciu își seed-uiește propria bază, iar amprenta de reseed rămâne per serviciu. Core își seed-uiește datele și publică evenimentele prin outbox, deci notificările din seed ajung la Notifications pe lanțul real, nu se scriu direct. Integrations are seed propriu (conexiune fictivă, `FakeGoogle`). Rămâne de verificat când se face pasul 2: ordinea pornirii și reseed-ul când se șterge doar baza unui serviciu (decis 2026-11-18).
- **Cheia tokenurilor Google**: aceeași cheie (`SecretProtector`, AES-GCM), mutată în configurarea Integrations și scoasă din Core, care nu mai poate citi tokenuri. Tokenurile existente se copiază criptate, fără re-criptare, deci migrarea tabelei `google_connections` e o copiere (decis 2026-11-18).
- **Tabela `meetings` din Core după pasul 2**: rămân doar `SyncStatus`, `MeetUrl` și `Version` (token de concurență, vine și în `MeetingChanged`). Se mută în Integrations `SyncAttempts`, `NextSyncAtUtc` și regulile `RecordSynced` / `RecordSyncFailure` / `RetrySync`; Core aplică doar rezultatul primit ca eveniment. `GoogleEventId` nu se mută: e derivat din id-ul întâlnirii (`Meeting.EventId`). `POST …/retry-sync` rămâne în Core ca rută și trimite un eveniment către Integrations (decis 2026-11-18).

## De decis (următoarele runde)

Nimic deschis pentru pașii 0–3; se detaliază la începutul fiecărui pas. De reluat la pasul 2: migrarea datelor existente din baza lui Core în baza lui Integrations (ordinea: copiere, apoi golirea coloanelor mutate dintr-o migrare Core separată).

## Pași

Schiță, fiecare pas se face separat, cu `dotnet build` fără warning-uri, `dotnet test` verde și e2e verde înainte de următorul. Se detaliază la începutul fiecărui pas.

- [ ] **0. Gateway + Consul în fața monolitului.** `Partnr.Gateway` (YARP), Consul în `docker-compose`, monolitul se înregistrează; sesiunea validată de gateway (gRPC către Core la fiecare cerere) și JWT intern; `BACKEND_URL` arată spre gateway; harness-ul e2e pornește și gateway-ul. Niciun serviciu nu se separă încă.
- [ ] **1. Fundația evenimentelor.** `Partnr.Contracts`, RabbitMQ în `docker-compose`, Wolverine cu outbox în Core (tabel în baza lui Core), un prim eveniment publicat și un consumator de probă.
- [ ] **2. Integrations.** Serviciu nou cu baza lui: conexiunea Google (rutele `/api/integrations/google`, inclusiv callback-ul OAuth prin gateway), tokenurile criptate, `MeetingSyncWorker`; întâlnirile ajung la el prin evenimente cu tot ce-i trebuie, iar rezultatul sincronizării se întoarce în Core tot prin eveniment. Datele se mută din baza lui Core. Documentația din `google-calendar.md` și `meetings.md` se actualizează.
- [ ] **3. Notifications.** Serviciu nou cu baza lui: inbox, worker de emailuri, SignalR (hub-ul trece prin gateway, WebSockets). `INotificationWriter` din Core devine evenimente prin outbox; destinatarii vin în payload.
- [ ] **4. Billing.** La pasul Plăți din planul de produs, construit direct ca serviciu.

Neacoperit și de reluat când apare: țintă de producție (deploy, rolul Consul-ului față de DNS-ul K8s), automatizări și webhook-uri.
