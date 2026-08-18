# AppLauncher

Desktopový launcher pro .NET repozitáře. Na jednom místě zobrazí všechny projekty ve tvých repozitářích, spustí je přes `dotnet run` s vybraným launch profilem, streamuje jejich log a ukáže git diff — bez otevírání Visual Studia.

[![release](https://img.shields.io/github/v/release/kopeckyrobin/AppLauncher)](https://github.com/kopeckyrobin/AppLauncher/releases/latest)
[![build](https://github.com/kopeckyrobin/AppLauncher/actions/workflows/release.yml/badge.svg)](https://github.com/kopeckyrobin/AppLauncher/actions/workflows/release.yml)
![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)
![dotnet](https://img.shields.io/badge/.NET-10-512BD4)

<!-- Screenshot: nahraj obrázek do docs/screenshot.png a odkomentuj řádek níže.
![AppLauncher](docs/screenshot.png)
-->

---

## Proč to existuje

Když máš deset mikroslužeb ve třech repozitářích, spouštění přes Visual Studio znamená deset otevřených instancí a hledání, který port patří které službě. AppLauncher naskenuje kořenovou složku repozitářů, najde spustitelné projekty podle jejich `launchSettings.json` a dá ti nad nimi jeden seznam se stavem, logem a odkazem na běžící endpoint.

## Funkce

| Oblast | Co umí |
|---|---|
| **Skenování** | Projde repozitáře, načte `.sln` / `.slnx`, vytáhne `.csproj` a jejich launch profily |
| **Spouštění** | `dotnet run` s vybraným profilem, přepínání profilů, více aplikací současně |
| **Log** | Živý stdout + stderr, fulltextové hledání s čísly řádků, kopírování do schránky |
| **Endpointy** | Z logu vytáhne `Now listening on…`, zobrazí porty a otevře URL v prohlížeči |
| **Git** | Diff pracovního stromu i posledních 50 commitů, inline / side-by-side, minimapa změn |
| **Commit & push** | `add --all` → `commit` → `push --set-upstream` přímo z aplikace |
| **Běh na pozadí** | Zavření okna schová aplikaci do tray, běžící projekty jedou dál |
| **Aktualizace** | Velopack, automatická kontrola GitHub Releases, ruční potvrzení restartu |

## Instalace

**Požadavky pro běh aplikace:** Windows 10 verze 1809 (build 17763) nebo novější. Nic dalšího — instalátor je self-contained.

**Požadavky pro spouštěné projekty:** [.NET SDK](https://dotnet.microsoft.com/download) v `PATH` (aplikace volá `dotnet run`) a [Git](https://git-scm.com/) v `PATH`, pokud chceš používat diff panel.

Stáhni `AppLauncher-win-Setup.exe` z [posledního release](https://github.com/kopeckyrobin/AppLauncher/releases/latest) a spusť ho. Instaluje se per-user do `%LocalAppData%\AppLauncher` a **nevyžaduje administrátorská práva**.

> Aplikace není code-signed, takže SmartScreen při první instalaci zobrazí varování — *Více informací* → *Přesto spustit*.

Alternativa bez instalace: `AppLauncher-win-Portable.zip` ze stejného release. Portable verze se ale sama neaktualizuje.

## Rychlý start

1. Spusť AppLauncher. Ve výchozím stavu skenuje `%UserProfile%\source\repos`.
2. Není-li to tvoje složka, klikni na cestu v hlavičce a vyber jinou — volba se uloží.
3. Rozbal repozitář, u projektu vyber launch profil a klikni **Run**.
4. Log běží v pravém panelu. Jakmile se objeví `Now listening on`, u projektu se zobrazí port a odkaz na otevření v prohlížeči.
5. **Stop** ukončí proces včetně potomků. **Stop all** ukončí všechno.

## Jak funguje skenování

Aplikace nehledá projekty rekurzivně po celém disku — spoléhá na konvenci `src/`:

```
<kořen repozitářů>/
└── <repozitář>/
    └── src/
        ├── Foo.slnx           ← solution přímo v src/
        └── Bar/
            └── Bar.sln        ← nebo o úroveň níž
```

Pravidla:

- Repozitář se zobrazí, jen pokud obsahuje složku `src/`.
- Řešení se hledají v `src/` a v jeho **přímých** podsložkách. Hlouběji už ne.
- Složky začínající tečkou a `node_modules` se přeskakují.
- Když vedle sebe leží `Foo.sln` a `Foo.slnx`, přednost dostane `.slnx`.
- Projekt se zobrazí, jen pokud má `Properties/launchSettings.json` s aspoň jedním profilem, kde `commandName` je `Project` (nebo chybí).
- Projekt zapsaný ve víc řešeních se zobrazí jen jednou, u prvního z nich.

**Multi-target projekty:** má-li `.csproj` víc TFM, aplikace vybere jeden a přidá ho na příkazovou řádku jako `-f`. Preferuje framework s `-windows`, jinak první bez pomlčky.

Spouštěný příkaz je vždy vidět v prvním řádku logu:

```
dotnet run --project "C:\repos\Foo\src\Foo.Api\Foo.Api.csproj" --launch-profile "https" -f net10.0
```

## Git panel

Tlačítko u repozitáře otevře diff panel. V rozbalovacím seznamu nahoře přepínáš mezi **Current Changes** (pracovní strom proti `HEAD`) a posledními 50 commity.

- Seznam souborů má vlastní filtr, diff má vlastní hledání s `n` / `N` navigací a počítadlem shod.
- Přepínání **inline** ↔ **side-by-side**.
- Svislý pruh vedle diffu je minimapa změn s indikací aktuálního výřezu.
- Netrackované soubory se vykreslí jako samé přidané řádky. Binární soubory se přeskočí.
- **Commit & push** provede `git add --all`, `git commit -m <zpráva>` a `git push --set-upstream origin <větev>`. Na detached HEAD operaci odmítne. Timeout je 5 minut.

Git se volá s `GIT_OPTIONAL_LOCKS=0` a `GIT_TERMINAL_PROMPT=0`, takže neblokuje ostatní nástroje a nikdy se neptá na přihlašovací údaje v terminálu — push tedy vyžaduje nakonfigurovaný credential helper.

**Limity, aby zůstal panel svižný:** max. 500 souborů na zdroj, 4 000 řádků na diff, 400 kB na netrackovaný soubor. Překročení se v UI označí.

## Chování procesů

- Každý spuštěný projekt je potomek AppLauncheru bez vlastního okna, stdout i stderr jsou přesměrované do logu (UTF-8).
- Procesy jsou přiřazené do Windows **job objectu** s `KILL_ON_JOB_CLOSE` — když AppLauncher spadne nebo ho ukončíš, nezůstanou viset osiřelé `dotnet` procesy.
- **Stop** posílá kill celému stromu procesů.
- Log drží posledních **2 000 řádků**; starší se zahazují, ale číslování řádků při hledání zůstává správné.
- Stav `Starting` přejde na `Running`, jakmile se v logu objeví naslouchající URL, nejpozději po 10 sekundách.
- Zavření okna aplikaci jen schová do oznamovací oblasti — **běžící projekty pokračují**. Ukončit ji úplně jde přes tray menu. Aplikace běží v jediné instanci; spuštění druhé jen aktivuje tu první.

## Nastavení

Vše se ukládá do `%AppData%\AppLauncher\state.json`, tedy mimo instalační adresář — přežije aktualizaci i odinstalaci.

```json
{
  "repositoriesRoot": "C:\\Users\\me\\source\\repos",
  "lastProfiles": {
    "C:\\...\\Foo.Api.csproj": "https"
  },
  "collapsedRepositories": [
    "C:\\Users\\me\\source\\repos\\Bar"
  ]
}
```

| klíč | význam |
|---|---|
| `repositoriesRoot` | skenovaná kořenová složka |
| `lastProfiles` | naposledy použitý launch profil pro každý projekt |
| `collapsedRepositories` | sbalené repozitáře v seznamu |

## Build ze zdrojů

Potřebuješ .NET 10 SDK a MAUI workload:

```bash
dotnet workload install maui-windows
```

```bash
dotnet build src/AppLauncher/AppLauncher.slnx -c Debug
```

Spuštění z příkazové řádky:

```bash
dotnet run --project src/AppLauncher/AppLauncher/AppLauncher.csproj -f net10.0-windows10.0.19041.0
```

Při běhu z Visual Studia nebo z `dotnet run` je `UpdateManager.IsInstalled` false, takže se tlačítko aktualizace nezobrazí.

## Architektura

MVVM bez externího frameworku. Žádný DI kontejner, žádná ORM, žádné knihovny navíc — jediná runtime závislost mimo MAUI je Velopack.

```
src/AppLauncher/AppLauncher/
├── Models/          datové typy (ScanResults, LaunchProfile, GitModels, RunState)
├── Services/        práce s okolím: skener, čtečky .sln/.csproj/launchSettings,
│                    ProcessRunner + ProcessJob, GitService, DiffParser,
│                    AppStateStore, UpdateService
├── ViewModels/      MainViewModel, ProjectViewModel, RepositoryViewModel,
│                    GitDiffViewModel, UpdateViewModel
├── Views/           minimapa změn, template selektory pro diff
├── Converters/      XAML konvertory
├── Platforms/Windows/  vstupní bod, tray ikona, single-instance mutex
└── MainPage.xaml    celé UI
```

Výstup běžících procesů se sbírá do concurrent queue a do UI se přelévá časovačem po 200 ms, aby ukecaný log neubil UI vlákno.

## Distribuce a vydání nové verze

Postup vydání, verzování a lokální build instalátoru popisuje [RELEASE.md](RELEASE.md).

Ve zkratce: GitHub → **Actions** → workflow **release** → **Run workflow** → zadat SemVer verzi. Workflow sestaví aplikaci, vytvoří Velopack balíčky, založí tag a publikuje GitHub Release. Nainstalovaná aplikace kontroluje aktualizace každých 30 minut a nabídne je tlačítkem v pravém horním rohu; aktualizace se nikdy neaplikuje sama, protože restart ukončí všechny běžící projekty.

Aktuálně běžící verze je vidět v hlavičce vedle názvu aplikace. U nainstalované verze ji hlásí Velopack, jinak se čte z `AssemblyInformationalVersion`.

## Omezení

- Pouze Windows. MAUI projekt cílí výhradně na `net10.0-windows10.0.19041.0`.
- Detekují se jen .NET projekty (`.csproj` s launch profilem). Node, Python ani Docker Compose ne.
- Aplikace nestaví ani neobnovuje balíčky sama — `dotnet run` to udělá za ni se svými výchozími hodnotami.
- Binárka není podepsaná.
- Bez automatizovaných testů.

## Licence

Repozitář zatím neobsahuje soubor s licencí, takže platí výchozí stav — všechna práva vyhrazena.
