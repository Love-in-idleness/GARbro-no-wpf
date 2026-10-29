# Portable scheme data

`Formats.json.gz` contains only the scheme groups used by the migrated providers,
plus the legacy game-name map. It is generated from the repository's existing
`ArcFormats/Resources/Formats.dat`; it contains no local user game files or credentials.
The original data and license remain intact.

To reproduce it, download `Formats.json` from this repository's legacy Windows CI
artifact and run `python3 tools/prepare-schemes.py /path/to/Formats.json`.
The Windows export uses the original .NET Framework serializer. The modern reader
uses JSON with a provider type allowlist and never enables BinaryFormatter.

Source data version: 148. Export verified at commit
`c6ed08e84f71c3e3970b1215261065e3965a28b5` (Windows workflow run 40933520897).
Importing a scheme establishes data compatibility, not verified decryption for
every game represented in the database. Real-game encrypted fixtures remain necessary.
