<h1 align="center">Genre Collections pour Jellyfin</h1>

<p align="center">
  Des collections automatiques par genre et un écran d'accueil qui met en avant tes films et séries.
</p>

<p align="center">
  <a href="../../releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/JohannDeon/jellyfin-plugin-genre-collections?label=version"></a>
  <a href="../../actions/workflows/build.yml"><img alt="Build" src="https://img.shields.io/github/actions/workflow/status/JohannDeon/jellyfin-plugin-genre-collections/build.yml?branch=main"></a>
  <img alt="Jellyfin" src="https://img.shields.io/badge/Jellyfin-12.x-00a4dc">
  <a href="LICENSE"><img alt="Licence" src="https://img.shields.io/github/license/JohannDeon/jellyfin-plugin-genre-collections"></a>
</p>

---

## Sommaire

- [Fonctionnalités](#fonctionnalités)
- [Compatibilité](#compatibilité)
- [Installation](#installation)
- [Premiers pas](#premiers-pas)
- [Configuration](#configuration)
- [Questions fréquentes](#questions-fréquentes)
- [Désinstallation](#désinstallation)
- [Développement](#développement)
- [Licence](#licence)

## Fonctionnalités

### Collections par genre

- Crée **une collection par genre** (Horreur, Comédie, Action, Animation…) à partir des genres de tes films et séries.
- Les collections se **mettent à jour toutes seules** : après chaque scan de bibliothèque, et chaque nuit à 4h.
- Les genres des séries TMDb sont **harmonisés avec ceux des films**. Par exemple, « Action & Adventure » est rangé dans Action et dans Aventure.
- Tu choisis : collections mixtes films + séries, ou séparées (« Horreur (Films) » / « Horreur (Séries) »).
- Tu peux exclure des genres, fixer un nombre minimum de titres par genre et changer le format des noms.
- **Tes collections créées à la main ne sont jamais modifiées.**

### Écran d'accueil

Ces rangées s'ajoutent à l'accueil, au-dessus ou en dessous des rangées habituelles de Jellyfin :

| Rangée | Contenu |
| --- | --- |
| Nouveaux films | Les derniers films ajoutés, toutes bibliothèques confondues |
| Séries avec de nouveaux épisodes | Les séries qui viennent de recevoir des épisodes |
| Collections par genre | Accès direct à chaque collection de genre |
| Une rangée par genre | Au choix : aléatoire, derniers ajoutés, mieux notés ou plus récents |

### Compatible avec les thèmes

Le plugin **n'ajoute aucun CSS**. Les rangées utilisent les mêmes éléments que les rangées natives de Jellyfin, donc ton thème (par exemple **Abyss**) les habille comme le reste de l'interface.

## Compatibilité

| Plugin | Jellyfin |
| --- | --- |
| 1.x | 12.x |

Les rangées d'accueil s'affichent dans le **client web** : navigateur, Jellyfin Media Player, et les applications Android et iOS qui utilisent le client web. Les clients natifs (Android TV, Swiftfin, Infuse…) affichent les collections, mais pas les rangées.

## Installation

### Méthode 1 : dépôt de plugins (recommandée)

1. Dans Jellyfin, ouvre **Tableau de bord → Extensions → Dépôts** (onglet *Catalogue*, puis l'icône des paramètres selon la version).
2. Clique sur **+** et ajoute :
   - **Nom** : `Genre Collections`
   - **URL** :
     ```
     https://raw.githubusercontent.com/JohannDeon/jellyfin-plugin-genre-collections/main/manifest.json
     ```
3. Va dans **Catalogue**, trouve **Genre Collections** et clique sur **Installer**.
4. **Redémarre Jellyfin.**

Les mises à jour apparaissent ensuite automatiquement dans le catalogue.

### Méthode 2 : installation manuelle

1. Télécharge le zip de la [dernière release](../../releases/latest).
2. Décompresse `Jellyfin.Plugin.GenreCollections.dll` dans un dossier `GenreCollections` du répertoire des plugins :

   | Installation | Dossier |
   | --- | --- |
   | Docker | `/config/plugins/GenreCollections/` |
   | Linux (paquet) | `/var/lib/jellyfin/plugins/GenreCollections/` |
   | Windows | `%LOCALAPPDATA%\jellyfin\plugins\GenreCollections\` |
   | macOS | `~/.local/share/jellyfin/plugins/GenreCollections/` |

3. **Redémarre Jellyfin.**

## Premiers pas

1. Ouvre **Tableau de bord → Extensions → Genre Collections**.
2. Règle les options si besoin, puis clique sur **Enregistrer**.
3. Clique sur **Synchroniser maintenant** (ou lance un scan de bibliothèque).
4. Les collections apparaissent dans ta bibliothèque **Collections**, et les nouvelles rangées sur l'accueil. Si l'accueil ne change pas, recharge la page (Ctrl + F5).

La synchronisation est aussi disponible dans **Tableau de bord → Tâches planifiées → Genre Collections**.

## Configuration

### Collections

| Option | Par défaut | Description |
| --- | --- | --- |
| Créer et mettre à jour les collections | Activé | Active ou coupe la gestion des collections |
| Inclure les films / les séries | Activé / Activé | Types de contenus rangés |
| Séparer films et séries | Désactivé | Une collection mixte, ou deux par genre |
| Nom d'une collection mixte | `{0}` | `{0}` est remplacé par le genre |
| Nom d'une collection de films / de séries | `{0} (Films)` / `{0} (Séries)` | Utilisés si films et séries sont séparés |
| Nombre minimum de titres par genre | 3 | En dessous, pas de collection |
| Genres exclus | Téléfilm, TV Movie | Ces genres n'ont jamais de collection |
| Alias de genres | voir ci-dessous | Fusionne ou découpe des genres |
| Supprimer les collections devenues inutiles | Activé | Supprime une collection de genre qui n'a plus assez de titres |

**Alias de genres.** Une règle par ligne, au format `Source=Cible` ou `Source=Cible1,Cible2`. Voici les règles par défaut :

```
Action & Adventure=Action,Aventure
Sci-Fi & Fantasy=Science-Fiction,Fantastique
Science-Fiction & Fantastique=Science-Fiction,Fantastique
Science Fiction=Science-Fiction
War & Politics=Guerre
Guerre & Politique=Guerre
```

### Écran d'accueil

| Option | Par défaut | Description |
| --- | --- | --- |
| Ajouter des rangées sur l'écran d'accueil | Activé | Active ou coupe toutes les rangées |
| Position | Au-dessus | Au-dessus ou en dessous des rangées Jellyfin (« Reprendre », « À suivre »…) |
| Nouveaux films / Séries avec de nouveaux épisodes | Activé | Rangées des nouveautés |
| Collections par genre | Activé | Rangée listant toutes les collections de genre |
| Une rangée par genre | Activé | Une rangée de titres par genre |
| Nombre maximum de rangées de genre | 6 | Limite le nombre de rangées de genre |
| Genres à afficher en premier | vide | Ordre des rangées de genre. Les autres suivent, du plus fourni au moins fourni |
| Ordre dans les rangées de genre | Aléatoire | Aléatoire, derniers ajoutés, mieux notés ou date de sortie |
| Titres par rangée | 20 | Entre 4 et 60 |
| Masquer les titres déjà vus | Désactivé | Dans les rangées de genre uniquement |

## Questions fréquentes

<details>
<summary><b>Les rangées n'apparaissent pas sur l'accueil</b></summary>

Pour afficher les rangées, le plugin ajoute une seule ligne `<script>` dans le fichier `index.html` du client web au démarrage de Jellyfin, et la retire à l'arrêt. Jellyfin doit donc pouvoir écrire dans ce fichier.

- **Docker (image officielle, lancée en root)** : rien à faire.
- **Docker avec `--user`** ou **installation Linux** : donne les droits d'écriture à l'utilisateur de Jellyfin, par exemple :
  ```bash
  sudo chown jellyfin /usr/share/jellyfin/web/index.html
  ```

Si l'écriture est impossible, un avertissement `Cannot write ... index.html` apparaît dans les logs. Les collections fonctionnent quand même. Pense aussi à vider le cache du navigateur (Ctrl + F5).
</details>

<details>
<summary><b>Un genre n'a pas de collection</b></summary>

Il faut au moins le nombre minimum de titres (3 par défaut), et le genre ne doit pas être dans la liste des exclus. Les genres viennent des métadonnées (TMDb, NFO…). Vérifie-les dans la fiche du film ou de la série.
</details>

<details>
<summary><b>J'ai des doublons comme « Science Fiction » et « Science-Fiction »</b></summary>

Ajoute un alias, par exemple `Science Fiction=Science-Fiction`, puis relance la synchronisation.
</details>

<details>
<summary><b>Est-ce que ça change le style de mon thème (Abyss, etc.) ?</b></summary>

Non. Le plugin n'ajoute aucun CSS. Les rangées reprennent le style de ton thème.
</details>

<details>
<summary><b>Est-ce que mes collections faites à la main sont touchées ?</b></summary>

Non. Le plugin ne gère que les collections qu'il a créées. Elles portent un identifiant interne `GenreCollections`, visible dans l'éditeur de métadonnées.
</details>

## Désinstallation

1. **Tableau de bord → Extensions → Genre Collections → Désinstaller**, puis redémarre Jellyfin. La ligne ajoutée à `index.html` est retirée à l'arrêt du serveur.
2. Les collections de genre restent. Pour les retirer, supprime-les depuis la bibliothèque **Collections**.

## Développement

Il faut le [SDK .NET 10](https://dotnet.microsoft.com/download).

```bash
dotnet test
dotnet publish Jellyfin.Plugin.GenreCollections -c Release -o artifacts
```

Structure du projet :

```
Jellyfin.Plugin.GenreCollections/
├── Api/             # Endpoints utilisés par l'accueil et la page de config
├── Configuration/   # Options et page de configuration
├── Services/        # Synchronisation des collections, règles de genres, injection du script
├── Tasks/           # Tâche planifiée et tâche après scan
└── Web/             # Script des rangées d'accueil (sans CSS)
tests/               # Tests unitaires
```

**Publier une version** : pousse un tag, par exemple `git tag v1.0.0 && git push origin v1.0.0`. Le workflow GitHub Actions compile, publie la release avec le zip et met à jour `manifest.json`, donc la nouvelle version apparaît dans le catalogue Jellyfin.

## Licence

[GPL-3.0](LICENSE), comme le modèle de plugin Jellyfin.
