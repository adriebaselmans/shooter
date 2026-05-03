---
name: online-material-sourcing
description: Source textures/materials from reliable online providers, record licensing, and integrate them into this repository in a repeatable way.
---

# Online Material Sourcing

Use this skill when a task requires importing new textures or material assets from online sources.

## Goals
- Find a good source material quickly.
- Prefer permissive licensing, ideally CC0.
- Avoid one-off conversational knowledge; make imports traceable and repeatable.
- Leave the repository with the asset, attribution notes, and enough context for the next agent.

## Best Providers
1. **ambientCG**
   - Best machine-friendly option in this repo.
   - Public JSON API works well from scripts.
   - Good fallback when browser/web-search tooling is flaky.
   - License target: CC0.
2. **Poly Haven**
   - Excellent quality and CC0.
   - Good for human-confirmed asset pages.
   - Direct scripted scraping may fail with 403 depending on user agent / site behavior.
3. Other sources only when clearly licensed and necessary.

## Repository-Specific Guidance
- Put imported runtime textures under `assets/textures/<theme-or-map>/`.
- Update the matching local license/credits file, usually `assets/textures/<theme-or-map>/LICENSES.md`.
- If a texture is only for a single demo/map validation path, say so explicitly in the license note.
- Prefer stable, descriptive filenames such as:
  - `water_river_1k.jpg`
  - `lava_magma_1k.jpg`
  - `stone_trim_1k.jpg`

## Proven Workflow
### A. Search / identify source
- Try official API or official asset page first.
- For **ambientCG**, the API pattern is reliable:
  - `https://ambientcg.com/api/v2/full_json?type=Material&q=<query>&limit=<n>&include=previewData`
- Extract:
  - asset id
  - display name
  - source URL
  - usable image URL
  - license

### B. Download
- Use `bash` with `curl -L` or a short Python `urllib` script.
- Prefer a real diffuse/albedo map when available.
- If the full material package is awkward to automate but the preview image is acceptable for a small demo, that is allowed — but document it clearly in `LICENSES.md`.

### C. Record provenance
Always add an entry to the local license file with:
- filename
- upstream asset id/name
- source URL
- provider
- what exact file family was downloaded (diffuse, preview, 1k jpg, etc.)
- intended use in this repo

### D. Integrate
- Point the map/runtime/editor material reference at the imported file.
- If the repo supports authored material properties, prefer those over hidden heuristics.
- For demo-map additions, make the placement read clearly in-world (for example, water should sit in a basin, not float above the floor).

### E. Validate
- Run the cheapest meaningful build.
- Run the relevant smoke path if visuals depend on the import.

## Known Gotchas
### Poly Haven 403
- Direct automated fetches of some Poly Haven asset pages may return **403 Forbidden**.
- Do not waste time fighting this if ambientCG can provide a suitable CC0 alternative.
- If Poly Haven is still desired, prefer a manually confirmed source page or a different fetch route.

### Preview vs full material
- A preview image is acceptable for a quick visual demo when documented.
- It is not equivalent to a full authored material set.
- If using a preview, explicitly say so in the license note.

### Water / lava
- A good water or lava result is not just the texture.
- Pair the imported image with authored material behavior (flow, distortion, fresnel, emissive, etc.) so the asset reads correctly in-engine.

## Expected Output
- Imported asset file(s)
- Updated `LICENSES.md`
- Clear repository-local path(s)
- Short summary of source, license, and why this asset was chosen
