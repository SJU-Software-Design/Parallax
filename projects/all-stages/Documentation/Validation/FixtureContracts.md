# Public native regression fixtures

These small fixtures replace machine-local review paths for the explicit native ending validator. They contain no save files or private presentation. They do not change product credits or audio.

Pass absolute fixture JSON paths to the existing opt-in validation flags:

- `-eclipse-team-role-source <project>/Documentation/Validation/ApprovedTeamRoles.json`
- `-eclipse-audio-source-guard <project>/Documentation/Validation/AudioSourceGuards.json`

The source paths inside each JSON resolve relative to that JSON. The native test still requires its existing explicit isolated `-eclipse-ending-validate` output and `-eclipse-ending-test` authorization flags. It never runs during normal play.

## Approved team roles

`ApprovedTeamRoles.txt` records the approved four captions and complete roles, including the later user corrections. Its UTF-8 text hash ignores only a leading BOM and CRLF/LF differences. The JSON oracle is compared to the product team entries in full.

The original presentation's SHA-256 and eighth-slide reference remain as provenance from the prior local inspection. The checked-in transcript is not the presentation, and this portable test does not claim to inspect that private file again. Existing local fixtures with an absolute original presentation path retain their original raw-file verification branch.

## Shared movement audio routing

`PlayerMovementAudio.before-eclipse.txt` is the captured pre-Eclipse shared movement source, retained as a comparison oracle rather than compiled code. The guard verifies the current source hash and that removing exactly the one Eclipse routing line leaves this original text unchanged. UTF-8 BOM and CRLF/LF are normalized, preserving all other source content.

This source comparison checks shared cadence and other stage routes. Separate existing runtime checks inspect the decoded boot/coda samples and actual mixer routing. The source fixture does not assert ownership of an external original recording or subjective listening approval. Existing machine-local raw-byte guards remain supported.