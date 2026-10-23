# Product Vision

## Thesis

**Hey Computer is an open-source, local-first Windows Jarvis that remembers transparently and delegates work to whichever agent is best.** It is not another general chat window and should not rebuild specialist coding, browser, or automation agents. It provides the durable relationship layer: voice, personality, context, routing, permissions, budget awareness, task supervision, and understandable results.

## The persistent brain

The user speaks only with the Conversation Agent. It stays responsive, listens more than it talks, and preserves a stable personality. Beside it, the Orchestration Agent turns conversational intent into bounded tasks and coordinates disposable workers. The Context Agent independently maintains the local memory vault from completed turns and verified outcomes. Separating these roles prevents long work from freezing conversation and prevents worker output from silently becoming identity or fact.

This three-role design remains the long-term architecture, but completing every contract and recovery mechanism must not block a functional prototype. The first implementation may use thinner in-process boundaries as long as its interfaces can evolve toward the persistent Conversation, Orchestration, and Context roles.

## Local-first meaning

Local-first does not require local model inference. Canonical memory, task state, permissions, credentials, audit history, and configuration live on the user’s Windows computer in inspectable formats. Cloud models may process requests when selected, but the product must disclose what leaves the device. Future optional local models may perform light routing, summarization, retrieval, or classification on hardware that can support them.

## User-owned intelligence layer

Hey Computer—not any model vendor—owns the durable assistant. Identity, personality, memory, current tasks, permissions, computer-control capabilities, audit history, and provider-independent context remain consistent when a user changes models. OpenAI, xAI, Anthropic, local models, and future providers plug into explicit model and tool contracts as replaceable reasoning engines. Changing the engine must not force the user into that vendor’s application, erase continuity, or remove access to the local computer capabilities the user has already configured.

## Transparent continuity

Continuity comes from explicit memory rather than one enormous hidden conversation. Human-readable Markdown and structured metadata hold durable facts, decisions, projects, tasks, and dated summaries. A derived SQLite index supports fast retrieval and can be rebuilt. Every durable memory should have a source, timestamp, confidence, and user-visible correction or deletion path.

## Delegation contract

Every worker receives an objective, permitted capabilities, model choice, budget, deadline, cancellation path, and expected result schema. The orchestrator limits concurrency, prevents obvious duplication, validates completion evidence, and asks before continuing when a configured usage threshold is low. Workers are task-scoped and do not speak as the main personality or directly rewrite durable memory.

## Computer-use direction

Capable computer control is the primary near-term function. The product should use the most reliable interface available: direct application APIs, MCP, shell or provider integrations first; Playwright-style browser control next; Windows UI Automation, COM, and Win32 for native applications; and visual clicking only when structured interfaces fail. The first prototype should prove a useful, narrowly scoped action instead of waiting for a comprehensive capability broker. Every action must remain locally visible and auditable, with a hard stop available at all times. Destructive actions and actions involving external communication, credentials, purchases, uploads, or security settings still require explicit confirmation.

## User-testable application

The immediate product milestone is something the user can actually run: an unsigned portable Windows executable with a clickable tray interface, clear running and activity states, Start, Pause, Stop, and hard-stop controls, and one working computer-control path. A minimal always-on-top floating panel provides glanceable state across applications, can become click-through when desired, and opens the full dashboard or controls when clicked. The same dashboard is accessible from the tray and contains activity, tasks, usage, permissions, local audit history, and settings.

The dashboard should make the agent model understandable with a simple live hierarchy: the Conversation Agent at the top; the Orchestration Agent and Context Agent beneath it; and active task workers beneath Orchestration. This initial tree is functional status and control UI, not a polished visualization. Signing, certification, polished onboarding, and rich avatar or animated presentation can follow after the core loop proves useful.

## Initial scope boundary

The first proof is one conversational, floating-panel, or tray-initiated loop that delegates one useful, narrowly scoped computer task while remaining available, exposes activity and cancellation through the user interface, records actions in a local audit, and returns a verified concise result. The floating panel, full dashboard, and rudimentary live agent tree are part of this milestone; a rich avatar treatment is not. Memory and usage ledgers should connect as soon as practical, but their complete architecture must not delay this runnable slice. Multiple users, unrestricted automation, messaging, purchases, smart-home control, signing, and expressive avatar presentation remain outside this milestone.
