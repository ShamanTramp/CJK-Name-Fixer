# CJK Name Fixer

Plugin Jellyfin qui remplace à la demande les noms de personnes écrits en caractères chinois, japonais ou coréens par leur nom latin fourni par TMDb.

## Fonctionnement

- La tâche **Fix CJK Person Names** analyse les fiches Person de la bibliothèque lorsqu’elle est lancée manuellement.
- Le traitement des nouveaux médias peut être activé dans les paramètres. Il ne vérifie que les personnes liées au média ajouté; aucun scan périodique ou déclenchement après un scan de bibliothèque n’est configuré.
- Un nom est modifié uniquement si la fiche a un identifiant TMDb, si le nom correspondant est trouvé et si le champ n’est pas verrouillé.
- Les réponses TMDb positives et négatives sont mises en cache 12 heures. Le délai entre les requêtes est configurable.
- Par défaut, le plugin réutilise le client TMDb de Jellyfin. Une clé TMDb personnelle peut être fournie pour utiliser son propre quota.
- Les modifications sont enregistrées directement. Il n’y a pas de case « mode simulation » dans les paramètres.

## Installation depuis le dépôt du plugin

Dans Jellyfin, ouvre **Tableau de bord → Plugins → Dépôts**, ajoute ce dépôt :

`https://raw.githubusercontent.com/ShamanTramp/CJK-Name-Fixer/main/manifest.json`

Enregistre, ouvre le catalogue, installe **CJK Name Fixer**, puis redémarre Jellyfin. La première version sera disponible une fois la release GitHub publiée et le manifeste généré par l’action de publication.

Les versions peuvent aussi être téléchargées depuis [GitHub Releases](https://github.com/ShamanTramp/CJK-Name-Fixer/releases). L’archive contient les fichiers du plugin; l’instance Jellyfin Docker de développement n’est jamais incluse dans cette archive.

## Paramètres

| Paramètre | Défaut | Description |
| --- | --- | --- |
| Vérifier les noms à l’ajout | Activé | Vérifie les personnes liées aux nouveaux films et épisodes. |
| Délai entre les requêtes TMDb | 500 ms | Ralentit les appels au fournisseur pour limiter les requêtes. |
| Clé API TMDb personnelle | Vide | Facultative; vide, le plugin utilise le client TMDb de Jellyfin. |

Pour traiter les médias déjà présents, lance manuellement **Fix CJK Person Names** depuis **Tableau de bord → Tâches planifiées**.

## Compatibilité

- Jellyfin Server **10.11.11**
- .NET **9**
- Linux, Windows et macOS pris en charge par Jellyfin

Le premier build est ciblé sur Jellyfin 10.11.11. La compatibilité avec d’autres versions n’est pas encore annoncée.

## Construire et tester

```sh
dotnet restore Jellyfin.Plugin.CjkNameFixer.sln
dotnet build Jellyfin.Plugin.CjkNameFixer.sln -c Release
dotnet test Jellyfin.Plugin.CjkNameFixer.sln -c Release
```

Les tests unitaires couvrent la détection Unicode, le cache TMDb et l’orchestration avec des dépendances simulées. Le dépôt contient aussi un `compose.yaml` pour une instance Jellyfin locale de test; ce conteneur sert au développement et n’est pas distribué avec le plugin.

## Licence

GPL-3.0-only. Voir [LICENSE](LICENSE).
