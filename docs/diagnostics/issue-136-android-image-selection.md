# Image selection diagnostics for FE #136

The picker read-recovery tests establish that an `IOException`, `TimeoutException` or Blazor `JSException` during a selected image read no longer strands the profile, create-team, manage-team or shared picker. They do not establish why Android's Google Gallery leaves the picker unusable after returning. The reported failure after both short and long selections still needs controlled device evidence.

## Enable browser diagnostics

In the affected tab's developer console, run:

```js
sessionStorage.setItem('lanParty.imageSelectionDiagnostics', '1');
location.reload();
```

Disable diagnostics and reload with:

```js
sessionStorage.removeItem('lanParty.imageSelectionDiagnostics');
location.reload();
```

To correlate the browser stages with component-side logs, temporarily set these component categories to `Debug` in the app's diagnostic run configuration, then restore their prior levels afterward:

```json
{
  "Logging": {
    "LogLevel": {
      "Mercurius.LAN.Web.Components.Pages.Users.Profile": "Debug",
      "Mercurius.LAN.Web.Components.Pages.Teams.CreateTeamDialog": "Debug",
      "Mercurius.LAN.Web.Components.Pages.Teams.ManageTeams": "Debug",
      "Mercurius.LAN.Web.Components.Shared.CustomInputFile": "Debug"
    }
  }
}
```

The mode is off by default and writes only to that tab's browser console. It records timestamped native input click/change/cancel stages, selected file count/type/size, whether the input is disabled or connected, page visibility/lifecycle and network events, Blazor's reconnect state event, and visible reconnect/error DOM state. It never records names, image bytes, preview URLs or form contents, and sends no telemetry. Do not share a console export containing unrelated page logs.

## Reproduce and record

For each run, note the device model, Android version, browser and version, site build, network (Wi-Fi/mobile), picker choice and wait duration. Keep the browser console open through the complete flow.

1. On Android Chrome, try Google Gallery with a quick selection and again after waiting at least 30 seconds. Repeat both with Files if available.
2. Repeat on profile picture, create-team logo, manage-team logo, tournament image and sponsor logo. After the first selection returns, click the same upload control again and record whether a `native-click` stage appears.
3. If the styled control appears dead, use remote DevTools to make its native input temporarily visible and clickable, then repeat once. Select the affected form's input in the console and run `Object.assign($0.style, { position: 'fixed', width: '10rem', height: '3rem', opacity: '1', pointerEvents: 'auto', zIndex: '2147483647' })`. Record whether this visible native control opens the picker, then reload to discard the temporary style.
4. Repeat the same forms on desktop Chrome and Firefox, and mobile Safari/iOS when available. Record whether the problem follows a browser, device, picker app or form.
5. Correlate browser timestamps with existing server logs. The component Debug entries are `selection callback received`, `selection rejected`, `read completed`, `read failed (ExceptionType)` or `read canceled`. They deliberately omit filenames and exception messages.

Interpret the stages narrowly: no `native-click` means the control did not receive a click; `native-click` without change/cancel means the browser did not report the picker closing; a native change without a server callback points to the handoff between browser event and Blazor callback; a callback followed by a read failure shows a recoverable server-side stream failure; a completed read shows that the image reached the component. The reconnect event, network events, visibility and DOM state are observations only. They do not prove that a SignalR circuit disconnected or reconnected; use existing server circuit logs or direct transport evidence for that conclusion.

## Current evidence

Automated fake-stream tests reproduce image-read failures and verify that a later readable selection can be used. No physical Android Gallery/Files run has been completed here, so the Android trigger remains unconfirmed and this issue should remain open until the device matrix above is recorded.
