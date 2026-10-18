# Client Portal — Plan de produs

> Document de referință pentru produs (scris în română, la fel ca planul original). Codul, identificatorii, comentariile și mesajele de commit rămân în engleză.
> Progresul implementării se bifează în secțiunea [Progres](#progres) de la final.

# Idee SaaS: Platformă pentru gestionarea proiectelor și colaborarea cu clienții

Construiește o platformă SaaS destinată freelancerilor și agențiilor mici, care să îi ajute să își gestioneze proiectele și să ofere clienților o experiență profesională, clară și transparentă pe tot parcursul proiectului.

Ideea principală este să facă colaborarea dintre freelancer și client mai organizată și cât mai automatizată, reducând nevoia de emailuri, mesaje pe WhatsApp, actualizări manuale și fișiere împrăștiate în mai multe locuri.

## Conceptul principal

Freelancerul își găsește clientul în afara platformei și apoi îl adaugă în aplicație. De acolo, poate gestiona relația cu clientul și proiectul dintr-un singur spațiu.

Clientul primește acces la un portal dedicat, unde poate vedea în orice moment informațiile relevante despre proiect.

Portalul clientului trebuie să permită vizualizarea:

* statusului actual și progresului general al proiectului;
* etapelor finalizate și următoarelor etape;
* timeline-ului proiectului;
* lucrurilor finalizate, aflate în lucru și care urmează;
* fișierelor, linkurilor, designurilor și altor livrabile;
* solicitărilor de feedback și modificări;
* aprobărilor necesare;
* conversațiilor și actualizărilor proiectului;
* deadline-urilor și acțiunilor pe care clientul trebuie să le realizeze;
* statusului plăților.

Principiul principal al produsului este:

**Freelancerul gestionează munca, iar clientul știe permanent ce se întâmplă cu proiectul.**

## Funcționalități principale

### Workspace și organizații

Freelancerii și agențiile pot crea workspace-uri, își pot organiza activitatea și pot adăuga membri în echipă.

### Gestionarea clienților

Platforma trebuie să permită gestionarea informațiilor despre clienți, istoricul colaborării și asocierea acestora cu proiecte, documente și plăți.

### Gestionarea proiectelor

Freelancerul poate gestiona proiecte, statusuri, etape, task-uri, deadline-uri, bugete, progres și activitatea proiectului.

### Portal pentru clienți

Fiecare client primește un spațiu dedicat în care poate vedea doar proiectele și informațiile la care are acces.

Portalul trebuie să fie mult mai simplu decât interfața freelancerului și să fie construit în jurul nevoilor clientului.

### Timeline-ul proiectului

Proiectul trebuie să aibă un timeline cronologic prin care clientul poate înțelege evoluția proiectului.

Exemple de evenimente:

* proiect creat;
* cerințe aprobate;
* design livrat;
* modificări solicitate;
* design aprobat;
* dezvoltare începută;
* versiune nouă livrată;
* proiect finalizat.

### Livrabile și aprobări

Freelancerul poate încărca sau adăuga livrabile, fișiere și linkuri.

Clientul poate:

* vizualiza livrabilul;
* oferi feedback;
* solicita modificări;
* aproba livrabilul.

Toate aprobările și modificările trebuie păstrate în istoricul proiectului.

### Propuneri și contracte

Freelancerul poate crea propuneri pentru clienți, cu servicii, descriere, preț, monedă, termeni și perioadă de valabilitate.

Clientul poate vizualiza și accepta sau respinge propunerea.

Ulterior, sistemul poate suporta și contracte și semnătură electronică prin servicii externe.

### Plăți

Platforma trebuie să permită integrarea unui sistem de plăți precum Stripe.

Clientul trebuie să poată vedea:

* suma totală;
* suma plătită;
* suma rămasă;
* istoricul plăților;
* plățile viitoare sau restante.

### Comunicare

Comunicarea legată de proiect trebuie să fie centralizată în aplicație.

Trebuie să existe:

* mesaje;
* comentarii;
* atașamente;
* mențiuni;
* notificări.

### Notificări

Utilizatorii trebuie să primească notificări pentru evenimente importante, precum:

* invitații;
* proiecte noi;
* mesaje;
* livrabile noi;
* feedback;
* aprobări;
* schimbări de status;
* deadline-uri;
* plăți;
* reminder-uri.

Notificările pot exista în aplicație și prin email.

### Automatizări

Automatizarea este una dintre funcționalitățile importante ale produsului.

Platforma trebuie să poată executa automat acțiuni atunci când apare un anumit eveniment.

Conceptul poate fi:

**Când se întâmplă X → execută Y.**

Exemple:

* clientul este adăugat → trimite automat invitația;
* este încărcat un livrabil → notifică clientul;
* clientul nu a aprobat un livrabil timp de câteva zile → trimite reminder;
* se apropie deadline-ul → notifică freelancerul;
* statusul proiectului se schimbă → notifică clientul;
* există o plată restantă → trimite reminder;
* un livrabil final este aprobat → marchează etapa ca finalizată.

Ulterior poate fi construit un sistem prin care utilizatorii își creează propriile automatizări.

### Actualizări săptămânale

Platforma poate genera actualizări periodice pentru client care să prezinte:

* ce s-a făcut;
* ce este în lucru;
* ce urmează;
* eventualele probleme sau întârzieri;
* ce trebuie să facă clientul.

Aceste actualizări pot fi generate sau asistate de AI.

### Inteligență artificială

AI-ul trebuie să fie un element secundar care îmbunătățește produsul, nu scopul principal al platformei.

Poate fi utilizat pentru:

* rezumarea progresului proiectului;
* generarea actualizărilor săptămânale;
* identificarea unor probleme sau întârzieri;
* generarea mesajelor către clienți;
* sumarizarea activității proiectului.

### Integrări externe

Platforma trebuie să poată integra servicii externe relevante, precum:

* Stripe pentru plăți;
* servicii de email;
* servicii de stocare a fișierelor;
* CRM-uri externe;
* GitHub;
* Figma;
* Google Calendar;
* servicii de semnătură electronică;
* servicii AI.

Integrările pot fi dezvoltate gradual.

### Integrare GitHub

Pentru proiectele software, platforma poate integra GitHub pentru a afișa informații relevante precum commit-uri, pull request-uri, release-uri și progres tehnic.

Aceste informații trebuie prezentate într-un mod simplu și ușor de înțeles pentru client.

### Integrare Figma

Pentru proiectele de design, platforma poate integra Figma pentru a permite partajarea designurilor, feedback-ul și aprobarea acestora.

### White-label

Pentru planurile plătite, freelancerii și agențiile pot personaliza portalul clientului cu:

* logo;
* numele companiei;
* culori;
* favicon;
* branding în emailuri.

Ulterior poate fi adăugat și suport pentru domenii personalizate.

### Istoric de activitate

Fiecare proiect trebuie să aibă un istoric al activității.

Acesta poate conține evenimente precum:

* proiect creat;
* status modificat;
* livrabil încărcat;
* feedback primit;
* modificare solicitată;
* livrabil aprobat;
* etapă finalizată;
* plată efectuată;
* mesaj trimis.

### Căutare și filtrare

Platforma trebuie să permită căutarea și filtrarea informațiilor importante, precum clienți, proiecte, task-uri, propuneri și livrabile.

### Setări

Trebuie să existe setări pentru:

* profilul utilizatorului;
* securitate;
* notificări;
* organizație;
* membri și roluri;
* branding;
* abonament;
* plăți;
* integrări;
* automatizări.

## Tipuri de utilizatori

Platforma trebuie să suporte freelanceri, proprietari de agenții, membri ai echipelor și clienți.

Un utilizator trebuie să poată avea un cont chiar dacă nu este încă freelancer sau client.

Aceeași persoană poate avea roluri diferite în contexte diferite. De exemplu, poate fi membru al unui workspace în care lucrează la proiecte și, în același timp, client într-un alt workspace.

Clienții trebuie să poată fi invitați prin email. Aceștia nu trebuie să caute freelancerul în platformă și nici să folosească platforma ca pe un marketplace.

## Experiența utilizatorului

Interfața freelancerului poate fi complexă și să ofere toate instrumentele necesare pentru gestionarea proiectelor.

Interfața clientului trebuie să fie mult mai simplă.

Când clientul intră în platformă, trebuie să poată înțelege rapid:

**Ce s-a făcut?
Unde este proiectul acum?
Ce urmează?
Există ceva ce trebuie să fac eu?**

Produsul trebuie să reducă la minimum necesitatea ca freelancerul să răspundă constant la întrebări precum:

„Cum mai merge proiectul?”
„Ai terminat designul?”
„Ce mai trebuie făcut?”
„Când este gata?”
„Mai trebuie să fac eu ceva?”

## Poziționarea produsului

Produsul nu trebuie să fie un marketplace pentru freelanceri.

Nu trebuie să fie nici un CRM enterprise și nici o simplă aplicație generică de project management.

Focusul principal trebuie să fie:

**Client Experience + Project Transparency + Automation.**

Platforma trebuie să creeze un spațiu central în care freelancerul gestionează proiectul, iar clientul are permanent o imagine clară asupra progresului, livrabilelor, aprobărilor, comunicării și plăților.

Scopul final este să creeze o experiență profesională pentru client, să reducă munca administrativă a freelancerului și să facă întregul proces de livrare a unui proiect mai transparent și mai automatizat.

## MVP

Prima versiune a produsului trebuie să se concentreze pe:

* autentificare;
* workspace;
* gestionarea clienților;
* invitații pentru clienți;
* proiecte;
* portal pentru clienți;
* timeline;
* livrabile;
* feedback;
* aprobări;
* notificări.

Funcționalități precum propuneri, plăți, automatizări avansate, CRM, AI, white-label și integrări suplimentare pot fi dezvoltate ulterior.

---

## Idei pentru mai târziu

Inspirate din [taskview-community](https://github.com/Gimanh/taskview-community) (task manager self-hosted; doar ideile, nu codul: licența interzice un produs comercial concurent). Nu fac parte din MVP; se reiau când ajunge rândul lor.

- **Jurnal automat de evenimente pe proiect** (livrabil partajat, status schimbat, etapă mutată...), nemodificabil, separat de actualizările scrise ale Timeline-ului. Discutat și amânat la Timeline: se reia când Aprobările sau Notificările au un motiv concret. Deciziile deja gândite: înregistrare explicită în servicii, în aceeași tranzacție; vizibilitatea pentru client fixată la scriere, după tip; clientul nu vede actorul; evenimentele „partajat" ale unui livrabil retras se ascund.
- **Șabloane de etape** („Create project from template", „Save these stages as a template"). Mic și util oricând: etapele sunt deja un agregat în `Project`, șablonul doar apelează `AddStage` de mai multe ori.
- **Tipuri TypeScript generate din OpenAPI**, în locul tipurilor scrise de mână în `frontend/src/lib/` (de ex. cu `openapi-typescript`). Previne desincronizarea dintre DTO-urile din .NET și frontend.
- **Un singur sistem de evenimente de domeniu** (livrabil partajat, feedback, aprobare, schimbare de status). Se construiește la Timeline; de el se leagă apoi Notificările, webhook-urile și Automatizările, în loc de patru mecanisme separate.
- **Webhook-uri semnate** (HMAC al corpului, cu un secret per workspace) și **API public cu tokenuri cu scope-uri limitate**, peste sistemul de evenimente. Baza pentru Automatizări.
- **Actualizări în timp real prin SSE**, odată cu Notificările (mai simplu decât WebSocket, ajunge pentru acest caz).
- **Time tracking facturabil / nefacturabil**, legat de Plăți (ore × tarif = suma de facturat).
- **Deploy: imagini Docker separate pentru API, web și migrări** (migrarea rulează o singură dată, înaintea API-ului), ca schema să nu se aplice de două instanțe deodată.
- **Permisiuni pe proiect** (un membru vede doar anumite proiecte), printr-o schimbare în `WorkspaceAccess`; azi toți membrii văd toți clienții.
- **MCP server** peste API-ul public, pentru partea de AI (de ex. „ce are de făcut clientul X?"). Cere întâi API public stabil și tokenuri.

### Dorite după MVP

Funcții pe care vrei să le ai pe viitor, în ordinea în care le-ai enumerat (ordinea de construcție se stabilește când ajunge rândul lor). Idei și referințe din [Clienter](https://clienter.co.in/), un produs apropiat.

- **Messages.** Există deja în plan la „Comunicare". De decis: un fir **per proiect** (planul inițial) sau **per client** (Clienter, mai simplu). Depinde de Notificări (mesaj nou → email / în aplicație).
- **Calendar / Meetings.** Sincronizare cu Google Calendar și linkuri Google Meet generate automat, plus reminder-uri. Deadline-urile proiectelor și ale etapelor pot apărea în calendar. Cere OAuth cu Google, deci vine odată cu primele integrări.
- **Kanban.** Etapele au deja statusurile To do / In progress / Done, deci o vedere Kanban **pe etape** e aproape gratuită (alt mod de afișare al acelorași date). Kanban pe **task-uri** cere întâi task-uri, care sunt excluse din MVP.
- **CRM.** Rămâne subțire, ca produsul să nu devină un CRM generic (vezi „Poziționarea produsului"): lead-uri cu etape personalizate și sursă, iar la câștigare se transformă în `Client`, cu istoricul păstrat. Fișa `Client` există deja și rămâne punctul de legătură.
- **Recenzii verificate.** La trecerea proiectului în Completed, clientul primește automat o invitație să lase o recenzie. Recenzia e legată de contul lui din portal (nu poate fi falsificată) și nu se editează sau șterge după publicare. Freelancerul primește o pagină publică cu recenzii și un badge de inserat în site. De decis: cine aprobă afișarea publică și dacă clientul poate retrage recenzia. Depinde de Notificări / email și de statusul Completed (există deja).

### Inspirație de layout (Clienter)

Din capturile de ecran ale [Clienter](https://clienter.co.in/) (demo). **Doar inspirație**, nu de copiat 1:1; rămânem la poziționarea noastră (transparență pentru client, fără banii în centru).

**Partea freelancerului (pagina proiectului)**
- Pagina proiectului pe **tab-uri** (Overview, Deliverables, Timeline, ...), nu secțiuni una sub alta, când apar Timeline, Feedback și Aprobări. shadcn are `Tabs`.
- **Overview** cu un card „Progress" (lista de actualizări + formular „Add update": titlu și „ce se întâmplă acum"), un card „Deadline" (zile rămase, bară de progres) și un card „Approvals" (câmp „ce are nevoie de aprobare" + buton „Request approval", apoi un jurnal de tipul „X approved by Ananya · 13 Jul").
- **Actualizările din Timeline pot fi scrise de mână**, nu doar evenimente automate, fiecare cu `Draft` / `Shared` (insigna „Shared", ochiul de arătat/ascuns).
- **Vizibilitate controlată pe element**, același tipar ca `Draft` / `Shared` de la livrabile.
- **Livrabile și fișiere în același tab**: livrabilele (cu insigna Shared) lângă o zonă de upload cu drag & drop. Reper pentru decizia de stocare: limită de 25 MB per fișier.

**Partea clientului (portalul)**
- **Prima pagină a portalului** cu „Welcome back, X. Here's where things stand today." și cartonașe: proiecte active, **„Awaiting you"** (câte lucruri așteaptă o acțiune de la client; răspunde la „trebuie să fac eu ceva?"), mesaje necitite.
- Sub ele, **proiectele active** cu bară de progres și un indicator scurt („On track"), apoi un rezumat „Your delivery" (de ex. livrabile primite, „typical reply time").
- Pagina unui proiect pe tab-uri (Overview, Deliverables, Files & docs), cu titlul, statusul și progresul sus.
- Navigarea portalului rămâne **mică** (Home, Projects, apoi Messages când există), spre deosebire de cele 8 intrări din meniul lor.
- **„Powered by <produs>"** în subsolul portalului pe planul gratuit; dispare pe planul plătit (se leagă de white-label).

**De nu luat**
- Cartonașe și tab-uri cu bani (buget, plătit, rămas, facturi, plăți către echipă) până la pasul Plăți.
- Pagina freelancerului încărcată (6 tab-uri, meniu cu 13 intrări).
- „Timeline" ca simplă bară de date: la noi rămân etapele și evenimentele.

---

## Progres

Se actualizează după fiecare pas încheiat. Ordinea pașilor se stabilește împreună, pe parcurs (vezi regulile din [CLAUDE.md](../../CLAUDE.md)).

- [x] Autentificare
- [x] Workspace
- [x] Gestionarea clienților
- [x] Profil cont (nume, telefon, companie), înaintea invitațiilor
- [x] Invitații pentru clienți
- [x] Proiecte, în trei pași:
  - [x] Pas 1: proiecte pentru freelancer (API, listă, pagina proiectului, secțiune pe pagina clientului, arhivare)
  - [x] Pas 2: etape (ordonate, cu status) și progres
  - [x] Pas 3: proiectele și etapele în portalul clientului
- [x] Livrabile ca link, în doi pași:
  - [x] Pas 1: livrabile ca link pentru freelancer (API și secțiunea „Deliverables” pe pagina proiectului)
  - [x] Pas 2: livrabilele în portalul clientului
- [ ] Timeline (actualizări scrise de freelancer, cu Draft / Shared), în doi pași:
  - [ ] Pas 1: actualizări pentru freelancer (API și secțiunea „Updates” pe pagina proiectului)
  - [ ] Pas 2: actualizările partajate în portalul clientului
- [ ] Feedback și aprobări, într-un singur pas, pe livrabil: clientul aprobă sau cere modificări, cu un mesaj. Aici se încheie și portalul pentru clienți (acum e doar de citit: proiecte, etape, progres, livrabile partajate)
- [ ] Notificări (întâi email, apoi în aplicație)
- [ ] Livrabile, pasul 3: upload de fișiere (decizie de stocare), ultimul din MVP

## Decizii luate

Deciziile de arhitectură / model de date, cu motivul lor, sunt în [decisions.md](decisions.md), pe secțiuni (Autentificare, Workspace, Clienți, Profil cont, Invitații, Proiecte, Livrabile, Timeline, Generale). Se citește doar secțiunea relevantă pentru pasul curent.
