## Context

Image selection has two independent recoverable stages: the native browser input must deliver a file, and the Blazor component must finish reading that file's stream. The reported Android symptom may fail before either component state updates, while current stream handling also has a reproducible defect on every picker surface.

## Goals / Non-Goals

**Goals:**

- Keep every image picker usable after an interrupted read and surface an accurate localized message.
- Preserve existing type and size validation and the rule that only successfully read bytes may be uploaded.
- Provide a small opt-in observation switch for physical Android diagnosis.

**Non-Goals:**

- Claim that the Android native picker issue is fixed without device evidence.
- Add telemetry, a backend upload contract, a SignalR timeout change, a global circuit handler, file persistence or a CSS picker redesign.

## Decisions

- Handle the documented recoverable stream failures in each existing picker path. This keeps upload ownership and backend-confirmed behavior unchanged while fixing the shared failure semantics.
- Clear a replace pending selection before opening its stream. A failed replacement therefore cannot leave stale bytes looking selected or uploadable.
- Enable browser diagnostics only through an explicit session storage switch. The script installs idempotent page listeners and logs counts, MIME types, byte sizes, visibility, network and observable DOM stages; it never logs filenames or bytes.
- Observe reconnect/error DOM markers and existing server logs separately in the device procedure. Those observations are evidence about presentation, not proof of the SignalR circuit stage.

## Risks / Trade-offs

- A diagnostic switch requires a reload and is intentionally absent from normal UI.
- Android backgrounding and native Gallery behavior still require a real device test; automated stream-failure tests establish recovery behavior, not the Android trigger.
