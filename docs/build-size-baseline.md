# Build-size baseline (M0)

Measured on 2026-10-11 from commit `b3de17e` plus the uncommitted M0 changes, with Unity 6000.3.25f1. The content is just the M0 test scene: a floor plane, a cube, a camera and a light. These numbers are the cost of the engine, URP and the Input System alone, before any game content.

| Build | Profile | Size | Notes |
|---|---|---|---|
| Web, download (`Build/`) | Web Release | **16.1 MB** (16,452 KiB) | Gzip + decompression fallback (`.unityweb` files). **This is what players download.** |
| Web, whole folder | Web Release | 16.1 MB (16,497 KiB) | |
| Web, download (`Build/`) | Web Dev | 106.8 MB (109,360 KiB) | Development builds are **not compressed** and keep debug symbols (`web.wasm` alone is 99 MB). Use them locally, not on itch.io. |
| Android APK | Android Dev | 43.0 MB (45,053,627 bytes) | IL2CPP, ARM64, development build |
| Android AAB | Android Release | not measured yet | First measured with `tools/ci.sh --release` |

The proposed web budget is 50 MB compressed (plan, M6). The M0 release download uses about a third of it.

`tools/ci.sh` appends every run to `artifacts/build-sizes.csv` (not committed). Compare new runs against this table.
