# Translations

Taskcode ships English (`en`) and Brazilian Portuguese (`pt-BR`). **General > Language > System** is the default: it follows the Windows display language and falls back to English when no supported match exists. Choosing a language explicitly overrides automatic detection. Changes apply immediately and are saved per user.

## Add or improve a language

1. Use `en.json` as the source catalog. Its keys are the exact English messages; its values repeat those messages.
2. Edit the values in an existing translation, or create `<culture>.json` beside it, using a .NET culture name such as `es` or `de-DE`. Keep keys in English, unchanged.
3. Translate values only. The file must be a UTF-8 JSON object mapping strings to strings, with no comments, duplicate keys or trailing commas.
4. Preserve every numbered placeholder, including its count: `{0}`, `{1}`, etc. You may reorder them to suit the language. Keep provider names, percentages, units and technical identifiers accurate.
5. Open a pull request describing the language and your validation. A partial catalog is fine: omit untranslated entries or leave their values empty to use English. Do not add keys absent from `en.json`.

Example entry for Brazilian Portuguese:

```json
{
  "Resets in {0}": "Renova em {0}",
  "{0}% used / {1}% time": "{0}% de uso / {1}% do tempo"
}
```

The build embeds every JSON catalog in this directory. The language picker discovers them automatically and displays each language's native name. No language registration or C# edit is needed. This first implementation uses left-to-right layouts; right-to-left languages need a separate layout contribution.

## Review a translation

Run `scripts\build-probe.cmd --test` on Windows. Checks validate catalog keys, format placeholders, automatic detection, explicit overrides, fallback, immediate switching, preference persistence and Portuguese rendering. They produce `out/probe/settings-pt-BR.png` and `out/probe/details-pt-BR.png` using sample data.

Pull requests run these checks automatically on Windows. You can submit a translation without a local build; state that in the pull request so a maintainer can review the rendered layout.

[Brazilian Portuguese settings preview](../docs/images/settings-pt-BR.png) · [Brazilian Portuguese Details preview](../docs/images/details-pt-BR.png).

For a newly added language, select it in **General > Language** and review Appearance, Accounts, General, each provider's Details, menus and tooltips. Keep labels short enough for the compact UI. Test both weekly readings and active/idle usage estimates. Include sample-data images in the pull request if practical.

## For developers

`L10n` translates at the presentation boundary. Control bindings retain the English source so existing windows can update without changing provider data, saved enum values, control IDs or credentials. `L10n.Choice` keeps selector indices stable. `L10n.Hint` handles tooltips; custom drawing uses `L10n.T`.

Catalog entries with placeholders also match explicitly declared dynamic status formats, such as `Resets in {0}`. Keep the complete English format in the catalog and preserve its literal separators in the producer. Do not add broad catch-all templates or translate protocol fields.

The installer uses Inno Setup's bundled English and Brazilian Portuguese messages plus `[CustomMessages]` in `installer/Taskcode.iss`; it is separate from the application's JSON catalogs. External CLI/browser sign-in pages follow the provider's own language support. Diagnostic logs and repository documentation remain English.
