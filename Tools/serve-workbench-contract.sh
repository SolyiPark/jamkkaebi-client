#!/usr/bin/env bash
# 작업대 서버 계약 검증용 로컬 Spring 서버를 띄운다(macOS·Linux).
#
#   Tools/serve-workbench-contract.sh <jamkkaebi-server 체크아웃 경로>
#
# 1. 서버 체크아웃에 UnityWorkbenchHarnessTest.java를 잠시 복사해 H2 로컬 서버를 연다.
# 2. 서버가 만든 세션 파일(주소·검증용 토큰)을 이 프로젝트의 Temp/ServerContract/workbench-session.json으로 복사한다.
#    Unity 에디터가 열려 있어도 된다. 에디터의 Test Runner(WorkbenchServerContractTests)와 Play가 이 파일을 읽는다.
# 3. Ctrl+C로 끝내면 서버를 멈추고 복사한 하네스·세션 파일을 모두 지운다.
#
# 세션 파일의 토큰은 로컬 H2 검증 서버에서만 쓰는 값이다. 커밋하거나 채팅·문서에 붙이지 않는다.
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "사용법: $0 <jamkkaebi-server 체크아웃 경로>" >&2
    exit 2
fi

project_root="$(cd "$(dirname "$0")/.." && pwd)"
server_root="$(cd "$1" && pwd)"
harness_source="$project_root/Tools/UnityWorkbenchHarnessTest.java"
harness_target="$server_root/src/test/java/com/example/jamkkaebi/workbench/UnityWorkbenchHarnessTest.java"
session_file="$server_root/workbench-session.json"
stop_file="$server_root/workbench-stop"
client_session_dir="$project_root/Temp/ServerContract"
client_session="$client_session_dir/workbench-session.json"
log_dir="$project_root/Logs/ServerContract/Workbench-$(date +%Y%m%d-%H%M%S)"

if [[ ! -f "$server_root/gradlew" ]]; then
    echo "서버 체크아웃에 gradlew가 없습니다: $server_root" >&2
    exit 1
fi
if ! java -version 2>&1 | grep -q 'version "21'; then
    echo "Java 21이 필요합니다(JAVA_HOME 확인)." >&2
    exit 1
fi
for target in "$harness_target" "$session_file" "$stop_file" "$client_session"; do
    if [[ -e "$target" ]]; then
        echo "기존 검증 파일을 덮어쓰지 않습니다: $target" >&2
        exit 1
    fi
done

mkdir -p "$log_dir" "$(dirname "$harness_target")" "$client_session_dir"
server_pid=""

cleanup() {
    touch "$stop_file"
    if [[ -n "$server_pid" ]]; then
        for _ in $(seq 1 60); do
            kill -0 "$server_pid" 2>/dev/null || break
            sleep 0.5
        done
        kill "$server_pid" 2>/dev/null || true
    fi
    rm -f "$harness_target" "$session_file" "$stop_file" "$client_session"
    rmdir "$(dirname "$harness_target")" 2>/dev/null || true
    echo "검증 서버를 멈추고 임시 파일을 지웠습니다."
}
trap cleanup EXIT INT TERM

cp "$harness_source" "$harness_target"
echo "Spring 검증 서버를 시작합니다. 로그: $log_dir/spring.log"
(cd "$server_root" && sh ./gradlew test --tests '*UnityWorkbenchHarnessTest' --rerun-tasks --no-daemon \
    > "$log_dir/spring.log" 2>&1) &
server_pid=$!

deadline=$((SECONDS + 600))
while [[ ! -f "$session_file" ]]; do
    if ! kill -0 "$server_pid" 2>/dev/null; then
        echo "Spring 검증 서버가 시작 전에 끝났습니다. spring.log를 확인하세요." >&2
        exit 1
    fi
    if (( SECONDS > deadline )); then
        echo "Spring 검증 서버를 10분 안에 시작하지 못했습니다." >&2
        exit 1
    fi
    sleep 0.5
done

(umask 077 && cp "$session_file" "$client_session")
echo "준비됐습니다. 세션 파일: $client_session"
echo "- Test Runner: EditMode > WorkbenchServerContractTests 실행"
echo "- Play: UIPrototype 씬을 Play하면 셸이 이 서버에 연결합니다(에디터 전용)."
echo "끝내려면 Ctrl+C를 누르세요."
wait "$server_pid" || true
