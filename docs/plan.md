# Plan produs: Client Collaboration & Project Delivery Platform

## Produsul

Portal de livrare a proiectelor pentru freelanceri și agenții mici de web/software. Nu este marketplace, CRM generic sau tool de project management generic.

Clientul are o experiență extrem de simplă. Freelancerul are o interfață de management mai complexă.

La intrarea în portal, clientul trebuie să poată răspunde în 5 secunde la:

1. Ce s-a făcut?
2. Unde este proiectul acum?
3. Ce urmează?
4. Ce trebuie să fac eu?

Diferențiatori, în ordinea priorității: portal foarte simplu, livrabile cu versiuni și aprobări, timeline generat automat, update-uri GitHub traduse pentru client (Faza 2), facturare locală SmartBill / e-Factura (Faza 3), white label.

## Modelul de utilizatori și acces

| Entitate | Ce reprezintă |
| --- | --- |
| User | Persoană cu email, fără rol global |
| Workspace | Organizația freelancerului sau agenției |
| WorkspaceMember | Relația User și Workspace, cu rol: Owner, Admin, Member |
| Client | Firma sau persoana client, în interiorul unui Workspace |
| ClientContact | O persoană de la client, legată opțional de un User |
| ProjectAccess | Ce proiecte vede un ClientContact |

- Același User poate fi Owner în propriul workspace și ClientContact în alt workspace.
- Un client poate avea mai multe persoane de contact, fiecare cu acces doar la proiectele lui.
- În MVP există doar rolul Owner în UI, dar modelul suportă toate rolurile.

## Onboarding

- Freelancer nou: creează workspace, adaugă primul client, creează primul proiect. Un proiect demo precompletat arată cum vede clientul portalul.
- Client invitat: primește un magic link și intră direct în portal.

## Model de date (MVP)

- Project: nume, descriere, client, dată start, deadline, status, buget intern.
- Milestone: nume, dată estimată, status (NotStarted, InProgress, Done), ordine.
- Deliverable: nume, milestone, status.
- DeliverableVersion: număr versiune, fișiere, linkuri, preview URL opțional, notă, dată.
- Review: decizie (Approved sau ChangesRequested), feedback, autor, dată, versiune.
- Message: fir per proiect, cu atașamente.
- Comment: pe o versiune de livrabil, cu flag isInternal.
- ProjectEvent: tip, actor, proiect, payload, visibility (Internal sau Client), dată. Imutabil.
- Notification și NotificationPreference.
- WorkspaceBranding: logo, culoare principală.

## Statusuri de proiect

| Status | Cum se setează |
| --- | --- |
| Draft | Manual, proiect încă nevăzut de client |
| Planning | Manual |
| InProgress | Manual sau automat la primul milestone început |
| WaitingForClient | Automat cât timp există aprobări în așteptare |
| Review | Automat când o versiune e trimisă spre aprobare |
| Completed | Automat după aprobarea finală, sau manual |
| Cancelled | Manual |

Statusurile automate pot fi suprascrise manual.

Progresul se calculează din milestones finalizate (ex. 3 din 5). Un milestone se închide automat când ultimul lui livrabil e aprobat.

## Livrabile și aprobări

1. Freelancerul încarcă o versiune și o trimite spre review.
2. Clientul primește notificare și vede livrabilul în cardul „Ai de făcut”.
3. Clientul alege Approve sau Request Changes. La Request Changes, feedback-ul e obligatoriu.
4. La Request Changes, freelancerul încarcă o versiune nouă și ciclul se repetă.
5. La Approve, versiunea devine finală și nu mai poate fi modificată.

Tipuri de conținut: fișiere (documente, imagini, PDF, arhive), linkuri externe, website preview URL.

## Fluxul de evenimente

Fiecare acțiune importantă scrie un ProjectEvent. Din același flux se construiesc:

- Activity History pentru freelancer (toate evenimentele).
- Project Timeline pentru client (doar visibility Client, formulate pe limba clientului).
- Notificările în aplicație și pe email.
- Ulterior: automatizările și contextul pentru AI.

Evenimente de bază: project.created, project.status_changed, milestone.completed, deliverable.uploaded, deliverable.approved, deliverable.changes_requested, message.created, update.published.

Freelancerul poate adăuga evenimente manuale în timeline (ex. „Meeting de kick-off”).

## Client Portal

- Acces prin magic link, fără parolă. Contul se creează automat la primul click. Sesiune lungă (ex. 30 de zile).
- Ecranul proiectului, în ordine: cardul „Ai de făcut” (sau „Nu ai nimic de făcut acum”), progres pe milestones, ultimul update, timeline scurt, tab-uri Livrabile și Mesaje.
- Clientul nu vede: task-uri, note interne, buget intern, comentarii interne, evenimente interne.
- Mobile-first.
- Folosește logo-ul și culoarea freelancerului.

## Comunicare și notificări

- Un fir de mesaje per proiect, cu atașamente.
- Comentarii pe fiecare versiune de livrabil, inclusiv interne (doar echipa).
- Notificări în aplicație și pe email, generate din ProjectEvent.
- Acțiunile urgente pleacă imediat; restul se grupează într-un digest zilnic pentru client.
- Fiecare email are un buton clar spre acțiune.
- Utilizatorul alege categoriile de notificări primite.
- Ulterior: reply-by-email, mențiuni, actualizare în timp real.

## Securitate și multi-tenancy

- Izolare centrală pe workspace_id (EF Core global query filters sau PostgreSQL RLS).
- Accesul clientului verificat separat prin ProjectAccess.
- Teste automate de izolare între workspace-uri și între proiecte.
- Freelanceri: email și parolă cu verificare, resetare parolă, Google OAuth.
- Magic links: unică folosință, expirare scurtă, stocate hash-uit.
- Sesiuni în cookie-uri httpOnly, Secure, SameSite. Rate limiting pe login și magic links.
- Fișiere în bucket privat, doar prin presigned URLs.
- ProjectEvent servește și ca audit log.
- GDPR: export și ștergere de date.

## Pagini MVP

- Public: Landing, Login, Register, Forgot Password, Terms, Privacy.
- Freelancer: Dashboard, Clients, Client Details, Projects, Project Details (Overview, Deliverables, Messages, Activity), Settings (profil, workspace, branding, notificări).
- Client: lista de proiecte (sau direct proiectul, dacă are unul singur), pagina proiectului, preferințe de notificări.
- Invitații: intrare din magic link.

Dashboard-ul freelancerului răspunde la: ce așteaptă clienții de la mine, ce aștept eu de la clienți, ce deadline-uri vin săptămâna aceasta.

## Design

- Interfață curată, modernă, fără aspect de template generic.
- Acțiunea principală a fiecărui ecran se vede imediat.
- Empty states bine gândite pentru fiecare listă.
- Contrast bun, focus vizibil, navigare din tastatură.

## Roadmap

**Faza 1: MVP.** Auth, workspace, clienți, invitații, proiecte, milestones, livrabile, aprobări, timeline, mesaje, notificări, portal, branding de bază.

**Poartă: validare.** Cel puțin 3 freelanceri folosesc MVP-ul pe proiecte reale, cu clienți reali.

**Faza 2.** Oferte cu plan de plăți (acceptarea creează proiectul), Stripe Connect pentru plăți de la clienți, Stripe Billing pentru planuri, automatizări fixe și remindere, integrare GitHub, AI pentru draft de weekly update.

**Faza 3.** SmartBill / e-Factura, planul Team (membri, roluri, task-uri interne), Google Calendar, white label complet, custom domain.

**Faza 4.** Figma și nișa de design, Slack / Teams, CRM extern (HubSpot), semnătură electronică, automation builder.

## Planuri SaaS (după MVP)

| Plan | Include |
| --- | --- |
| Free | 1–2 proiecte active, logo, „Powered by” vizibil |
| Pro | Proiecte nelimitate, plăți, automatizări, AI, fără „Powered by” |
| Team | Tot din Pro, membri și roluri, custom domain |