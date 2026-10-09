# Plan détaillé : Plugin "CJK Name Fixer" + Publication GitHub

---

## Partie 1 — Développement du plugin

### 1. Initialisation du projet

- Basculer sur le template officiel `jellyfin/jellyfin-plugin-template` (GitHub) comme point de départ
- Créer un nouveau projet .NET 8.0 (classlib) nommé `Jellyfin.Plugin.CjkNameFixer`
- Ajouter les packages NuGet : `TMDbLib` (≥ 2.1.0) pour l'API TMDB
- Ajouter les références vers les assemblies Jellyfin (`Jellyfin.Model`, `Jellyfin.Controller`) via NuGet (méthode moderne) ou par `HintPath` vers le dossier `plugins/` de l'instance locale (méthode legacy)
- Générer un **GUID unique** pour le plugin (utilisé dans `Plugin.cs` et le `manifest.json`)

### 2. Structure des fichiers à créer

| Fichier | Rôle |
|---------|------|
| `Plugin.cs` | Entry point. Hérite de `BasePlugin<PluginConfiguration>`. Définit le nom, le GUID, la description. |
| `PluginConfiguration.cs` | POCO avec les options utilisateur (voir ci-dessous). |
| `PluginServiceRegistrator.cs` | Enregistre les services dans le DI de Jellyfin. |
| `CjkDetector.cs` | Classe statique. Détecte la présence de caractères CJK dans une string (ranges Unicode). |
| `TmdbNameResolver.cs` | Encapsule les appels API TMDB. Récupère le nom romanisé d'une personne. |
| `PersonNameFixer.cs` | Logique principale : scan → détection → résolution → mise à jour. |
| `ScheduledTasks/FixCjkNamesTask.cs` | Implémente `IScheduledTask` pour exécution manuelle depuis le dashboard. |
| `PostScanTasks/CjkPostScanFixer.cs` | Implémente `ILibraryPostScanTask` pour correction auto après chaque scan. |
| `README.md` | Documentation : installation, configuration, limitations. |
| `LICENSE` | Licence (MIT recommandé pour la visibilité community). |

### 3. Options de configuration (`PluginConfiguration`)

| Option | Type | Défaut | Description |
|--------|------|--------|-------------|
| `EnablePostScanFix` | bool | `true` | Activer la correction automatique après scan. |
| `EnableScheduledTask` | bool | `true` | Activer la tâche planifiée manuelle. |
| `ScheduledIntervalHours` | int | `168` | Intervalle de la tâche planifiée (heures). |
| `DryRun` | bool | `false` | Mode test : log seulement, aucune modification. |
| `TmdbApiKeyOverride` | string | `null` | Clé API custom. Si null, utiliser celle du plugin TMDb core. |
| `ThrottleMs` | int | `500` | Délai entre chaque appel API TMDB (rate limiting). |
| `MergeDuplicates` | bool | `true` | Fusionner les doublons Person (même TMDB ID, nom CJK + romanisé). |

### 4. Logique de détection CJK

- Scanner chaque caractère du nom de la Person
- Ranges Unicode à couvrir :
  - CJK Unified Ideographs : `U+4E00`–`U+9FFF`
  - CJK Extension A : `U+3400`–`U+4DBF`
  - Hiragana : `U+3040`–`U+309F`
  - Katakana : `U+30A0`–`U+30FF`
  - Hangul Syllables : `U+AC00`–`U+D7AF`
  - Hangul Jamo : `U+1100`–`U+11FF`
  - Hangul Compatibility Jamo : `U+3130`–`U+318F`
- Retourner `true` au premier caractère CJK détecté (pas besoin de scanner tout)

### 5. Logique de résolution du nom romanisé

- Extraire le TMDB Person ID via `GetProviderId(MetadataProvider.Tmdb)`
- Si l'ID est absent → skip + log warning
- Appeler `TMDbClient.GetPersonAsync(id, language: "en")`
- TMDB retourne le "Stage Name" romanisé en `en` (ex : `Kimura Takuya` au lieu de `木村拓哉`)
- Si le résultat est vide ou identique au nom actuel → skip
- Appliquer un `Task.Delay(ThrottleMs)` entre chaque appel pour respecter le rate limit TMDB (100 req / 10 min)

### 6. Logique de mise à jour

- Vérifier `IsLocked` sur le champ nom → si verrouillé, skip + log info
- Mettre à jour `person.Name` avec le nom romanisé
- Appeler `ILibraryManager.UpdateAsync` avec le provider TMDB
- Logger l'ancienne et la nouvelle valeur

### 7. Gestion des doublons (option `MergeDuplicates`)

- Avant de renommer, vérifier si une autre Person avec le **même TMDB ID** existe déjà avec un nom latin
- Si oui : relier les crédits de l'entrée CJK à l'entrée romanisée existante, puis supprimer l'entrée CJK
- Évite de créer des doublons après correction

### 8. Tâche planifiée (`IScheduledTask`)

- Nom : "Fix CJK Person Names"
- Catégorie : "Metadata"
- Pas de trigger automatique par défaut (exécution manuelle via le dashboard)
- Optionnel : trigger périodique configurable via `ScheduledIntervalHours`
- Retourner un résumé via `IProgress` (nombre de corrections, skips, erreurs)

### 9. Post-scan task (`ILibraryPostScanTask`)

- Se déclenche automatiquement après chaque scan de bibliothèque
- Vérifier `EnablePostScanFix` avant d'exécuter
- Utiliser le `CancellationToken` fourni pour pouvoir être annulé
- Ne pas bloquer le scan : exécuter en arrière-plan

### 10. Récupération de la clé API TMDB

- Lire la configuration du plugin TMDb core via `IServerConfigurationManager`
- Parser le fichier XML de config du plugin TMDb pour extraire la clé
- Fallback : utiliser `TmdbApiKeyOverride` si défini dans la config du plugin
- Si aucune clé disponible → log error, ne pas crasher

### 11. Points critiques de robustesse

| # | Point | Détail |
|---|-------|--------|
| 1 | **Idempotence** | Re-exécution sans effet : si le nom est déjà en latin, skip. |
| 2 | **Rate limiting** | Throttle entre appels TMDB. Gérer les 429 avec retry + backoff. |
| 3 | **Thread safety** | `ILibraryPostScanTask` tourne en parallèle. Utiliser `CancellationToken`. |
| 4 | **Gestion d'erreurs** | Try/catch autour de chaque appel API. Un échec ne doit pas stopper la boucle. |
| 5 | **Log** | Utiliser `ILogger<T>`. Logger chaque correction (ancien → nouveau), chaque skip (raison), chaque erreur. |
| 6 | **Version minimale Jellyfin** | Cibler Jellyfin 10.9+ (API stable). Tester sur 10.10, 10.11, 12.0. |

### 12. Scénarios de test

1. **Japonais** : `木村拓哉` → `Kimura Takuya`
2. **Coréen** : `김수현` → `Kim Soo-hyun`
3. **Chinois** : `周星驰` → `Stephen Chow`
4. **Pas de TMDB ID** → skip
5. **Nom déjà romanisé** → non détecté
6. **Nom verrouillé** → skip
7. **Dry run** → log sans modification
8. **100+ personnes CJK** → throttle respecté, pas de 429
9. **Post-scan** : ajout d'un film coréen → scan → correction auto
10. **Doublon** : même TMDB ID avec nom CJK + romanisé → fusion

---

## Partie 2 — Publication sur GitHub

### Étape 1 : Créer le dépôt public

- Sur GitHub → **New repository**
- Nom : `jellyfin-plugin-cjk-name-fixer` (convention : `jellyfin-plugin-*`)
- Visibilité : **Public**
- Cocher : Add README, Add .gitignore (C# template), Add MIT license
- Pusher le code initial

### Étape 2 : Configurer GitHub Actions (CI/CD)

Créer le fichier `.github/workflows/build-and-publish.yml` dans le dépôt. Ce workflow fait deux choses :

**A. Build (à chaque push sur `main`)**
- Checkout du code
- Setup .NET 8.0
- `dotnet build -c Release`
- Upload du `.dll` comme artifact (pour test manuel)
- Ne crée PAS de release

**B. Publish (à chaque création de GitHub Release)**
- Checkout du code
- Setup .NET 8.0
- `dotnet publish -c Release`
- Attacher le `.dll` (ou `.zip`) à la Release via `softprops/action-gh-release`
- Générer/mettre à jour le `manifest.json`
- Committer le `manifest.json` sur la branche `gh-pages` (ou `main`)
- Déployer sur GitHub Pages

> **Option alternative** : utiliser l'action réutilisable `LizardByte/jellyfin-plugin-repo` qui automatise la génération du manifest + le push sur `gh-pages`. C'est la méthode la plus simple.

### Étape 3 : Créer le `manifest.json`

C'est le fichier **critique** que Jellyfin lit pour découvrir le plugin. Il doit être accessible via une URL HTTPS publique.

**Structure du manifest :**

| Champ | Valeur | Description |
|-------|--------|-------------|
| `name` | `CJK Name Fixer` | Nom affiché dans le Catalog. |
| `description` | `Fixes CJK person names...` | Description courte. |
| `overview` | (texte long) | Description détaillée (HTML autorisé). |
| `owner` | `ton-username-github` | Identifiant GitHub. |
| `category` | `General` | Catégorie dans le Catalog. |
| `versions[]` | (tableau) | Liste des versions publiées. |

**Chaque entrée dans `versions[]` :**

| Champ | Valeur | Description |
|-------|--------|-------------|
| `version` | `1.0.0` | Version sémantique (doit matcher le `csproj`). |
| `changelog` | `Initial release` | Notes de version. |
| `targetAbi` | `10.9.0.0` | Version minimale de Jellyfin supportée. |
| `sourceUrl` | `https://github.com/.../releases/download/v1.0.0/CjkNameFixer.dll` | URL directe du binaire (GitHub Release). |
| `checksum` | `sha256:...` | Hash SHA-256 du fichier. |
| `timestamp` | `2026-10-08T00:00:00Z` | Date de publication (ISO 8601). |

**Où héberger le manifest :**

| Méthode | URL résultante | Avantage |
|---------|---------------|----------|
| **GitHub Pages** (recommandé) | `https://ton-user.github.io/jellyfin-plugin-cjk-name-fixer/manifest.json` | Gratuit, auto-déployé, HTTPS. |
| **Raw GitHub** | `https://raw.githubusercontent.com/ton-user/jellyfin-plugin-cjk-name-fixer/main/manifest.json` | Le plus simple, mais pas de versioning propre. |
| **GitHub Release asset** | `https://github.com/.../releases/download/v1.0.0/manifest.json` | Moins courant, moins propre. |

> **Recommandation** : GitHub Pages. Activer dans Settings → Pages → Source : "GitHub Actions" (le workflow déploie le manifest).

### Étape 4 : Activer GitHub Pages

- Settings du dépôt → **Pages**
- Source : **GitHub Actions** (le workflow de publish s'occupe du deploy)
- Ou : Source : **Deploy from a branch** → branche `gh-pages` → dossier `/`
- Le manifest sera servi à `https://ton-user.github.io/jellyfin-plugin-cjk-name-fixer/manifest.json`

### Étape 5 : Créer la première Release

- Sur GitHub → **Releases** → **Draft a new release**
- Tag : `v1.0.0`
- Title : `v1.0.0`
- Description : changelog
- Le workflow se déclenche automatiquement :
  - Build → attach le `.dll` à la release
  - Génère le `manifest.json` avec l'URL de la release
  - Push sur `gh-pages` → GitHub Pages se met à jour

### Étape 6 : Vérifier que tout fonctionne

1. Ouvrir `https://ton-user.github.io/jellyfin-plugin-cjk-name-fixer/manifest.json` dans un navigateur → doit retourner du JSON valide
2. Vérifier que `sourceUrl` pointe vers un `.dll` téléchargeable
3. Sur Jellyfin : **Dashboard → Plugins → Repositories** → Add
   - Nom : `CJK Name Fixer`
   - URL : `https://ton-user.github.io/jellyfin-plugin-cjk-name-fixer/manifest.json`
4. **Dashboard → Plugins → Catalog** → le plugin apparaît
5. Installer → Restart → vérifier que le plugin est actif

### Étape 7 : Visibilité dans la communauté (optionnel)

| Canal | Action |
|-------|--------|
| **Universal Plugin Repository** (`0belous/Jellyfin-Universal-Plugin-Repo`) | Ouvrir un PR : ajouter une ligne dans `sources.txt` au format `manifest URL \| repo URL` |
| **Awesome Jellyfin** (`awesome-jellyfin/awesome-jellyfin`) | Ouvrir un PR pour lister le plugin dans la section Metadata |
| **JellyWatch Community Hub** | Soumettre le plugin via leur formulaire de modération |
| **Forum Jellyfin** | Poster un thread dans la section "Plugins" avec le lien GitHub |
| **r/jellyfin** | Partager dans le subreddit (thread type "Plugin") |

---
## Résumé du flux complet
```Code écrit localement       │       ▼Push sur GitHub (dépôt public)       │       ▼Créer une GitHub Release (tag v1.0.0)       │       ▼GitHub Actions se déclenche  ├── Build le .dll  ├── L'attache à la Release  ├── Génère manifest.json  └── Déploie sur GitHub Pages       │       ▼manifest.json accessible publiquement       │       ▼Utilisateurs ajoutent l'URL dans Jellyfin → Catalog → Install```
## Fichiers à avoir dans le dépôt au final
```jellyfin-plugin-cjk-name-fixer/|-- .github/|   `-- workflows/|       `-- build-and-publish.yml|-- src/|   |-- Jellyfin.Plugin.CjkNameFixer/|   |   |-- Jellyfin.Plugin.CjkNameFixer.csproj|   |   |-- Plugin.cs|   |   |-- PluginConfiguration.cs|   |   |-- PluginServiceRegistrator.cs|   |   |-- CjkDetector.cs|   |   |-- TmdbNameResolver.cs|   |   |-- PersonNameFixer.cs|   |   |-- ScheduledTasks/|   |   |   `-- FixCjkNamesTask.cs|   |   `-- PostScanTasks/|   |       `-- CjkPostScanFixer.cs|   `-- Jellyfin.Plugin.CjkNameFixer.sln|-- manifest.json|-- README.md|-- LICENSE`-- .gitignore```   


## Instance Jellyfin de test (Docker)

Le fichier `compose.yaml` démarre une instance isolée de Jellyfin 10.11.11 pour le développement et les essais d’intégration du plugin. Elle n’accède à aucun dossier média de l’hôte.

- Interface : <http://127.0.0.1:18096>
- Configuration et cache : volumes Docker `cjk-name-fixer-jellyfin-config` et `cjk-name-fixer-jellyfin-cache`
- Le port est lié à `127.0.0.1` uniquement.

Au premier démarrage, ouvre l’interface et crée un compte administrateur de test. Commandes depuis ce dossier :

```sh
sudo docker compose up -d
sudo docker compose logs -f jellyfin-test
sudo docker compose down
```

`down` arrête et supprime le conteneur; les deux volumes de données sont conservés.
