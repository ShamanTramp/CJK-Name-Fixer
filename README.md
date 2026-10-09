# CJK Name Fixer

![CJK Name Fixer illustration](assets/cjk-name-fixer-cover.png)

**CJK Name Fixer** is a Jellyfin plugin that replaces person names written in Chinese, Japanese, or Korean characters with their corresponding Latin names from TMDb. It is designed to correct the names of actors and other people associated with media in your library.

## Features

- Scans existing person entries when manually started from Jellyfin's scheduled tasks.
- Can process people associated with a new movie, episode, or series when it is added to the library.
- Changes a name only when a TMDb match is available and the name field is not locked.
- Updates the credits on associated media along with the name, preserving links to the person entry.
- Caches TMDb results, including searches with no match, for 12 hours.
- Uses Jellyfin's TMDb client by default. You can configure a personal TMDb API key to use your own quota.
- Lets you set the delay between TMDb requests to limit their frequency.

The plugin does not run periodic checks. Scanning an existing library is a manual task; processing new media is enabled by default and can be disabled in the settings.

## Installation

In Jellyfin, open **Dashboard → Plugins → Repositories**, then add the repository manifest:

```text
https://raw.githubusercontent.com/ShamanTramp/CJK-Name-Fixer/main/manifest.json
```

Save the repository, open the plugin catalog, select **Available** if needed, then install **CJK Name Fixer**. Restart Jellyfin when the installation is complete. You can also download the plugin from [GitHub Releases](https://github.com/ShamanTramp/CJK-Name-Fixer/releases).

Jellyfin must be able to access the manifest and release files on GitHub to update the repository.

## Configuration and usage

In **Dashboard → Plugins → CJK Name Fixer**, configure the following options:

| Option | Default | Description |
| --- | --- | --- |
| Check names when media is added | Enabled | Processes people associated with supported new media. |
| Delay between TMDb requests | 500 ms | Sets the minimum delay between TMDb requests. |
| Personal TMDb API key | Empty | Optional. When provided, it is used instead of Jellyfin's TMDb client. |

To scan media already in your library, run **Fix CJK Person Names** from **Dashboard → Scheduled Tasks**.

## License

GPL-3.0-only. See [LICENSE](LICENSE).
