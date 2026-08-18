# Distribuce a aktualizace

AppLauncher se distribuuje přes [Velopack](https://velopack.io). Instalátor je per-user
(`%LocalAppData%\AppLauncher`), nevyžaduje administrátorská práva a aplikace se sama
aktualizuje z GitHub Releases.

## Vydání nové verze

1. Nahrát změny do větve `main`.
2. GitHub → **Actions** → workflow **release** → **Run workflow**.
3. Do pole `version` zadat verzi v SemVer, například `1.0.1`, a potvrdit.

Workflow ověří verzi, sestaví aplikaci, vytvoří balíčky, založí tag `v1.0.1` a publikuje
GitHub Release. Trvá to zhruba 5–10 minut.

Release se nevytváří ručně přes web — vytvoří ho workflow. Pokud tag pro danou verzi už
existuje, workflow skončí chybou.

## Verzování

Verze je SemVer a zadává se při spuštění workflow. Hodnota `ApplicationDisplayVersion`
v `.csproj` slouží jen pro lokální buildy.

Verze s pomlčkou (`1.1.0-beta.1`) se označí jako pre-release. Aplikace pre-release verze
nenabízí, protože `GithubSource` je v `UpdateService` nastavený s `prerelease: false`.

## Výstupy release

| soubor | účel |
|---|---|
| `AppLauncher-win-Setup.exe` | instalátor pro koncové uživatele |
| `AppLauncher-<verze>-full.nupkg` | plný balík pro updater |
| `AppLauncher-<verze>-delta.nupkg` | rozdílový balík, od druhé verze |
| `releases.win.json` | feed, který čte aplikace |
| `AppLauncher-win-Portable.zip` | verze bez instalace a bez aktualizací |

## Lokální build instalátoru

```
dotnet tool install -g vpk --version 1.2.0
```

```
dotnet publish src/AppLauncher/AppLauncher/AppLauncher.csproj -c Release -f net10.0-windows10.0.19041.0 -r win-x64 -p:ApplicationDisplayVersion=1.0.1 -o publish
```

```
vpk pack -u AppLauncher -v 1.0.1 -p publish -e AppLauncher.exe --packTitle AppLauncher -o releases
```

Ve složce `releases` vznikne `AppLauncher-win-Setup.exe` a doprovodné balíčky. Aby se
vygenerovala delta, musí ve složce být i předchozí verze — stáhne je
`vpk download github --repoUrl https://github.com/kopeckyrobin/AppLauncher --token <PAT> -o releases`.

## Feed aktualizací

URL repozitáře se do binárky vkládá při buildu přes MSBuild property `UpdateFeedUrl`
a čte se za běhu z `AssemblyMetadataAttribute`. CI ji nastavuje automaticky na
`https://github.com/<owner>/<repo>`, výchozí hodnota v `.csproj` platí jen pro lokální
buildy.

## Poznámky

- Aplikace není podepsaná, takže SmartScreen při první instalaci zobrazí varování
  („Více informací" → „Přesto spustit").
- Aktualizace se neaplikuje sama — uživatel ji spustí tlačítkem v pravém horním rohu.
  Před restartem se ukončí všechny běžící spuštěné projekty.
- Tlačítko aktualizace se zobrazí jen v nainstalované verzi. Při běhu z Visual Studia je
  `UpdateManager.IsInstalled` false a kontrola se přeskočí.
- Nastavení se ukládá do `%AppData%\AppLauncher\state.json`, tedy mimo instalační
  adresář, a přežije aktualizaci i odinstalaci.
