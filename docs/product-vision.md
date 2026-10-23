# Product Vision

## Thesis

**Hey Computer is an open-source, local-first Windows Jarvis that remembers transparently and delegates work to whichever agent is best.** It is not another general chat window and should not rebuild specialist coding, browser, or automation agents. It provides the durable relationship layer: voice, personality, context, routing, permissions, budget awareness, task supervision, and understandable results.

## The persistent brain

The user speaks only with the Conversation Agent. It stays responsive, listens more than it talks, and preserves a stable personality. Beside it, the Orchestration Agent turns conversational intent into bounded tasks and coordinates disposable workers. The Context Agent independently maintains the local memory vault from completed turns and verified outcomes. Separating these roles prevents long work from freezing conversation and prevents worker output from silently becoming identity or fact.

## Local-first meaning

Local-first does not require local model inference. Canonical memory, task state, permissions, credentials, audit history, and configuration live on the user’s Windows computer in inspectable formats. Cloud models may process requests when selected, but the product must disclose what leaves the device. Future optional local models may perform light routing, summarization, retrieval, or classification on hardware that can support them.

## Transparent continuity

Continuity comes from explicit memory rather than one enormous hidden conversation. Human-readable Markdown and structured metadata hold durable facts, decisions, projects, tasks, and dated summaries. A derived SQLite index supports fast retrieval and can be rebuilt. Every durable memory should have a source, timestamp, confidence, and user-visible correction or deletion path.

## Delegation contract

Every worker receives an objective, permitted capabilities, model choice, budget, deadline, cancellation path, and expected result schema. The orchestrator limits concurrency, prevents obvious duplication, validates completion evidence, and asks before continuing when a configured usage threshold is low. Workers are task-scoped and do not speak as the main personality or directly rewrite durable memory.

## Computer-use direction

Capable computer control is central, but it should use the most reliable interface available: direct application APIs, MCP, shell or provider integrations first; Playwright-style browser control next; Windows UI Automation, COM, and Win32 for native applications; and visual clicking only when structured interfaces fail. A capability broker mediates every action and requires explicit confirmation for consequential operations.

## Initial scope boundary

The first architectural proof is one conversational voice loop that delegates one safe task while remaining available, records it in a visible task and usage ledger, updates an inspectable local memory vault in parallel, and returns a verified concise result. Multiple users, unrestricted automation, messaging, purchases, and smart-home control remain outside this milestone.
