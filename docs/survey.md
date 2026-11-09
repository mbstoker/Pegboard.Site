# Surveys

The organiser and player surveys at `/survey/organisers` and `/survey/players` ([ado:2566](https://dev.azure.com/mstoker/Pegboard/_workitems/edit/2566)). The question sets are owned by Strategy, in the [organiser](https://github.com/mbstoker/PegboardRoot/blob/master/Strategy/research/grassroots-survey-organiser-questions-2026-11-09.md) and [player](https://github.com/mbstoker/PegboardRoot/blob/master/Strategy/research/grassroots-survey-player-questions-2026-11-09.md) question-set documents. The ethics and data rules are in [brief §8](https://github.com/mbstoker/PegboardRoot/blob/master/Strategy/research/grassroots-survey-brief-2026-11-09.md#8-ethics-and-data).

## Changing a survey

Each survey is one file, `src/PegboardWebSite/Survey/Definitions/<id>.json`. You can change wording, options, branching and opening or closing text there with no code change. The site reads the files at startup, so deploy the file and restart the site. Bump `version` when the meaning of a question changes, because every response records the version it answered.

- **Sections** group questions. A section's `title` is for editors only. Its `header` is the "Thinking about…" line shown at the top of each step in that section. A change of header starts a new step.
- **Question types:** `single`, `multi` (optional `maxPicks`, plus `exclusiveOptions` such as "Neither"), `number`, `shortText` (up to 200 characters) and `text` (up to 2,000 characters). A question is required unless it has `"optional": true`. `hint` adds a line under the question.
- **`showIf`** names one earlier question and one operator. Use `equals` or `notEquals` for single choice, `includes` or `minCount` for multi choice, and `anyOf` for either. An unanswered question never satisfies a condition. A question whose condition is false is not shown, and any answer it has is dropped before storage.
- **`endPages`** stop the survey when their condition holds and store nothing. The player survey's under-16 gate is one of these.
- `questionsPerStep` (default 4) and `minSecondsToComplete` (default 60) are per survey.

An invalid file is logged and left out, so its URL returns 404 and the rest of the site keeps running. The test suite validates the shipped files and checks their question order against the question sets, so update `SurveyDefinitionTests` when you add or move a question.

## Storage

The two tables live in `PegboardDb` ([deploy/sql/survey.sql](../deploy/sql/survey.sql); the site also creates them at startup).

- **`survey_responses`** holds the survey id, definition version, submit time, `answers` (`{"S1": ["UK"], …}`) and `path` (the ids of the questions shown). It has no name, email or IP address.
- **`survey_testers`** holds the email, the survey id and the signup day. Nothing in it refers to a response. Ids are random UUIDs and the time is kept to the day, so a row can't be matched to a response by order or time.

If the database is unreachable, the respondent sees "couldn't be saved" and can retry. They are never given a false thank-you.

## Abuse protection

A blocked submission still sees the normal thank-you and is logged with its rule (`honeypot`, `missing-or-tampered-token`, `too-fast` or `rate-limit`). It is not stored, and the IP address is never logged.

- A hidden honeypot field is checked on every step and on the tester form.
- Survey progress is carried in a Data Protection token holding the opening-page timestamp. A missing or altered token is blocked, as is a survey finished faster than `minSecondsToComplete`.
- Each IP has an in-memory fixed-window limit (`Survey:RateLimit:PermitLimit` per `WindowMinutes`, default 10 per 60). It is counted separately for responses and tester signups.
- The tester form needs a ticket issued with the thank-you page, valid for 24 hours.

## Configuration on the VPS

| Setting | Purpose |
|---|---|
| `ConnectionStrings__PegboardDb` | Already set for the site; the survey uses the same database. |
| `Survey__ExportKey` | Enables `GET /survey/{id}/export.csv` with header `X-Survey-Key`. Unset means the endpoint returns 404. |
| `DataProtection__KeysPath` | A folder outside the site folder, such as `C:\sites\keys\www`. Without it, an app-pool recycle can invalidate surveys in progress, and those submissions are then blocked as tampered. |

## Export

```powershell
Invoke-WebRequest https://www.staging.epegboard.com/survey/players/export.csv -Headers @{ 'X-Survey-Key' = $env:SURVEY_EXPORT_KEY } -OutFile players.csv
```

The export has one row per response and one column per question id in definition order. Ids found only in older versions are added at the end, followed by `path`. Multi-choice answers are joined with `; `. A text cell that starts with `=`, `+`, `-` or `@` gets a leading `'`, so a spreadsheet won't run it as a formula.
