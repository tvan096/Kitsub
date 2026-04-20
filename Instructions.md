# Instructions

## Getting help

```bash
kitsub --help
kitsub <command> --help
```

## Build and run from source

```bash
dotnet build Kitsub.sln
```

```bash
dotnet run --project src/Kitsub.Cli -- --help
```

## Examples

### Example: Inspect a media file

**Inputs:**
- `INPUT_FILE` — path to a media file (placeholder)

**Command:**

```bash
kitsub inspect "INPUT_FILE"
```

**Result:** Track metadata is printed to the console.

### Example: Generate a MediaInfo JSON report

**Inputs:**
- `INPUT_FILE` — path to a media file (placeholder)

**Command:**

```bash
kitsub inspect mediainfo "INPUT_FILE"
```

**Result:** A JSON report is written under `./reports/mediainfo/<YYYYMMDD>/` with a generated filename.

### Example: Mux subtitles into an MKV

**Inputs:**
- `INPUT_MKV` — input MKV file (placeholder)
- `SUB_FILE` — subtitle file (placeholder)
- `OUTPUT_MKV` — output MKV file (placeholder)

**Command:**

```bash
kitsub mux --in "INPUT_MKV" --sub "SUB_FILE" --lang eng --title "English" --default --out "OUTPUT_MKV"
```

**Result:** An MKV file is written to the output path.

### Example: Burn subtitles into a video

**Inputs:**
- `INPUT_FILE` — input media file (placeholder)
- `SUB_FILE` — subtitle file (placeholder)
- `OUTPUT_FILE` — output media file (placeholder)

**Command:**

```bash
kitsub burn --in "INPUT_FILE" --sub "SUB_FILE" --out "OUTPUT_FILE" --crf 18 --preset medium
```

**Result:** A video file is written to the output path.

### Example: Extract a subtitle track

**Inputs:**
- `INPUT_FILE` — input media file (placeholder)
- `TRACK_SELECTOR` — subtitle track selector (placeholder)
- `OUTPUT_SUB` — output subtitle file (placeholder)

**Command:**

```bash
kitsub extract sub --in "INPUT_FILE" --track "TRACK_SELECTOR" --out "OUTPUT_SUB"
```

**Result:** A subtitle file is written to the output path.

### Example: Translate a subtitle file

**Inputs:**
- `INPUT_SUB` — input subtitle file (placeholder)
- `OUTPUT_SUB` — output subtitle file (placeholder)

**Command:**

```bash
kitsub translate sub --in "INPUT_SUB" --out "OUTPUT_SUB" --to cs
```

**Result:** A translated subtitle file is written to the output path.

## CLI reference

| Command | Purpose |
| --- | --- |
| `inspect` | Inspect media file. |
| `mux` | Mux subtitles into MKV. |
| `burn` | Burn subtitles into video. |
| `fonts attach` | Attach fonts to MKV. |
| `fonts check` | Check fonts in MKV. |
| `extract audio` | Extract audio track. |
| `extract sub` | Extract subtitle track. |
| `extract video` | Extract video track. |
| `convert sub` | Convert subtitle file. |
| `translate sub` | Translate subtitle file with OpenAI. |
| `tools status` | Show resolved tool paths. |
| `tools fetch` | Download and cache tool binaries. |
| `tools clean` | Delete extracted tool cache. |
| `release mux` | Release mux for MKV files. |
| `config path` | Show resolved configuration paths. |
| `config init` | Initialize the default configuration file. |
| `config show` | Display configuration files. |
| `doctor` | Run diagnostics and tool checks. |
