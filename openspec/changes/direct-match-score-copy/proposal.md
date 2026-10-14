## Why

Review of the direct match score submission found leftover wording after the backend removed the confirm-ended command. The awaiting-initial-report states show two different descriptions under one label, the score-confirmation description is only correct from the reporter's perspective ("The opponent has five minutes..."), and the live contract spec still lists `confirm-ended` as a supported lifecycle command.

The backend (`Match.cs` `ReportScore` and `ApplyDeadline` on the direct-score branch) starts a five-minute window on the first report, and when that window passes with only one side having reported, that report becomes the final score. The copy must say so.

## What Changes

- Show one shared description for the awaiting-ended-confirmation and awaiting-score states: either side may report the score, the other side then has five minutes to agree or correct it, otherwise the report becomes final.
- Make the score-confirmation description perspective-neutral so it is correct for both the reporter and the other side.
- Remove the now-unused `descriptionAwaitingConfirmation` and `descriptionAwaitingScore` locale keys and add `descriptionAwaitingReport`, keeping the en-US and nl-BE key sets identical.
- Remove `confirm-ended` from the supported lifecycle command list in the live backend contract spec.
- Remove dead `.match-confirmation-status` CSS rules.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `live-backend-contract-integration`: the supported lifecycle command list no longer names confirm-ended.
- `match-result-lifecycle-ui`: the awaiting-report and score-confirmation descriptions are specified.

## Impact

- `TournamentMatchDetailsDialog.razor.cs`, `TournamentMatchDetailsDialog.razor.css`, `wwwroot/locales/translations.en-US.json` and `translations.nl-BE.json`.
- Copy and dead-style cleanup only; no lifecycle, mock or API behaviour changes.
