## ADDED Requirements

### Requirement: Image pickers recover from interrupted reads

The profile picture, create-team logo, manage-team logo and shared image pickers MUST return to a usable state after a recoverable interruption while reading a selected image. The failed selection MUST NOT remain pending or uploadable, any previously selected replacement bytes MUST be discarded, and the user MUST receive localized retry feedback. A later successful selection MUST remain available for upload through the existing backend-confirmed flow.

#### Scenario: A recoverable read failure leaves a picker ready to retry

- **WHEN** a valid image within the existing type and size limits fails while its browser stream is being read
- **THEN** the picker MUST clear the failed pending selection
- **AND** it MUST show a localized retry message
- **AND** its file control MUST remain enabled
- **AND** selecting a readable image afterward MUST make only that image available for upload

#### Scenario: A failed replacement cannot upload stale bytes

- **WHEN** a picker has a successfully read selection and a later replacement fails while being read
- **THEN** the earlier selection MUST NOT remain pending or uploadable
- **AND** the user MUST see the replacement failure
- **AND** only a later successfully read replacement MAY be uploaded

