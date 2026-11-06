# Third-party notices

## Codenotch connection approach

The C# Claude connection adapts the Windows CLI discovery/sign-in and usage-reading behavior from [Codenotch](https://github.com/vinzdg/codenotch), reviewed at commit `183fc2f835dbe46855cf1bf0f7c03e05d5f811c8`. Reference files: `windows/codenotch/src/claude_auth.rs`, `usage.rs`, and `claude_accounts.rs`. Taskcode implements its own C# UI and adapter; no Codenotch branding or application artwork is included.

MIT License

Copyright (c) 2026 Vinz

Copyright (c) 2026 Im-Midi (NG) and contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Claude symbol

- Asset: `assets/providers/claude.svg`, embedded in the executable and rendered as a vector.
- Source: [Simple Icons](https://github.com/simple-icons/simple-icons/blob/develop/icons/claude.svg).
- Retrieved: November 5, 2026.
- Asset license: CC0 1.0 Universal. A copy is included in [SIMPLE-ICONS-LICENSE.md](assets/providers/SIMPLE-ICONS-LICENSE.md).

Claude and its symbol are trademarks of Anthropic. Taskcode uses the symbol to identify the monitored provider and is not affiliated with or endorsed by Anthropic. The project's MIT license does not grant rights to third-party trademarks.

## Installer

Windows setup packages are built with [Inno Setup](https://jrsoftware.org/). Copyright (C) 1997-2026 Jordan Russell; portions Copyright (C) 2000-2026 Martijn Laan. The installer retains the engine copyright notices and website addresses. Taskcode does not claim authorship of the installer engine.
