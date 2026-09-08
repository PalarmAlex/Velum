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