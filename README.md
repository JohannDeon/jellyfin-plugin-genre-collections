# Genre Collections pour Jellyfin

Plugin Jellyfin (serveur **12.x**, .NET 10) qui :

1. **Crée et tient à jour une collection par genre** (Horreur, Comédie, Action…) pour les films et les séries. Les nouveaux titres sont rangés automatiquement après chaque scan de bibliothèque.
2. **Ajoute des rangées sur l'écran d'accueil** pour retrouver ses contenus sans fouiller dans les dossiers :
   - « Nouveaux films » (derniers ajoutés, toutes bibliothèques confondues)
   - « Séries avec de nouveaux épisodes »
   - « Collections par genre » (accès direct à chaque collection)
   - une rangée par genre (aléatoire, derniers ajoutés, mieux notés ou plus récents)

Le plugin **n'ajoute aucun CSS**. Les rangées utilisent les mêmes éléments et classes que les rangées natives de Jellyfin, donc un thème comme **Abyss** les habille exactement comme le reste de l'accueil.

## Installation

1. Télécharger `genre-collections.zip` depuis la page [Releases](../../releases) (ou l'artefact du dernier build dans l'onglet *Actions*).
2. Décompresser `Jellyfin.Plugin.GenreCollections.dll` dans un dossier `GenreCollections` du répertoire des plugins :
   - Docker : `/config/plugins/GenreCollections/`
   - Linux : `/var/lib/jellyfin/plugins/GenreCollections/`
   - Windows : `%LOCALAPPDATA%\jellyfin\plugins\GenreCollections\`
3. Redémarrer Jellyfin.
4. *Tableau de bord → Extensions → Genre Collections* pour régler les options, puis **Synchroniser maintenant** (ou lancer un scan de bibliothèque).

### Rangées d'accueil : accès en écriture à `index.html`

Pour afficher les rangées, le plugin ajoute une seule balise `<script>` dans le fichier `index.html` du client web au démarrage, et la retire à l'arrêt du serveur. Jellyfin doit donc pouvoir écrire dans ce fichier :

- Image Docker officielle lancée en root : rien à faire.
- Docker avec `--user` ou installation Linux : donner les droits d'écriture à l'utilisateur Jellyfin, par exemple `sudo chown jellyfin /usr/share/jellyfin/web/index.html`.

Si l'écriture est impossible, un avertissement apparaît dans les logs. Les collections fonctionnent quand même.

Les rangées apparaissent dans le client **web** (navigateur, application de bureau, et les applications qui embarquent le client web comme Android ou iOS). Les clients natifs (Android TV, Swiftfin, Infuse…) affichent les collections mais pas les rangées.

## Options

| Option | Par défaut | Rôle |
| --- | --- | --- |
| Inclure films / séries | oui / oui | Types de contenus rangés |
| Séparer films et séries | non | « Horreur » mixte, ou « Horreur (Films) » + « Horreur (Séries) » |
| Formats de nom | `{0}`, `{0} (Films)`, `{0} (Séries)` | `{0}` est remplacé par le genre |
| Minimum par genre | 3 | En dessous, pas de collection |
| Genres exclus | Téléfilm, TV Movie | Jamais de collection |
| Alias | voir ci-dessous | Fusionne ou découpe des genres |
| Supprimer les collections inutiles | oui | Supprime une collection de genre qui n'a plus assez de titres |
| Position des rangées | au-dessus | Au-dessus ou en dessous des rangées Jellyfin (« Reprendre », « À suivre »…) |
| Genres à afficher en premier | vide | Ordre des rangées de genre, le reste suit par taille |

Les alias par défaut découpent les genres TMDb des séries pour qu'ils rejoignent ceux des films :

```
Action & Adventure=Action,Aventure
Sci-Fi & Fantasy=Science-Fiction,Fantastique
Science-Fiction & Fantastique=Science-Fiction,Fantastique
Science Fiction=Science-Fiction
War & Politics=Guerre
Guerre & Politique=Guerre
```

Les collections créées par le plugin sont reconnues par un identifiant interne (`GenreCollections`). Le plugin ne modifie jamais les collections créées à la main.

## Compiler

```bash
dotnet test
dotnet publish Jellyfin.Plugin.GenreCollections -c Release -o artifacts
```

Pousser un tag `v1.0.0` publie automatiquement une release avec le zip.

## Licence

GPL-3.0, comme le modèle de plugin Jellyfin.
