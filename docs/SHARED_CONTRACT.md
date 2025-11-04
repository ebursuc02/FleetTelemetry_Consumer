# Shared Contract (v1.0)

> Shared contract for **producers** and the **consumer**.

---

## 0) Scope
This contract defines:
- directory/prefix layout and naming
- data file formats (JSON Lines or CSV)
- sidecar metadata (`.meta.json`)
- atomic handoff rules (temp → rename → sidecar commit)
- minimal consumer & producer obligations

---

## 1) Directory & Naming
**Canonical layout (filesystem or bucket prefixes):**
```
    inbox/      # producers write here
    archive/    # consumer moves processed files here (date-partitioned)
    error/      # consumer moves rejected files here (+ .error.json)
```

**Final data filename:**
```
telemetry_YYYYMMDD_HHMMSS_{vehicleId}.jsonl
# example
after-rename: telemetry_20251028_101045_V042.jsonl
sidecar:      telemetry_20251028_101045_V042.jsonl.meta.json
```
- One **vehicle per file**. If multiple vehicles must appear, each record still MUST include `vehicleId`.

---

## 2) Atomicity & Handoff
**Filesystem:**
1. Producer writes to temp in **same directory**:
   ```
   inbox/{finalName}.tmp
   ```
2. Flush, then **rename** temp → `{finalName}` (same dir).
3. Write sidecar `{finalName}.meta.json` (commit signal).
4. Consumer **ignores** `*.tmp` and **only** processes when **both** `{finalName}` **and** sidecar exist.

---

## 3) Data File Format
Producers use **JSON Lines**.

### 3.1 JSON Lines
- Encoding: **UTF-8** (no BOM)
- **One JSON object per line**
- Timestamps in **UTC**
- Example line:
```json
{
    "vehicleId": "V042",
    "tsUtc": "2025-10-28T09:00:25Z",
    "speedKmh": 58.0,
    "fuelPct": 73.5,
    "coolantTempC": 92.1
}
```
 
**Informative JSON schema (records):**
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["vehicleId", "tsUtc"],
  "properties": {
    "vehicleId": {"type": "string"},
    "tsUtc": {"type": "string", "format": "date-time"},
    "speedKmh": {"type": ["number", "null"]},
    "fuelPct": {"type": ["number", "null"], "minimum": 0, "maximum": 100},
    "coolantTempC": {"type": ["number", "null"]},
    "oilTempC": {"type": ["number", "null"]},
    "engineRpm": {"type": ["number", "null"]},
    "co2": {"type": ["number", "null"]},
  },
  "additionalProperties": false
}
```

---

## 4) Sidecar Metadata (`{finalName}.meta.json`)
- Encoding: UTF-8
- **Required fields:**
```json
{
  "version": "1.0",
  "createdUtc": "2025-10-28T11:03:22Z",
  "recordCount": 12345,
  "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "encoding": "utf-8",
}
```

**Informative JSON schema (sidecar):**
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["version", "createdUtc", "recordCount", "sha256"],
  "properties": {
    "version": {"type": "string"},
    "createdUtc": {"type": "string", "format": "date-time"},
    "recordCount": {"type": "integer", "minimum": 0},
    "sha256": {"type": "string", "pattern": "^[a-f0-9]{64}$"},
    "encoding": {"type": ["string", "null"]},
  },
  "additionalProperties": false
}
```

---

## 5) Producer Obligations
- **Atomicity:** temp write → same-dir rename to final → write sidecar (commit).
- **Accuracy:** `recordCount` SHOULD equal actual lines (JSONL).
- **Time:** `createdUtc` and data `tsUtc` MUST be UTC (suffix `Z`).

## 6) Consumer Obligations
- Pairing: process only when **both** data and sidecar exist.
- Checksum: recompute **SHA-256** of data; MUST match sidecar.
- Idempotency: track processed pairs by `sha256`; skip on duplicates.
- Streaming parse: no full-file loads;
- Archival: on success → move pair to `archive/yyyy/MM/dd/` (names unchanged).
- Errors: on failure → move pair to `error/` with `{finalName}.error.json` detailing reason & when.

---

## 7) Examples
**Final files in `inbox/`:**
```
telemetry_20251028_101045_V042.jsonl
telemetry_20251028_101045_V042.jsonl.meta.json
```
**Temp flow (producer):**
```
inbox/telemetry_20251028_101045_V042.jsonl.tmp  ← write
→ rename to final
→ write sidecar (commit)
```

---

**Change log**
- v1.0 — initial version (data formats, sidecar, atomicity, validator)
