## 1. OpenSpec

- [x] 1.1 Record the `featured-tournament-curation` delta for the one-to-four selection range, the compact draft arrangement, and the saved-count fallback.
- [x] 1.2 Strictly validate this change before writing production code: `openspec validate issue-94-featured-count-range --type change --strict --no-interactive` passed before any source edit.

## 2. Featured editor

- [x] 2.1 Draft the actual selected cards instead of four padded positions, so removing a card compacts the arrangement and the first remaining card becomes the lead card.
- [x] 2.2 Enable **Save changes** for one to four distinct eligible tournaments, disable it for an empty selection with a localized minimum-one hint, and keep adding blocked at four.
- [x] 2.3 Keep the draft local until a confirmed save, keep **Cancel** and the failed-save recovery behavior, and replace the exact-four copy with range copy in en-US and nl-BE.

## 3. Shared cards

- [x] 3.1 Render the draft as lead card plus the remaining selected rows, drop the empty-position rendering, its styles and its copy, and keep the drag, drop-target, grip, and position-control behavior error-free for one to four cards.

## 4. Mock back end

- [x] 4.1 Accept one to four distinct eligible tournaments, preserve the saved count and order, and backfill a no-longer-eligible saved tournament only up to the saved selection count while an unconfigured selection still defaults to up to four.

## 5. Verification

- [x] 5.1 Retarget the editor contract tests to the one-to-four range, the compact arrangement, the empty-selection guard, and the saved-count fallback, and add the mock parity case that proves a partial saved selection is not padded to four. Tests are written; execution is owned by the independent tester because build and test runs are paused for this task.
- [x] 5.2 Cover the one-to-four range against the real API: a three-card save and a single-card save reload on the public homepage as the same lead card plus rows, with the hero following the confirmed lead, and restore the seeded four ids in a `finally` so the shared stored selection is not inherited by later tests. The test is written; execution is owned by the independent tester.
- [x] 5.3 Strictly validate this change again after implementation and task synchronization: `openspec validate issue-94-featured-count-range --type change --strict --no-interactive` passed after the source, mock, locale, and test edits.
- [x] 5.4 Independent tester ran the full Debug contract suite (425/425) and the real-API E2E (32/32: the editor drag across eight viewports, the empty-selection minimum-one message, a three-then-one save/reload count-preserving sequence, and the seeded-four restore), and an independent reviewer confirmed the compact-arrangement and mock-cardinality cleanup with no unresolved findings before this change was archived.
