# Prototype — 프로젝트 규칙

## 테스트 실행 — 하지 않는다

EditMode 테스트는 **Unity Test Runner가 알아서 전부 돌린다.** 그러므로:

- 테스트 실행을 사용자에게 요청하지 않는다. 결과를 기다리며 작업을 멈추지 않는다.
- `Unity.exe -batchmode -runTests` CLI도 시도하지 않는다 — 에디터가 열려 있어 프로젝트 락에 막힌다.
- 계획서에 "테스트를 돌려 실패를 확인한다" 같은 단계가 있어도 **건너뛰고 다음으로 간다.**

테스트 **코드는 계획대로 계속 쓴다.** 실행만 붙잡지 않는다.

검증이 필요한 건 테스트로 못 잡는 것뿐이다 — 씬을 재생해서 눈으로 확인하는 항목.
그건 사용자에게 "무엇을 볼지" 목록으로 넘긴다.

## 테스트 파일 위치

`Assets/Editor/Tests/`, 네임스페이스 `Prototype.Tests`, NUnit.

`Assets/Tests/EditMode/`가 아니다 — asmdef로 만든 어셈블리는 `Assembly-CSharp`(게임 코드)를
참조할 수 없다는 유니티 제약 때문에, 게임 코드를 asmdef로 쪼개기 전까지 여기가 유일하게 동작하는 자리다.

<!-- code-review-graph MCP tools -->
## MCP Tools: code-review-graph

**IMPORTANT: This project has a knowledge graph. ALWAYS use the
code-review-graph MCP tools BEFORE using Grep/Glob/Read to explore
the codebase.** The graph is faster, cheaper (fewer tokens), and gives
you structural context (callers, dependents, test coverage) that file
scanning cannot.

### When to use graph tools FIRST

- **Exploring code**: `semantic_search_nodes_tool` or `query_graph_tool` instead of Grep
- **Understanding impact**: `get_impact_radius_tool` instead of manually tracing imports
- **Code review**: `detect_changes_tool` + `get_review_context_tool` instead of reading entire files
- **Finding relationships**: `query_graph_tool` with callers_of/callees_of/imports_of/tests_for
- **Architecture questions**: `get_architecture_overview_tool` + `list_communities_tool`

Fall back to Grep/Glob/Read **only** when the graph doesn't cover what you need.

### Key Tools

| Tool | Use when |
| ------ | ---------- |
| `detect_changes_tool` | Reviewing code changes — gives risk-scored analysis |
| `get_review_context_tool` | Need source snippets for review — token-efficient |
| `get_impact_radius_tool` | Understanding blast radius of a change |
| `get_affected_flows_tool` | Finding which execution paths are impacted |
| `query_graph_tool` | Tracing callers, callees, imports, tests, dependencies |
| `semantic_search_nodes_tool` | Finding functions/classes by name or keyword |
| `get_architecture_overview_tool` | Understanding high-level codebase structure |
| `refactor_tool` | Planning renames, finding dead code |

### Workflow

1. The graph auto-updates on file changes (via hooks).
2. Use `detect_changes_tool` for code review.
3. Use `get_affected_flows_tool` to understand impact.
4. Use `query_graph_tool` pattern="tests_for" to check coverage.
