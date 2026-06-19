# Pipeline Layout Transition

This directory is transitioning from a legacy layout to a new structure. Both layouts coexist during
the migration.

## Target Layout

```text
eng/pipelines/
├── ci/                    # CI pipelines (package, test, stress, kerberos, …)
│   ├── kerberos/
│   │   └── sqlclient-ci-kerberos-pipeline.yml
│   ├── package/
│   │   └── sqlclient-ci-package-pipeline.yml
│   ├── shared/            # Templates shared across CI pipelines only
│   ├── stress/
│   │   └── sqlclient-ci-stress-pipeline.yml
│   └── test/
│       └── sqlclient-ci-test-pipeline.yml
├── github/                # GitHub sync pipeline
│   └── sqlclient-github-sync-pipeline.yml
├── official/              # Official / release pipelines (renamed from onebranch/)
│   ├── sqlclient-official-pipeline.yml
│   └── sqlclient-non-official-pipeline.yml
├── pr/                    # Pull-request validation pipelines
│   └── sqlclient-pr-pipeline.yml
├── shared/                # Templates shared across pr/, ci/, etc
└── README.md
```

Every leaf directory above (each pipeline group with entry files, plus the `shared/` directories)
also contains the standard template subdirectories, omitted above for brevity:

```text
<group>/
├── …entry pipeline yml(s)…   # omitted for shared/ directories
├── jobs/
├── scripts/
├── stages/
├── steps/
└── variables/
```

### Naming Convention

Pipeline entry files follow the pattern:

```text
sqlclient-<pipeline-name>-pipeline.yml
```

Each directory may contain multiple entry files when there are distinct pipelines in the same
logical group (e.g. `official/` has separate official and non-official pipelines). In `ci/`, each
pipeline (package, test, stress, kerberos) lives in its own subdirectory with its own `jobs/`,
`stages/`, `steps/`, `variables/`, and `scripts/`.

### `shared/` Directories

Templates that are consumed by multiple top-level pipelines live in the top-level
`shared/`. A template that is only used within a single pipeline group stays in that group's own
subdirectory, for example `ci/shared/`

## Legacy Layout (being removed)

```text
eng/pipelines/
├── dotnet-sqlclient-ci-core.yml
├── dotnet-sqlclient-ci-package-reference-pipeline.yml
├── dotnet-sqlclient-ci-project-reference-pipeline.yml
├── sqlclient-pr-package-ref-pipeline.yml
├── sqlclient-pr-project-ref-pipeline.yml
├── github-sync-pipeline.yml  # ⚠ Legacy — moving to github/
├── common/                   # ⚠ Legacy — shared templates
│   ├── templates/
│   ├── steps/
│   └── variables/
├── jobs/                     # ⚠ Legacy — job templates
├── stages/                   # ⚠ Legacy — stage templates
├── libraries/                # ⚠ Legacy — variable templates
├── kerberos/                 # ⚠ Legacy — moving to ci/
├── stress/                   # ⚠ Legacy — moving to ci/
└── onebranch/                # ⚠ Legacy — renaming to official/
```

## Migration Status

| Legacy Location | Target Location | Status |
| --------------- | --------------- | ------ |
| Root entry YAMLs (`dotnet-sqlclient-ci-*.yml`, `sqlclient-pr-*.yml`) | `ci/`, `pr/` | In progress |
| `github-sync-pipeline.yml` | `github/` | Not started |
| `common/` | `shared/` (most files to be deleted) | In progress |
| `jobs/` | `pr/jobs/`, `ci/jobs/`, `shared/jobs/` | In progress |
| `stages/` | `pr/stages/`, `ci/stages/`, `shared/stages/` | In progress |
| `libraries/` | `shared/variables/` | Not started |
| `kerberos/` | `ci/` | Not started |
| `stress/` | `ci/` | Not started |
| `onebranch/` | `official/` | Not started |

## Guidelines

1. **New pipelines** go in `pr/`, `ci/`, or `official/`.
2. **New shared templates** go in `shared/`.
3. **Do not add files** to `common/`, root-level `jobs/`, `stages/`, `libraries/`, or `onebranch/`.
4. When moving a template, update all `@self` references that point to it.
5. Legacy files will be deleted once all references are migrated. Most legacy shared templates are
   not expected to survive the migration.
