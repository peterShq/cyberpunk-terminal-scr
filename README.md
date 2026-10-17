# Cyberpunk Terminal Screensaver (.scr)

A Windows screen saver that looks like a machine busy doing real work — scrolling
build logs, cluster monitoring, database flame graphs, CI pipelines.

Everything on screen is **generated live, every frame**. Nothing is a recording,
nothing is a static image loop.

![Screen saver in action](docs/screenshot-saver.png)

---

## Why

Waiting for a build is dead time. Instead of a black screen, why not something that
looks like a terminal that's actually working?

Written as a **native Windows `.scr`** so it appears in the system's own screen saver
list alongside "Blank", "Mystify" and the rest — not some third-party window floating
on top.

## Features

- **Real `.scr`** — shows up in *Settings → Personalization → Lock screen → Screen saver*
- **78 KB, single file** — no runtime, no Python, no VC++ redistributable, nothing to install
- **Zero network requests** — reads no files, collects nothing
- **Live content** — logs, node status, throughput and flame graphs all animate
- **Six settings with live preview** — change anything and see it immediately
- **Multi-monitor** — one window per display with independent phases, or primary-only
- **Exits on any input** — exactly like the built-in Windows screen savers

## Install

Download or clone, then:

```
1. build\NexusCore.scr  →  right-click → Properties → tick "Unblock" → OK
2. src\install.cmd       →  double-click, allow administrator access
3. Press Enter when asked "Make it the ACTIVE screen saver now?"
4. Settings → Personalization → Lock screen → Screen saver → pick "Cyberpunk Terminal"
```

Then click **Preview** to see it. You can also enable it from the Run dialog:

```
Win + R  →  screensaver.cpl
```

To uninstall, double-click `src\uninstall.cmd`. It only clears your system setting if
this screen saver is the one currently selected, so it will not touch another one you
installed.

> Chinese users: see [使用教程.md](使用教程.md) for the full guide.

## Settings

Double-click the `.scr`, or click **Settings** in the screen saver dialog. The lower
half of the window is a **live preview** — what you see is what you get.

| Setting | Options | Default |
|---|---|---|
| Display scope | All monitors / Primary only | All monitors |
| Font | Consolas / Cascadia Mono / Lucida Console / Courier New | Consolas |
| Font size | Auto (fits screen height) or 8–48 | Auto |
| Animation speed | Slow / Normal / Fast | Normal |
| Content density | Low / Medium / High | High |
| Frame rate | 15 / 30 / 60 FPS | 30 |

## Performance

Measured on 16 logical cores, 1600×900, high density (single-core CPU):

| Frame rate | CPU | GDI / USER growth |
|---|---|---|
| 15 FPS | 9.0% | 0 |
| **30 FPS (default)** | **14.5%** | 0 |
| 60 FPS | ~24% | 0 |

**30 FPS is the sweet spot.** 60 FPS costs noticeably more CPU for very little visual
gain — the bulk of it is ClearType text rasterization. On laptops or 4K displays where
things feel warm, drop to 15 FPS and low density.

## Building from source

You only need the .NET Framework compiler that ships with Windows. No Visual Studio,
no .NET SDK, no NuGet.

```bat
cd src
build.cmd
```

That produces `build\NexusCore.scr` (~78 KB).

Two details worth knowing:

- **`rc.exe` is optional.** It comes with the Windows SDK. If present, the screen saver
  lists itself as "Cyberpunk Terminal" instead of the raw filename. If missing, the
  build still works — you just get the filename in the list.
- **Sources are C# 5.0 only.** The bundled compiler is old, so no string interpolation,
  no `?.`, no `nameof`.

## How it's drawn

The chrome — panel borders, bar charts, sparklines, box charts, progress bars — is
drawn with GDI+ primitives rather than box-drawing characters. Consolas does not
reliably cover the rounded box-drawing range (U+256D and up), and text rendering for
chrome would be both slower and uglier. Only real text goes through a font.

Content lives in `Content.cs` (data pools and ~30 generators) and `Panels.cs` (panel
layout and state). The layout algorithm recursively splits the screen into panels and
tries to keep them stable across frames, so panels don't jump around while you watch.

## Troubleshooting

**Preview is black.** Wait 2–3 seconds — the first frame may not have rendered yet.
Still black? Check that density and font size aren't at extreme values, and that
`build\` and `src\` are kept together as folders.

**Install says "device or resource busy".** Something is holding the file — most often
an open settings or preview window. Close them, or run
`taskkill /F /IM NexusCore.scr`, then retry.

**Antivirus blocked it.** Normal for a `.scr` — it *is* an executable. Unblock the file
in its Properties and whitelist it.

**Not in the list?** Close and reopen the screen saver settings window; the list is
populated on open.

**Does it lock my computer?** No. A screen saver is not a security boundary — an admin
or Task Manager can always end the process. For unattended locking, use Windows' own
*On resume, display logon screen* option. This saver is compatible with it.

## Compatibility

| Environment | Status |
|---|---|
| Windows 10 21H2 x64 | ✅ Tested |
| Windows 11 x64 | ✅ Tested |

Requires .NET Framework 4.8, which ships with Windows 10 1803 and later.

## License

MIT — see [LICENSE](LICENSE).

The content pools (project names, namespaces, file paths, log messages) are fictional
and invented for this project.
