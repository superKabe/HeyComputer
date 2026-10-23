# Computer Control Architecture

## Decision

Hey Computer will use a general, provider-neutral desktop-control loop rather than expanding a catalog of app-specific commands. The loop observes the Windows desktop, asks a computer-use worker to choose a bounded action, applies policy, executes locally, observes the changed state, and verifies progress. Direct APIs, browser automation, shortcuts, and application skills remain optional accelerators; full visual mouse-and-keyboard control is the universal fallback.

## Recommended foundation

Start with the MIT-licensed core of [Cua Driver](https://github.com/trycua/cua) as a replaceable local sidecar behind an Hey Computer-owned `IDesktopDriver` contract. OpenClaw uses the same driver family for its experimental Windows computer-use path, while [Microsoft UFO](https://github.com/microsoft/UFO) independently validates the hybrid pattern of screenshots plus Windows UI Automation. A short local spike must prove window enumeration, screenshots, UI-tree capture, clicking, typing, scrolling, cancellation, and post-action verification before CUA becomes a committed dependency.

The CUA integration is a starting point, not the product boundary. Hey Computer owns orchestration, model adapters, permission gates, action budgets, audit history, cancellation, and result verification. This allows individual driver functions to move later to [FlaUI](https://github.com/FlaUI/FlaUI), native [Windows UI Automation](https://learn.microsoft.com/en-us/windows/win32/winauto/entry-uiautocore-overview), [Windows Graphics Capture](https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture), or Win32 `SendInput` without changing the conversation or orchestration layers.

## Runtime layers

The Computer Use worker receives a bounded task from Orchestration while the Conversation Agent remains available. A model adapter converts the selected provider’s output into a common desktop action schema. The local control host supplies observations and executes actions through four paths: Windows UI Automation for structured native controls; CUA or native screen and input primitives for visual control; Playwright or browser accessibility data as an optional browser optimization; and direct APIs, shell, media keys, or app-specific skills when they are measurably faster or more reliable.

Each observation carries a frame or state identifier. Before executing an action, the control host rejects stale observations, checks scope and confirmation requirements, records the attempt, and confirms that the expected state change occurred. The model should receive event-driven snapshots or relevant crops rather than a continuous video stream; this reduces latency, API cost, and unnecessary disclosure of the desktop.

## Provider strategy

[OpenAI computer use](https://developers.openai.com/api/docs/guides/tools-computer-use) and [Anthropic computer use](https://platform.claude.com/docs/en/agents-and-tools/tool-use/computer-use-tool) already define screenshot, mouse, and keyboard loops that an application executes in its own environment. Grok currently documents image understanding and function calling, which is enough to prototype the same loop through Hey Computer’s action tools, but the driver contract must stay independent of any provider’s native schema. Provider evaluation should measure grounding accuracy, step count, latency, cost, recovery, and instruction-injection resistance on the same local tasks.

## Boundaries and risks

An ordinary Windows process cannot safely automate everything. UAC and the secure desktop are separate security boundaries; higher-integrity applications can reject injected input; protected media can capture as black; and multi-monitor DPI, display rotation, remote desktop, minimized windows, games, and custom-rendered controls create special cases. Foreground control also competes with the human’s real mouse and keyboard, so Hey Computer needs an unmistakable takeover state, a global emergency stop, and immediate cancellation before broader autonomy.

Everything visible on screen is untrusted observation. Websites, messages, documents, and images cannot grant permission, redefine the goal, or approve consequential actions. Cloud vision also means selected screenshots may leave the machine, so the dashboard must identify the active provider, show what is being shared, support window or region scoping, and eventually add sensitive-region redaction.

## First proof

The next milestone is a constrained live desktop session, not another application button. It should enumerate windows, capture one selected window and its UI tree, execute click/type/scroll actions, show each action and observation in the dashboard, stop immediately, and verify the final state. The acceptance test is one task that crosses two ordinary applications without app-specific code, first under a deterministic test harness and then on the user’s desktop.

## Existing prototype

The current Spotify, YouTube, process-check, and media-key controls remain as smoke tests and future fast paths. They should not expand into the primary architecture. Their typed results, cancellation, allowlisting, and tests can be reused inside the broader control host.

## Alternatives considered

Three broad implementation strategies are viable. A native Windows approach built around UI Automation, FlaUI, Windows Graphics Capture, and `SendInput` would fit the existing .NET application and give maximum control, but would require us to build and maintain most of the difficult perception, input, recovery, and browser machinery. A pure screenshot-and-coordinate loop, like the basic OpenAI or Anthropic computer-use contract, has universal visual reach but is slower, more expensive, and more fragile on long tasks. A browser-first implementation using Playwright or CDP is efficient and reliable for websites but cannot control the rest of Windows. The recommended hybrid routes each step through the strongest available interface and preserves visual control as the fallback.

## Current state of the art

The strongest contemporary systems are converging on hybrid, adaptive interfaces. Microsoft UFO² combines application-specific agents, screenshots, UI Automation, native APIs, and isolated desktops. [Agent S](https://github.com/simular-ai/Agent-S) demonstrates general screenshot-driven control across desktop environments, while [Agent S4](https://arxiv.org/abs/2610.28601) goes further by having a meta-agent evolve small tool interfaces from task experience; its authors report 21.3% on strict OSWorld V2 with an open model, 3.8 times the same model with fixed interfaces. This supports Hey Computer’s design principle that structured tools should emerge as optimizations over a general control loop rather than replacing it.

Published computer-use scores are not directly comparable across OSWorld, OSWorld Verified, OSWorld V2, Windows Agent Arena, different step budgets, and best-of-N harnesses. Hey Computer should build a small repeatable Windows suite derived from [Windows Agent Arena](https://github.com/microsoft/WindowsAgentArena) and report completion, action count, latency, provider cost, focus theft, stale-state recovery, and user interventions.

## Comparison with OpenClaw

[OpenClaw](https://github.com/openclaw/openclaw) is the closest broad product analogue: a mature, cross-platform agent gateway with chat channels, plugins, remote nodes, tool policy, device pairing, and persistent workspaces. On Windows and Linux its optional, experimental CUA plugin embeds a pinned Cua Driver SDK in-process, exposes a normalized computer tool, verifies native artifacts, binds actions to the current session and frame generation, and closes execution on cancellation or disconnect. OpenClaw treats the gateway as the authorization boundary and CUA as a replaceable effector.

Hey Computer is currently far behind OpenClaw in general execution, permissions, pairing, plugins, browser operations, remote nodes, and lifecycle hardening. Its differentiated target is narrower: a Windows-native, voice-first companion with a stable conversational personality, a transparent local memory vault, an always-visible Conversation → Orchestration + Context → Worker hierarchy, and explicit usage-budget governance. We should borrow OpenClaw’s provider boundary, pinned runtime, execution-scoped references, opt-in control, and lifecycle cleanup without copying its broader channel-and-gateway product.

