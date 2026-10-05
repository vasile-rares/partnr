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

## Progres

Se actualizează după fiecare pas încheiat. Ordinea pașilor se stabilește împreună, pe parcurs (vezi regulile din [CLAUDE.md](../../CLAUDE.md)).

- [x] Autentificare
- [x] Workspace
- [ ] Gestionarea clienților
- [ ] Invitații pentru clienți
- [ ] Proiecte
- [ ] Portal pentru clienți
- [ ] Timeline
- [ ] Livrabile
- [ ] Feedback
- [ ] Aprobări
- [ ] Notificări

## Decizii luate

Aici se notează deciziile de arhitectură / model de date luate pe parcurs (cu motivul), ca să nu fie rediscutate în fiecare chat.

### Autentificare

- **Doar identitate acum, rolurile la Workspace.** Aceeași persoană poate fi membru într-un workspace și client în altul, deci rolurile aparțin unui workspace, nu contului. De aceea nu există roluri globale.
- **ASP.NET Core Identity** (hashing, lockout, token-uri gata făcute), în Infrastructure, în spatele `IAuthService`. Nu ne scriem singuri partea sensibilă de securitate.
- **Cookie HttpOnly, nu JWT în localStorage.** JavaScript-ul nu poate citi cookie-ul, deci un atac XSS nu poate fura sesiunea. Sesiune de 14 zile, sliding.
- **Tot API-ul cere login implicit** (fallback policy); excepțiile se marchează `[AllowAnonymous]`. Dacă uităm un atribut, endpoint-ul rămâne închis, nu deschis.
- **Parolă:** minim 8 caractere, fără reguli de compoziție. Lockout: 5 încercări greșite → 15 minute. Eșecurile de login au mereu același mesaj, ca să nu se poată afla ce emailuri există.
- **Register cere email, parolă și nume complet** (numele apare în portalul clientului).
- **Confirmarea emailului și resetarea parolei sunt amânate** până apare serviciul de email (pasul Invitații).
- **`Client` rămâne o fișă de contact**, creată de freelancer. Legătura cu un cont (`User`) se face la Invitații.
- **Frontend: browserul apelează API-ul doar prin Next** (rewrite `/api/*`), ca să nu existe CORS și cookie-ul să aparțină originii aplicației; Server Components apelează API-ul direct, cu cookie-ul trimis mai departe.
- **Frontend: protejarea paginilor.** `proxy.ts` face doar un redirect rapid dacă lipsește cookie-ul. Verificarea reală (`requireUser()`, apel la `/me`) se face în fiecare pagină protejată, nu în layout, conform ghidului Next.js 16 (layout-urile nu se re-randează la navigare). Proxy-ul nu redirectează niciodată dinspre `/login` doar după cookie, ca un cookie expirat să nu producă o buclă.
- **UI cu shadcn/ui** (ales după auth și workspace, cât codul era încă mic): aspectul presetului implicit `base-nova` (neutru, font Geist), pe primitive **Base UI** (alegerea implicită din shadcn v4, întreținută activ). Am renunțat la direcția vizuală proprie de la început; doar cele trei culori de status (terminat / în lucru / urmează) rămân token-uri proprii, pentru timeline. Formularele rămân pe `useActionState` + validare nativă, fără react-hook-form/zod, ca regulile să nu fie dublate față de API. Mod întunecat cu buton (Light / Dark / System), prin `next-themes`.
- **Frontend: textele interfeței sunt în engleză**, fără i18n deocamdată.
- **Teste E2E cu Playwright, pe o bază separată** (`client_portal_e2e`, pe același Postgres local, recreată la fiecare rulare). Browserul, Next, API-ul și baza se testează împreună, ceea ce testele unitare și de integrare nu acoperă. Baza de dezvoltare nu se atinge: scriptul refuză orice bază al cărei nume nu se termină în `_e2e`. SQLite a fost respins pentru dev și test: codul se bazează pe Postgres (erori `23505`, tipuri și migrări Npgsql), iar producția va fi tot Postgres, deci rulăm pe același motor peste tot.
### Workspace

- **Workspace-ul se creează explicit**, după register (pagina „Create your workspace”), nu automat. Un client invitat nu trebuie să ajungă cu un workspace gol. Un utilizator poate avea mai multe workspace-uri.
- **Roluri: `Owner` și `Member`**, ținute pe apartenența la workspace (`WorkspaceMember`), nu pe cont. Ownerul gestionează workspace-ul; membrii lucrează la clienți și proiecte.
- **Clienții nu sunt membri.** Accesul lor la portal vine prin invitație, limitat la proiectele lor (pasul Invitații). Dacă ar fi membri cu rol, o verificare uitată le-ar arăta datele tuturor clienților.
- **Membrii noi se adaugă abia prin invitații**; tabela de membri și rolurile există deja, ca autorizarea să fie corectă de la început.
- **Workspace-ul e în rută** (`/api/workspaces/{id}/...`). Un non-membru primește **404**, la fel ca pentru un workspace inexistent, ca id-urile să nu poată fi ghicite. Un membru fără drept (de ex. redenumire) primește **403**.
- **Regula de acces stă într-un singur loc** (`WorkspaceAccess`), prin care trec toate serviciile cu date dintr-un workspace.
- **`Client` aparține unui workspace**; emailul e unic per workspace, nu global (aceeași persoană poate fi client la doi freelanceri). Asta a închis și o scurgere: înainte, orice utilizator logat putea citi orice client după id.
- **Ștergerea workspace-ului e amânată** până există proiecte și invitații, ca să știm ce înseamnă „șterge”. Până atunci FK-ul clienților către workspace e `Restrict`, ca nimic să nu se șteargă în cascadă din greșeală.
- **Frontend:** `/` doar redirecționează (0 → creare, 1 → direct în workspace, mai multe → listă). URL-urile folosesc id-ul (`/w/{id}`), fără slug.

### Generale

- **Backend organizat pe roluri, nu pe feature-uri** (`Domain/Entities`, `Application/Interfaces/Repositories|Services`, `Application/Services`, `Application/DTOs/<Feature>`, `Infrastructure/Authentication`). Folderele se creează doar când apare primul fișier. Regulile de dependență dintre straturi sunt verificate de `ClientPortal.ArchitectureTests` (NetArchTest), nu doar scrise în documentație. Validarea rămâne pe data annotations.

- **Limitare cunoscută:** logout-ul șterge cookie-ul din browser, dar un cookie copiat înainte rămâne valid până expiră (cookie Identity fără stare pe server). Se rezolvă cu invalidarea `SecurityStamp` la logout; de decis la pasul de securitate / înainte de producție.
