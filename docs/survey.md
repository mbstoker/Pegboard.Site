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

- **`survey_responses`** holds the survey id, the definition version the respondent answered, the submit time rounded down to the hour, `answers` (`{"S1": ["UK"], …}`) and `path` (the ids of the questions shown). It has no name, email or IP address. The row id is the run id, fixed when the opening page is shown. The insert is insert-if-absent, so a repeated final submit (a lost redirect, a double click, a retry) never stores a second row.
- **`survey_testers`** holds the email, the survey id and the signup day. Nothing in it refers to a response. Ids are random UUIDs and the time is kept to the day, so a row can't be matched to a response by order or time.

If the database is unreachable, the respondent sees "couldn't be saved" and can retry. They are never given a false thank-you, and retrying the same submission never uses up another rate-limit permit.

A survey in progress restarts at the opening page, with a short notice, if its definition `version` has changed since it began or if it began more than 24 hours ago. Answers from one version are never mixed into another.

### The tester list is unconfirmed

Anyone can type any address into the tester form, so a `survey_testers` row is not consent and not proof that the address belongs to the person who typed it. The first message to a tester must be confirm-or-ignore: it asks them to confirm they want to test and says that ignoring it removes them. Contact only addresses that confirm, and delete those that don't.

## Abuse protection

A blocked submission still sees the normal thank-you and is logged with its rule (`honeypot`, `missing-or-tampered-token`, `too-fast` or `rate-limit`), along with how many that rule has blocked since startup. It is not stored, and the IP address is never logged.

- A hidden honeypot field is checked on every step and on the tester form.
- Survey progress is carried in a Data Protection token holding the run id, the definition version and the opening-page timestamp. A missing or altered token is blocked, as is a survey finished faster than `minSecondsToComplete`.
- Each client has an in-memory fixed-window limit (`Survey:RateLimit:PermitLimit` per `WindowMinutes`, default 40 per 60). It is counted separately for responses and tester signups. IPv4 addresses are counted individually and IPv6 addresses by their /64. A permit is charged once per survey run or tester ticket, so retries don't use more.
- The tester form needs a ticket issued with the thank-you page, valid for 24 hours. A ticket adds at most one email.

## Configuration on the VPS

| Setting | Purpose |
|---|---|
| `ConnectionStrings__PegboardDb` | Already set for the site; the survey uses the same database. |
| `Survey__ExportKey` | Enables `GET /survey/{id}/export.csv` with header `X-Survey-Key`. Unset means the endpoint returns 404. Use a long random value of at least 32 characters, such as `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`, and a different one per environment. |
| `DataProtection__KeysPath` | A folder outside the site folder. It **must be a different folder for staging and production**, such as `C:\sites\keys\www-staging` and `C:\sites\keys\www-prod`. Both builds use the application name `PegboardWebSite`, so a shared folder would let a staging token pass on production. Without the setting, the site logs a startup warning, and an app-pool recycle can invalidate surveys in progress; those submissions are then blocked as tampered. |

### Staging check

Before production, also check that a survey survives an app-pool recycle. Start a survey on staging, answer a step or two, recycle the site's app pool, then carry on and submit. The submission must be stored (check the export). If it isn't, `DataProtection__KeysPath` isn't taking effect.

## Export

```powershell
Invoke-WebRequest https://www.staging.epegboard.com/survey/players/export.csv -Headers @{ 'X-Survey-Key' = $env:SURVEY_EXPORT_KEY } -OutFile players.csv
```

The export has one row per response and one column per question id in definition order. Ids found only in older versions are added at the end, followed by `path`. Multi-choice answers are joined with `; `. A text cell that starts with `=`, `+`, `-` or `@` gets a leading `'`, so a spreadsheet won't run it as a formula.
