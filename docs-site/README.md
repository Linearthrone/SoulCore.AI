# SoulCore searchable handbook

Local VitePress site over `docs/handbook`, `docs/runbooks`, and `PROP_NUMBERING.md`.

```bash
cd docs-site
npm install
npm run docs:dev
```

Open the printed URL. Use the **search** box for keywords.

`npm run docs:build` runs `sync-content.sh` first (copies from `docs/`), then builds static output under `.vitepress/dist/`.

Edit markdown under `docs/handbook/` or `docs/runbooks/` — not under `content/` directly (it is regenerated).
