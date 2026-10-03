# Client Collaboration & Project Delivery Platform

SaaS pentru freelanceri și agenții mici de web/software: un portal în care clientul vede progresul proiectului, primește livrabile, le aprobă sau cere modificări, iar tot istoricul rămâne într-un singur loc.

Principiu: The freelancer manages the work. The client always knows what is happening.

Planul complet al produsului: `docs/plan.md`. Citește-l când lucrezi la o funcționalitate nouă.
Progresul și deciziile luate: `docs/progress.md`. Citește-l la începutul fiecărei sesiuni și actualizează-l la finalul fiecărui pas.

## Stack

- Backend: ASP.NET Core, Web API, EF Core, PostgreSQL
- Frontend: Next.js (App Router), TypeScript, Tailwind CSS
- Fișiere: S3 / Cloudflare R2, upload prin presigned URLs
- Email: Resend sau Postmark
- Background jobs: Hangfire
- Teste: xUnit (backend), Playwright (end-to-end)
- Local: Docker Compose

## Comenzi

<!-- Completează după setup -->
- Pornire servicii: `docker compose up -d`
- Backend: ...
- Frontend: ...
- Migrații: ...
- Teste: ...

## Reguli care nu se încalcă

- Fiecare entitate legată de un tenant are `workspace_id`. Izolarea se face central (EF Core global query filters sau PostgreSQL RLS), niciodată doar prin filtre scrise manual în endpoint.
- Accesul clientului se verifică prin `ProjectAccess`. Clientul nu vede niciodată: buget intern, comentarii interne, evenimente cu visibility `Internal`.
- Un utilizator nu are tip global. Permisiunile vin din relații: `WorkspaceMember` (Owner, Admin, Member) și `ClientContact` + `ProjectAccess`.
- `ProjectEvent` este imutabil: nu se modifică și nu se șterge. Timeline-ul, activity history și notificările se construiesc din el.
- O versiune de livrabil aprobată nu mai poate fi modificată. Aprobarea salvează cine, când și pe ce versiune.
- Request Changes cere feedback obligatoriu.
- Statusurile `WaitingForClient` și `Review` și progresul proiectului se derivă automat, nu se introduc manual.
- Fișierele sunt în bucket privat, accesibile doar prin presigned URLs cu expirare.
- Magic link: token de unică folosință, expirare scurtă, stocat hash-uit.

## Convenții de cod

- TypeScript strict, fără `any`.
- Validare la intrare pe toate endpoint-urile.
- Orice funcționalitate care atinge date de tenant are test de izolare (acces din alt workspace trebuie să eșueze).

## Scope

Lucrăm la MVP. Nu implementa fără să întrebi: task-uri, oferte, contracte, plăți, Stripe, automatizări configurabile, AI, roluri Admin/Member în UI, planuri SaaS, integrări (GitHub, Figma, Calendar, CRM, facturare), chat în timp real.

## Mod de lucru

- Un pas pe rând. La final: ce ai făcut, cum testez, ce urmează.
- Când o cerință e ambiguă, pune o singură întrebare și propune o variantă implicită.
- Dacă vezi o problemă în plan, spune-o direct.