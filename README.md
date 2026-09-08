# Velum — SOLIDWORKS Adapter for the ISIDA Agent Platform

## Description

Velum is a SOLIDWORKS add-in and adapter for the **ISIDA** (Incremental System for Intelligent Development of Agents) adaptive agent platform. It serves as a "second pilot" for design engineers -- bridging the CAD environment with an intelligent assistant that learns from operator demonstrations and provides contextual guidance during repetitive but variable engineering workflows.

The project provides a structured workspace for managing engineering documentation: part and assembly registries, batch DXF/PDF export, material management, custom properties, and technical requirements. The agent panel is optional and can be enabled on demand.

## Architecture

The system consists of three layers:

| Layer | Purpose | User-facing |
|---|---|---|
| **SOLIDWORKS + Velum add-in** | Commands, registries, batch operations, task pane | Daily use |
| **Velum Adapter** | Session bridge between SOLIDWORKS and the ISIDA core: environment metrics, commands, confirmations | Configurable via paths and the Agent panel |
| **ISIDA Core** | Adaptive agent library (work cycle, situation memory, reaction rules) | Activated via the "Start" button on the Agent panel |

## ISIDA Core

ISIDA is the software library of an adaptive agent. In Velum, it is integrated as the core of the Agent panel -- managing the pulse cycle, experience accumulation within permitted actions, and reaction to operator instructions.

- ISIDA repository: <https://github.com/PalarmAlex/isida>
- Profile data for SOLIDWORKS is stored separately: `%ProgramData%\\VELUM\\` (not mixed with other ISIDA shells).

**Product positioning:** Velum is a "second pilot", not autopilot and not a replacement for the designer. Where Design Tables, parametric modeling, or rigid macros already work well, they should be kept. The assistant is useful in repetitive but variable chains where the full scenario cannot be defined in advance.

## MVAP Theory

**MVAP** (Model of Volitional Adaptivity of Psyche) is the theoretical foundation on which the mechanisms of individual adaptivity in ISIDA are built. In brief for practice:

- Adaptivity principles can be implemented programmatically, not only "as in a living nervous system";
- Adaptivity is described as a coordinated interaction scheme (conditions → reaction → experience reinforcement), not as a "black box that guesses the answer".

Detailed theory, publications and prototypes are available at the project website:

<https://p-mvap.ru/>

In the Velum interface, engineering terms (state, metrics, instructions, operation blocking) are used intentionally instead of theory terminology. Knowing MVAP is not required to work with registries and export; this link is for those who want to understand "why the assistant is built this way and not like a chatbot".

## What Velum Does Not Promise

- Does not replace strong general-purpose AI and does not promise fully autonomous learning without human participation.
- Learning and reinforcement of typical techniques is a joint effort: demonstration, attempt, operator correction.
- Complex geometry editing "by itself" is not a primary goal; priority goes to verifiable operations (properties, export, KB regulations, etc.).

## Technology Stack

- **Platform:** .NET Framework 4.8
- **Language:** C# 7.3
- **IDE:** Visual Studio 2022 Professional
- **Add-in Framework:** XCad (Xarial.XCad.SolidWorks)
- **Build:** MSBuild (bundled with VS 2022)

## Project Structure

```
Velum/
  VelumAddIn.cs              # Add-in entry point (SwAddInEx)
  velum.csproj               # Project (C# 7.3, .NET 4.8)
  velum.sln                  # Solution
  packages.config            # NuGet dependencies
  Configuration/             # Settings and adapter package
  ReactiveCore/              # Reactive core, recipe engine
    Export/                  # DXF and PDF export
    RecipeExecutor*.cs       # Recipe execution
    *.cs                     # Property mapping, metrics
  SolidHomeostasis/          # Monitoring and coordinators
    MetricProbes/            # Metric probes
    *.cs                     # Command buffer, SW events
  Common/                    # Common components
    AssemblyRegistry/        # Assembly registry, BOM
    ProductRegistry/         # Product registry
    TechRequirements/        # Tech requirements
    DocumentProperties/      # Document properties
    Material/                # Material management
    Export/                  # Export dialogs
    *.cs                     # Agent task pane, forms
  Isida/                     # ISIDA platform integration
  Properties/                # Project properties
  icons/                     # Interface icons
  docs/                      # Documentation
    help/                    # Help (HTML, forms)
    adapter-package/         # Adapter package schema
    Settings/                # Part and assembly templates
  tools/                     # Utility scripts
  lib/                       # SolidWorks Interop assemblies
  .gitignore                 # Git ignore rules
```

## Dependencies

### External (not in repository)

| Dependency | Path |
|---|---|
| **isida** | `..\\..\\ISIDA\\Programms\\isida\\bin\\$(Configuration)\\isida.dll` |
| **SymbiontEnv.Contract** | `..\\..\\ISIDA\\Programms\\app\\SymbiontEnv.Contract\\bin\\$(Configuration)\\SymbiontEnv.Contract.dll` |

Both assemblies are built from external repositories and must be available before building Velum.

### NuGet packages

Restored from `packages.config`:
- `Xarial.XCad.0.8.1`
- `Xarial.XCad.SolidWorks.0.8.1`
- `Xarial.XCad.Toolkit.0.8.1`

## Building

### Requirements

- **Visual Studio 2022 Professional** (or higher)
- **.NET Framework 4.8**
- **SolidWorks** (required to run the add-in)

### Build command (MSBuild)

```powershell
& "C:\\Program Files\\Microsoft Visual Studio\\2022\\Professional\\MSBuild\\Current\\Bin\\MSBuild.exe" D:\\VELUM\\Velum\\Velum.csproj /t:Build /p:Configuration=Debug /v:q /nologo
```

> **Important:** Do not use `dotnet build` -- the project does not support .NET SDK for building.

### Step by step

1. Ensure external assemblies `isida.dll` and `SymbiontEnv.Contract.dll` are available.
2. Open the solution `velum.sln` in Visual Studio 2022.
3. Restore NuGet packages (VS does this automatically on open).
4. Build the project (`Ctrl+Shift+B`).
5. Output: `bin\\Debug\\velum.dll` (or `bin\\Release\\velum.dll`).

## Add-in Installation

The add-in registers via the XCad `SwAddInEx` mechanism. After building:

1. Copy `velum.dll` to the SOLIDWORKS add-in loading directory.
2. Configure `Settings.xml` in `%ProgramData%\\VELUM\\Settings\\`.
3. Launch SOLIDWORKS -- the add-in appears as commands on the ribbon and the **Agent** task pane.

Help for all forms is available in `docs/help/` and is copied to `C:\\ProgramData\\VELUM\\help\\` on build.

## License

[Add license information]

## Authors

Velum / ISIDA development team.