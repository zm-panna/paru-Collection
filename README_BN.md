# October 2026 update
নতুন পাঁচটি requirement-এর setup ও verification status: docs/UPDATE_SETUP_BN.md। এই update-এর build/live acceptance এখনও execute হয়নি।

# Paru Collection — AI E-commerce + ERP

ASP.NET Core 10 MVC + Razor + EF Core 10 + SQL Server 2022 + Identity/RBAC + Ollama।

**আগের বাকি development তালিকার guest checkout, store/split stock, supplier invoice, SKU exchange, campaign quotas ও advanced AI/report workflow যোগ করা হয়েছে। বাস্তব merchant/IIS acceptance বাকি; production-certified দাবি করা হচ্ছে না।** সুনির্দিষ্ট coverage: `docs/FEATURE_COVERAGE.md`। নতুন solution; আপনার পুরোনো IMS/SSLCommerz source পরিবর্তন করা হয়নি।

## কী চালানো যাবে

- Responsive storefront, category/product search, filters, product details ও local demo illustrations।
- Customer login/register, password change, SMTP password reset, address book, wishlist, support, moderated reviews।
- Guest/account cart, guest checkout, login merge, checkout review, server-side pricing/VAT/coupon/shipping, order tracking ও invoice।
- COD collection; SSLCommerz session + server verification + duplicate callback protection।
- Admin dashboard, database-driven menu, roles, permission grants এবং user deny override।
- Master CRUD, product variants, image upload, multi-warehouse stock, reserve/release/issue, transfer ও adjustments।
- Requisition → quotation comparison → purchase approval → partial goods receive → matched supplier invoice/approval → supplier payment/return; barcode cash POS।
- Return request → inspection → optional restock → verified SSLCommerz refund / manual refund / store credit।
- Moving-average stock cost, balanced journals, customer/supplier account entries, expense approval/payment।
- Reports: CSV, real `.xlsx`, server PDF ও browser Print / Save PDF; trial balance, P&L, balance sheet ও KPI।
- Database facts-ভিত্তিক local Ollama shopping/admin assistant।

## নতুন workflow

`docs/ADVANCED_WORKFLOWS_BN.md`-এ guest checkout, stock split, invoice, campaign এবং exchange-এর ধাপ আছে। SQL: ৮৭টি application/Identity table + migration history = মোট ৮৮টি।

## ১ অক্টোবরের আপডেট

বাস্তব SQL Server migration/Identity/concurrency এবং Ollama response পরীক্ষা হয়েছে। Identity key length ও checkout deadlock ত্রুটি ঠিক হয়েছে। Menu rules, search/history, barcode/thumbnail, replacement, POS terminal collection, expense receipts, CRM/report filters এবং dashboard/KPI যোগ হয়েছে।

বিস্তারিত: `docs/FEATURE_COVERAGE.md` (মূল ৫৬টি requirement), `docs/VALIDATION.md` (পরীক্ষা), `docs/TASK_STATUS_BN.md` (সীমা ও বাকি acceptance)।

## এই আপডেটে নতুন

Campaign ও banners, product PDF manuals ও attributes, address edit/default, threaded support, partial receiving ও supplier return, bank/payment reconciliation, vouchers/period lock, store credit, verified refund API, email outbox এবং server PDF export যোগ হয়েছে।

সব টেবিলের script: `sql/AI_Ecommerce_ERP_All_Tables.sql`। Install/upgrade নিয়ম: `sql/README_BN.md`। বাকি কাজের তালিকা: `docs/TASK_STATUS_BN.md`।

## Windows-এ শুরু করুন

1. **.NET 10 SDK** এবং **SQL Server 2022** install করুন। .NET 10-এর জন্য VS 2022 ব্যবহার করবেন না; compatible Visual Studio অথবা `dotnet` CLI ব্যবহার করুন।
2. ZIP extract করে `AI_Ecommerce_ERP` folder খুলুন। Folder address bar-এ `powershell` লিখে Enter দিন।
3. SQL Server service চালু রাখুন। Windows authentication হলে চালান:

```powershell
.\scripts\setup.ps1 -Server 'localhost' -Database 'ParuEcommerceERP'
```

SQL Express হলে:

```powershell
.\scripts\setup.ps1 -Server '.\SQLEXPRESS' -Database 'ParuEcommerceERP'
```

SQL username/password দিয়ে হলে:

```powershell
.\scripts\setup.ps1 -Server 'localhost' -Database 'ParuEcommerceERP' -SqlUser 'paru_app'
```

4. Setup চাইলে SQL password, website admin email/password এবং installed Ollama model-এর নাম লিখুন। Password-এ ১২+ অক্ষর, বড়/ছোট অক্ষর, সংখ্যা ও symbol রাখুন।
5. Setup packages restore, build, migrations ও seed চালাবে। Existing configuration ও database delete করবে না।
6. App চালান:

```powershell
.\scripts\start-local.ps1
```

7. Browser: **http://localhost:5080**। Staff login: **http://localhost:5080/account/login?admin=true**।
8. Setup-এ দেওয়া email/password দিয়ে Admin login করুন। Customer হিসেবে `/account/register`-এ নতুন account তৈরি করুন।

PowerShell script policy বাধা দিলে downloaded source যাচাই করে current process-এ চালাতে পারেন:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup.ps1
```

**SQL login এবং website login আলাদা।** SQL login শুধু database connection-এর জন্য। Website admin login ASP.NET Identity table-এ থাকে। কোনো hard-coded admin password নেই।

## Practice data

Setup সাতটি category, ১৪টি product/SKU, brand, supplier, দুইটি warehouse, opening stock, account heads, coupon এবং local SVG illustration তৈরি করে। Image file path database-এ সংরক্ষিত হয়। এগুলো illustration, বাস্তব product photograph নয়।

Windows setup-এ `Seed:IncludeDemoOrders=true`: দুইটি sample COD order, একটি sample collection এবং সংশ্লিষ্ট ledger entries তৈরি হয়। এগুলো **কৃত্রিম practice transactions**, বাস্তব বিক্রয় নয়। Production-এর জন্য আলাদা পরিষ্কার database এবং বাস্তব master data ব্যবহার করুন। Default application config-এ demo orders enabled নয়। Demo customer-এর password random; নিজে register করে checkout practice করুন।

## একটি সম্পূর্ণ practice flow

1. Admin → Products এবং Variants-এ product ও SKU দেখুন। Product row-এর Images থেকে নিজের PNG/JPEG/WebP upload করুন।
2. Inventory-এ stock দেখুন; purchase তৈরি করে Approve → Receive করুন। Stock ও average cost বাড়বে।
3. অন্য browser/incognito-তে customer register করুন। Product cart-এ দিন; checkout-এ `WELCOME10` ব্যবহার করতে পারেন।
4. Review → Confirm order করুন। Admin inventory-এ reserved quantity দেখুন।
5. Admin Orders থেকে Confirmed → Processing → Packed → ReadyToShip → Shipped → OutForDelivery → Delivered করুন।
6. COD cash পাওয়ার পরে **Record COD collection** করুন। Reports ও Accounts দেখুন।
7. Customer order থেকে return চাইতে পারবেন। Admin goods inspection-এর পরে accept/restock সিদ্ধান্ত দেবেন।
8. প্রয়োজনীয় refund merchant portal/cash process-এ সম্পন্ন করে verified reference record করুন। এই button নিজে টাকা পাঠায় না।

## Ollama

```powershell
ollama list
ollama serve
```

Ollama আগে থেকে চললে আবার `serve` করার প্রয়োজন নেই। `src/Ecommerce.Web/appsettings.Local.json`-এ `Ollama:Model`-এ **`ollama list`-এ থাকা exact নাম** দিন। `/assistant` খুলুন। GPU/CPU ও model অনুযায়ী response সময় পরিবর্তিত হয়।

AI-কে unrestricted SQL দেওয়া হয়নি। Customer শুধু নিজের order facts পান; admin summary-এর জন্য `ai.admin`, `reports.view`, `inventory.view` permissions একসঙ্গে লাগে। বর্তমানে context সর্বোচ্চ ১০০টি SKU, ২০টি own order; প্রশ্নে মিল থাকা products আগে নির্বাচন হয়। Model response নিজে authoritative payment/stock decision নয়।

## Online payment

SSLCommerz default disabled। `docs/PAYMENTS.md` অনুযায়ী sandbox merchant credentials ও public HTTPS callback URL দিন। Browser localhost-এ external gateway IPN পৌঁছাবে না। Live যাওয়ার আগে নিজের merchant account-এ sandbox tests চালান। bKash/Nagad direct adapters শুধু fail-closed extension architecture; checkout-এ enabled নয়।

## Database name / username / password পরিবর্তন

1. App বন্ধ করুন। `src/Ecommerce.Web/appsettings.Local.json` খুলুন।
2. `ConnectionStrings:Default`-এ `Server`, `Database`, `User Id`, `Password` বদলান। Windows login হলে `Trusted_Connection=True` ব্যবহার করুন।
3. নতুন database হলে `scripts/db-migrate.ps1`, তারপর `scripts/db-seed.ps1` চালান। নতুন database-এ পুরোনো data নিজে থেকে যাবে না।
4. পুরোনো database রেখে দিন; transfer চাইলে backup/restore বা reviewed data migration করুন।

Local configuration file-এ credentials থাকে—ZIP/share/git-এ সেটি দেবেন না।

## Docker Desktop (ঐচ্ছিক)

Docker Desktop install করে Linux containers চালু করুন। তারপর:

```powershell
Copy-Item .env.example .env
notepad .env
# .env-এর SQL_SA_PASSWORD, ADMIN_EMAIL, ADMIN_PASSWORD বদলান
# Connection-string-safe password ব্যবহার করুন; semicolon থাকলে quoted connection config প্রয়োজন।
docker compose up -d sql
docker compose build web
docker compose run --rm web --migrate --seed
docker compose up -d web
```

Open http://localhost:5080। এই compose **local development**-এর জন্য; production hardening আলাদা। SQL SA এখানে local bootstrap-এর জন্য; production app-এ সীমিত login ব্যবহার করুন। Data named volumes-এ থাকে; `docker compose down -v` data মুছে ফেলে—data রাখার প্রয়োজন হলে তা চালাবেন না।

## Project map

- `EcommerceERP.sln`: চারটি application project ও দুটি test project।
- `src/Ecommerce.Domain`: domain entities ও statuses।
- `src/Ecommerce.Application`: contracts এবং pure business rules।
- `src/Ecommerce.Infrastructure`: EF, migrations, Identity storage, repositories, transactional services, payments, Ollama, seed।
- `src/Ecommerce.Web`: MVC controllers, Razor layouts/views, CSS, images, configuration।
- `tests`: rule tests, SQLite relational workflow tests, HTTP/Identity/Razor tests।
- `scripts`: setup/start/migrate/seed/verify/publish/backup PowerShell।
- `sql`: generated idempotent migration script, backup/restore reference।
- `docs`: coverage, security, SQL, IIS, payments, tests।

## Verification ও পরবর্তী কাজ

```powershell
.\scripts\verify.ps1
.\scripts\publish.ps1
```

`docs/VALIDATION.md` দেখুন। SQL Server 2025 ও local Ollama দিয়ে বাস্তব পরীক্ষা হয়েছে। IIS/Docker, merchant sandbox, SMTP এবং backup/restore acceptance নিজের deployment environment-এ চালাতে হবে। SQL Server 2022 engine-এ আলাদা পরীক্ষা হয়নি।

## নিজের Windows-এ integration test

Chromium install করতে `scripts/install-pdf.ps1` চালান—PDF ও thumbnail দুটির জন্য লাগে। SQL Server Windows authentication এবং installed Ollama model থাকলে:

```powershell
.\scripts\verify-live.ps1 -Server '.\SQLEXPRESS' -OllamaModel 'আপনার-installed-model-এর-exact-name'
```

এটি নতুন `Paru_Acceptance_...` test database তৈরি করে; production database ব্যবহার করে না। মূল acceptance database ও concurrency database রাখা হয়; প্রত্যেক workflow-এর বিচ্ছিন্ন test database এবং upgrade test database পরীক্ষা শেষে delete হয়।
