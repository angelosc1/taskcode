# Contributing to Taskcode

Bug reports, fixes and translations are welcome. Please keep issues, pull requests, code comments and project documentation in English. Translated application text belongs in the language catalogs.

## Report a problem

Include the Taskcode version, Windows build, display scaling, steps to reproduce and expected behavior. For quota problems, state the provider and window. Remove account identities from screenshots. Never upload credentials, account profiles, raw session transcripts or your settings file.

## Contribute a translation

See the [translation guide](locales/README.md). You can edit JSON files directly on GitHub and open a pull request without changing C# code. Partial translations are welcome; untranslated messages fall back to English. Please mention the language and whether you were able to review it in the running app.

## Code changes

Read [Development](docs/DEVELOPMENT.md), keep changes focused and run `scripts\build-probe.cmd --test` on Windows. Include the relevant validation results in your pull request. UI changes should include images rendered with sample data, without personal account information.

Keep English source messages in `locales/en.json` and route new UI text through the localization layer. Preserve provider protocol values, settings IDs, command names, paths and security checks. Do not translate diagnostics or program identifiers.

Contributions are provided under the project's [MIT license](LICENSE). Provider artwork has separate notices in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
