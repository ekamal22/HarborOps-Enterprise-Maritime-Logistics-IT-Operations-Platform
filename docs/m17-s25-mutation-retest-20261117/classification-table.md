| ID | Family | Mutant | Frozen source 634053c4 | Candidate source | Final disposition |
| --- | --- | --- | --- | --- | --- |
| M01 | reconcile binding | promtool binding bypassed | KILLED (exit 1, 1 failing) | n/a | **KILLED_ON_FROZEN** |
| M02 | reconcile binding | manifest rejection binding bypassed | KILLED (exit 1, 1 failing) | n/a | **KILLED_ON_FROZEN** |
| M03 | evidence archive | extracted files not compared by size | SURVIVED (exit 0) | n/a | **REDUNDANT_VERDICT_EQUIVALENT** |
| M04 | evidence archive | extra extracted files tolerated | SURVIVED (exit 0) | n/a | **REDUNDANT_DEFENCE_IN_DEPTH** |
| M05 | evidence archive | unextracted files tolerated | SURVIVED (exit 0) | n/a | **REDUNDANT_VERDICT_EQUIVALENT** |
| M06 | docker ownership | project label not compared (container) | SURVIVED (exit 0) | SURVIVED (exit 0) | **UNREACHABLE_GUARD** |
| M07 | docker ownership | network label project not compared | SURVIVED (exit 0) | SURVIVED (exit 0) | **UNREACHABLE_GUARD** |
| M08 | docker ownership | residual resources tolerated | SURVIVED (exit 0) | KILLED (exit 1, 1 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |
| M09 | docker ownership | address range release not proven | SURVIVED (exit 0) | KILLED (exit 1, 1 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |
| M10 | docker ownership | volumes removed with containers | KILLED (exit 1, 1 failing) | KILLED (exit 1, 2 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |
| M11 | docker ownership | existing evidence directory overwritten | SURVIVED (exit 0) | KILLED (exit 1, 1 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |
| M12 | diagnostic producer | exit of children not awaited | control failed here (exit 1) | n/a | **OPEN_PLATFORM_BLOCKED** |
| M13 | diagnostic producer | lingering child tolerated | control failed here (exit 1) | n/a | **OPEN_PLATFORM_BLOCKED** |
| M14 | diagnostic verifier | stream names accepted | SURVIVED (exit 0) | KILLED (exit 1, 1 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |
| M14b | diagnostic verifier | stream names accepted (second position) | KILLED (exit 1, 1 failing) | KILLED (exit 1, 1 failing) | **KILLED_ON_FROZEN** |
| M15 | diagnostic verifier | bom not stripped | KILLED (exit 1, 16 failing) | n/a | **KILLED_ON_FROZEN** |
| M16 | witness binding | marker may be non-comment text | SURVIVED (exit 0) | KILLED (exit 1, 2 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |
| M17 | witness binding | block start not required | SURVIVED (exit 0) | KILLED (exit 1, 1 failing) | **GAP_CLOSED_KILLED_ON_CANDIDATE** |
