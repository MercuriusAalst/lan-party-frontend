# Image selection reproduction for FE #136

The read-recovery tests cover interrupted image streams across profile, create-team, manage-team and shared pickers. They do not establish why Android's Google Gallery can leave the picker unusable after returning. No physical Android run has confirmed that trigger.

For each run, record the device model, OS, browser and version, site build, network, picker app and wait duration. On Android Chrome, try Google Gallery with a quick selection and again after waiting at least 30 seconds; repeat with Files if available. Test profile picture, create-team logo, manage-team logo, tournament image and sponsor logo, then try reopening the picker after each selection. Repeat on desktop Chrome and Firefox and mobile Safari/iOS when available.

If a styled control appears dead, use remote DevTools to select its native `input[type=file]` and temporarily expose it with:

```js
Object.assign($0.style, { position: 'fixed', width: '10rem', height: '3rem', opacity: '1', pointerEvents: 'auto', zIndex: '2147483647' });
```

Record whether the visible native control opens the picker, then reload to discard the temporary style. Use existing server circuit logs if available; a failure to open the picker alone does not establish that the Blazor circuit disconnected. Keep Android-specific root cause and fix claims pending device evidence.
