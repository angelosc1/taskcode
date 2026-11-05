# Taskcode branding

The project owner supplied the Taskcode mark as a transparent PNG.

- `taskcode-source.png`: unmodified source artwork.
- `taskcode.png`: centered 512-pixel mark with compact transparent margins, used in the repository documentation.
- `taskcode.ico`: Windows icon with 16, 20, 24, 32, 40, 48, 64, 128, and 256-pixel entries, embedded in the executable and used for the tray and settings window.

The build regenerates the derived PNG and ICO from the source using `scripts/BrandingAssets.cs`. It preserves the supplied shapes and colors, adjusting only transparent margins and image size.

The supplied file named `icon.svg` contains PNG bytes, not SVG markup, so it is not treated as a vector asset or published with an incorrect format.
