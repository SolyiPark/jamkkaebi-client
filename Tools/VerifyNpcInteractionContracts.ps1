#requires -Version 7.0
<#
.SYNOPSIS
Unity 없이 NPC 공통 규약을 컴파일하고 25개 시나리오 그룹을 검사합니다.
.DESCRIPTION
새 PowerShell 7 프로세스에서 순수 C# 계약과 아래 임시 검사기를 함께 컴파일합니다.
XML 문서 주석을 포함해도 실행 동작이 달라지지 않는지 확인하는 계약 검사이며,
Unity Test Runner, Player, IL2CPP 또는 Android 실제 추론 검증을 대체하지 않습니다.
공개 멤버 문서 누락과 XML 형식, 매개변수, 참조 오류를 컴파일 오류로 처리합니다.
Add-Type은 문서 주석을 검사하지만 XML 문서 파일은 내보내지 않습니다.
검사 실패는 예외로 종료하고 성공 요약은 지정한 보고서에 저장합니다.
.PARAMETER ReportPath
결과 요약을 저장할 경로입니다. 생략하면 Logs/NpcInteraction/contracts-portable.txt입니다.
.EXAMPLE
pwsh -NoProfile -File ./Tools/VerifyNpcInteractionContracts.ps1
#>
param(
    [string]$ReportPath
)

# Unity 없이 순수 C# 계약을 컴파일하고 주요 시나리오를 확인합니다.
# Unity EditMode/Player, IL2CPP, 실제 추론 및 Android 검증을 대체하지 않습니다.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot 'Assets\Jamkkaebi\Scripts\Gameplay\Npc\Interaction'
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $projectRoot 'Logs\NpcInteraction\contracts-portable.txt'
}
$contractSources = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File | Sort-Object Name)
if ($contractSources.Count -eq 0) { throw "계약 소스를 찾을 수 없습니다: $sourceRoot" }
if ('NpcInteractionPortableVerification' -as [type]) {
    throw '이 검사는 새 PowerShell 프로세스에서 실행하세요. 이전에 로딩한 계약을 재사용할 수 없습니다.'
}

$harnessSource = @'
using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Npc.Interaction;

/// <summary>Unity 의존 없이 순수 계약 시나리오를 실행하는 일회성 검사기입니다.</summary>
public static class NpcInteractionPortableVerification
{
    /// <summary>현재 실행에서 성공한 시나리오 이름을 모읍니다.</summary>
    private static readonly List<string> Passed = new List<string>();

    /// <summary>기존 결과를 비우고 모든 시나리오를 실행합니다. 실패는 예외로 종료합니다.</summary>
    /// <returns>성공한 시나리오 이름의 새 배열입니다.</returns>
    public static string[] Run()
    {
        Passed.Clear();
        Check("unknown state remains distinct from zero", () =>
        {
            var unknown = Context();
            var known = new NpcContextSnapshot("npc-a", "spirit-a", "도깨비", 4, careCount: 0, pollution: 0);
            Require(unknown.CareCount == null && unknown.Pollution == null, "unknown values");
            Require(known.CareCount == 0 && known.Pollution == 0, "known zero");
            Require(unknown.Traits.Count == 0 && unknown.RecentContext.Count == 0, "empty context");
        });
        Check("snapshot lists are defensive copies and read-only", () =>
        {
            var traits = new List<string> { "장난스러움" };
            var recent = new List<string> { "관리를 받았다." };
            var context = new NpcContextSnapshot("npc-a", "spirit-a", "도깨비", 4, traits, recentContext: recent);
            traits[0] = "변경";
            recent.Clear();
            Require(context.Traits[0] == "장난스러움" && context.RecentContext.Count == 1, "snapshot copies");
            Expect<NotSupportedException>(() => ((IList<string>)context.Traits)[0] = "변경");
            Expect<NotSupportedException>(() => ((IList<string>)context.RecentContext).Clear());
        });
        Check("page and plan lists are defensive copies and read-only", () =>
        {
            var choices = new List<NpcInteractionChoice> { new NpcInteractionChoice("care", "관리") };
            var facts = new List<string> { "오염도가 낮아졌다." };
            var page = new NpcInteractionPage("session-a", "root", Context(), choices);
            var plan = new NpcResponsePlan("thanks", "감사", Reaction(), facts, "root", choices);
            choices.Clear();
            facts[0] = "변경";
            Require(page.Choices[0].ActionId == "care" && plan.NextChoices[0].ActionId == "care", "choice copies");
            Require(plan.Facts[0] == "오염도가 낮아졌다.", "fact copies");
            Expect<NotSupportedException>(() => ((IList<NpcInteractionChoice>)page.Choices).Clear());
            Expect<NotSupportedException>(() => ((IList<NpcInteractionChoice>)plan.NextChoices).Clear());
            Expect<NotSupportedException>(() => ((IList<string>)plan.Facts)[0] = "변경");
        });
        Check("pollution rejects NaN infinity and values outside 0..1", () =>
        {
            foreach (float value in new[] { -0.01f, 1.01f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Expect<ArgumentOutOfRangeException>(() => new NpcContextSnapshot("npc-a", "spirit-a", "도깨비", 4, pollution: value));
            Require(new NpcContextSnapshot("npc-a", "spirit-a", "도깨비", 4, pollution: 1).Pollution == 1, "upper boundary");
        });
        Check("negative care count and state versions are rejected", () =>
        {
            Expect<ArgumentOutOfRangeException>(() => new NpcContextSnapshot("npc-a", "spirit-a", "도깨비", 4, careCount: -1));
            Expect<ArgumentOutOfRangeException>(() => Context(version: -1));
            Expect<ArgumentOutOfRangeException>(() => Selection(version: -1));
        });
        Check("page accepts only the matching enabled selection", () =>
        {
            var page = new NpcInteractionPage("session-a", "root", Context(), new[]
            {
                new NpcInteractionChoice("care", "관리"), new NpcInteractionChoice("gift", "선물", false, "선물 없음")
            });
            Require(page.CanSelect(Selection()), "valid selection");
            var invalid = new[] { null, Selection(actionId: "gift"), Selection(actionId: "unknown"),
                Selection(sessionId: "session-b"), Selection(nodeId: "node-b"), Selection(npcId: "npc-b"),
                Selection(version: 3), Selection(version: 5) };
            foreach (var item in invalid) Require(!page.CanSelect(item), "invalid selection");
        });
        Check("request carries matching NPC post-effect version", () =>
        {
            Require(Request().Context.StateVersion == 5, "post-effect version");
            Require(Request(Context()).Context.StateVersion == 4, "no-op state");
            Require(Request().ContractVersion == 1, "contract version");
            Expect<ArgumentException>(() => Request(Context(version: 3)));
            Expect<ArgumentException>(() => Request(Context(npcId: "npc-b")));
        });
        Check("non-positive total generation budget is rejected", () =>
        {
            foreach (int budget in new[] { 0, -1 })
                Expect<ArgumentOutOfRangeException>(() => new NpcGenerationRequest("request-a", Selection(), Context(), Plan(), budget));
        });
        Check("successful generation changes dialogue only", () =>
        {
            var request = Request();
            Require(Resolve(request, new NpcGenerationResult("request-a", NpcGenerationStatus.Succeeded, "개운해!"),
                out var response), "valid response");
            Require(response.Reaction.Dialogue == "개운해!" && response.Source == NpcReactionSource.Generated, "generated dialogue");
            Require(response.Reaction.EmotionId == request.Plan.FallbackReaction.EmotionId &&
                response.Reaction.GestureId == request.Plan.FallbackReaction.GestureId, "game expression");
            Require(response.BranchId == request.Plan.BranchId && response.NextNodeId == request.Plan.NextNodeId &&
                Object.ReferenceEquals(response.NextChoices, request.Plan.NextChoices), "game branch and next choices");
            Require(response.RequestId == "request-a" && response.SessionId == "session-a" &&
                response.InteractionId == "interaction-a" && response.NpcId == "npc-a" && response.StateVersion == 5,
                "response correlation");
        });
        foreach (var failure in new[] { NpcGenerationStatus.TimedOut, NpcGenerationStatus.ModelUnavailable,
            NpcGenerationStatus.Failed, NpcGenerationStatus.InvalidOutput, NpcGenerationStatus.QueueFull })
        {
            Check("fallback for " + failure, () =>
            {
                var request = Request();
                Require(Resolve(request, new NpcGenerationResult("request-a", failure), out var response), "fallback resolved");
                Require(Object.ReferenceEquals(response.Reaction, request.Plan.FallbackReaction), "fallback identity");
                Require(response.Source == NpcReactionSource.Fallback && response.GenerationStatus == failure, "fallback status");
                Require(response.BranchId == request.Plan.BranchId && response.NextNodeId == request.Plan.NextNodeId &&
                    Object.ReferenceEquals(response.NextChoices, request.Plan.NextChoices), "fallback next flow");
            });
        }
        Check("mismatched request and stale active context block responses", () =>
        {
            var request = Request();
            var result = new NpcGenerationResult("request-a", NpcGenerationStatus.Succeeded, "늦은 대사");
            Require(!Resolve(request, new NpcGenerationResult("request-b", NpcGenerationStatus.Succeeded, "다른 요청"), out var mismatch)
                && mismatch == null, "result request id");
            var activeContexts = new[]
            {
                new[] { "session-b", "interaction-a", "npc-a" }, new[] { "session-a", "interaction-b", "npc-a" },
                new[] { "session-a", "interaction-a", "npc-b" }, new[] { null, "interaction-a", "npc-a" },
                new[] { "session-a", null, "npc-a" }, new[] { "session-a", "interaction-a", null }
            };
            foreach (var active in activeContexts)
                Require(!NpcInteractionResponse.TryResolve(request, result, "request-a", active[0], active[1], active[2], 5,
                    out var response) && response == null, "active correlation");
            foreach (long version in new long[] { 4, 6 })
                Require(!NpcInteractionResponse.TryResolve(request, result, "request-a", "session-a", "interaction-a", "npc-a", version,
                    out var response) && response == null, "state version");
            Require(!NpcInteractionResponse.TryResolve(request, new NpcGenerationResult("request-a", NpcGenerationStatus.TimedOut),
                "request-a", "session-a", "interaction-a", "npc-a", 6, out var fallback) && fallback == null, "stale fallback");
        });
        Check("replaced request in same interaction blocks prior completion", () =>
        {
            foreach (string active in new[] { "request-b", null })
                Require(!NpcInteractionResponse.TryResolve(Request(),
                    new NpcGenerationResult("request-a", NpcGenerationStatus.Succeeded, "이전 요청"), active,
                    "session-a", "interaction-a", "npc-a", 5, out var response) && response == null, "active request");
        });
        Check("cancelled generation produces no displayed fallback", () =>
        {
            Require(!Resolve(Request(), new NpcGenerationResult("request-a", NpcGenerationStatus.Cancelled), out var response)
                && response == null, "cancelled");
        });
        Check("terminal plan keeps interaction closed", () =>
        {
            var plan = new NpcResponsePlan("end", "종료", Reaction());
            var request = new NpcGenerationRequest("request-a", Selection(), Context(version: 5), plan, 1000);
            Require(plan.IsTerminal && Resolve(request, new NpcGenerationResult("request-a", NpcGenerationStatus.Succeeded, "잘 가!"),
                out var response) && response.NextNodeId == null && response.NextChoices.Count == 0, "terminal flow");
        });
        Check("invalid dialogue is rejected and exact length limit is accepted", () =>
        {
            foreach (string text in new[] { null, "", " ", "줄\n바꿈", "탭\t문자", "제어\u0000문자",
                new string('가', NpcInteractionContract.MaxDialogueLength + 1) })
            {
                Expect<ArgumentException>(() => new NpcReaction(text, "happy", "nod"));
                Expect<ArgumentException>(() => new NpcGenerationResult("request-a", NpcGenerationStatus.Succeeded, text));
            }
            string boundary = new string('가', NpcInteractionContract.MaxDialogueLength);
            Require(new NpcReaction(boundary, "happy", "nod").Dialogue == boundary, "reaction boundary");
            Require(new NpcGenerationResult("request-a", NpcGenerationStatus.Succeeded, boundary).Dialogue == boundary, "result boundary");
        });
        Check("undefined status and failure dialogue are rejected", () =>
        {
            Expect<ArgumentOutOfRangeException>(() => new NpcGenerationResult("request-a", (NpcGenerationStatus)999));
            Expect<ArgumentException>(() => new NpcGenerationResult("request-a", NpcGenerationStatus.TimedOut, "미완성"));
            Expect<ArgumentException>(() => new NpcGenerationResult("request-a", NpcGenerationStatus.Cancelled, ""));
        });
        Check("next node and choices must exist together", () =>
        {
            Expect<ArgumentException>(() => new NpcResponsePlan("thanks", "감사", Reaction(), nextNodeId: "root"));
            Expect<ArgumentException>(() => new NpcResponsePlan("thanks", "감사", Reaction(),
                nextChoices: new[] { new NpcInteractionChoice("greet", "인사") }));
            Expect<ArgumentException>(() => new NpcResponsePlan("thanks", "감사", Reaction(), nextNodeId: "root",
                nextChoices: Array.Empty<NpcInteractionChoice>()));
        });
        Check("null duplicate and excessive choices are rejected", () =>
        {
            RejectChoices(new NpcInteractionChoice[] { null });
            RejectChoices(new[] { new NpcInteractionChoice("care", "관리"), new NpcInteractionChoice("care", "다른 이름") });
            var many = new List<NpcInteractionChoice>();
            for (int i = 0; i <= NpcInteractionContract.MaxChoices; i++) many.Add(new NpcInteractionChoice("action-" + i, "행동"));
            RejectChoices(many);
        });
        Check("disabled choice requires reason and enabled choice rejects it", () =>
        {
            Expect<ArgumentException>(() => new NpcInteractionChoice("gift", "선물", false));
            Expect<ArgumentException>(() => new NpcInteractionChoice("gift", "선물", true, "불가"));
            Require(!new NpcInteractionChoice("gift", "선물", false, "선물 없음").IsEnabled, "disabled choice");
        });
        Check("invalid identity and bounded context are rejected", () =>
        {
            foreach (string id in new[] { null, "", "npc a", "도깨비", "npc/a" })
                Expect<ArgumentException>(() => Context(npcId: id));
            Expect<ArgumentException>(() => new NpcContextSnapshot("npc-a", "spirit-a", "도깨비", 4,
                traits: RepeatedText(NpcInteractionContract.MaxTraits + 1)));
            Expect<ArgumentException>(() => new NpcContextSnapshot("npc-a", "spirit-a", "도깨비", 4,
                recentContext: RepeatedText(NpcInteractionContract.MaxRecentContextEntries + 1)));
            Expect<ArgumentException>(() => new NpcResponsePlan("thanks", "감사", Reaction(),
                facts: RepeatedText(NpcInteractionContract.MaxFacts + 1)));
            Expect<ArgumentException>(() => new NpcContextSnapshot("npc-a", "spirit-a", "도깨비", 4, traits: new string[] { null }));
            Expect<ArgumentException>(() => new NpcContextSnapshot("npc-a", "spirit-a", "도깨비", 4,
                recentContext: new[] { new string('가', NpcInteractionContract.MaxContextEntryLength + 1) }));
        });
        Check("required contract objects reject null", () =>
        {
            Expect<ArgumentNullException>(() => new NpcInteractionPage("session-a", "root", null, null));
            Expect<ArgumentNullException>(() => new NpcResponsePlan("thanks", "감사", null));
            Expect<ArgumentNullException>(() => new NpcGenerationRequest("request-a", null, Context(), Plan(), 1000));
            Expect<ArgumentNullException>(() => new NpcGenerationRequest("request-a", Selection(), null, Plan(), 1000));
            Expect<ArgumentNullException>(() => new NpcGenerationRequest("request-a", Selection(), Context(), null, 1000));
            Expect<ArgumentNullException>(() => Resolve(null, new NpcGenerationResult("request-a", NpcGenerationStatus.Failed), out _));
            Expect<ArgumentNullException>(() => Resolve(Request(), null, out _));
        });
        return Passed.ToArray();
    }

    /// <summary>시나리오를 실행하고 성공 이름을 기록하며 실패에는 시나리오 이름을 추가합니다.</summary>
    /// <param name="name">시나리오 이름입니다.</param>
    /// <param name="action">실행할 검사입니다.</param>
    /// <exception cref="InvalidOperationException">검사에서 예외가 발생한 경우입니다. 원인은 내부 예외로 보존합니다.</exception>
    private static void Check(string name, Action action)
    {
        try { action(); }
        catch (Exception failure) { throw new InvalidOperationException("FAILED: " + name, failure); }
        Passed.Add("PASS: " + name);
    }

    /// <summary>필수 조건을 만족하지 못하면 검사를 중단합니다.</summary>
    /// <param name="valid">검사한 조건입니다.</param>
    /// <param name="message">실패 원인을 설명하는 메시지입니다.</param>
    /// <exception cref="InvalidOperationException">조건이 false인 경우입니다.</exception>
    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }

    /// <summary>검사 동작이 예상 예외를 발생시키는지 확인합니다. 다른 예외는 그대로 전파합니다.</summary>
    /// <typeparam name="T">예상하는 예외 형식입니다.</typeparam>
    /// <param name="action">예외를 발생시켜야 하는 동작입니다.</param>
    /// <exception cref="InvalidOperationException">예상 예외가 발생하지 않은 경우입니다.</exception>
    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    /// <summary>검사용 NPC 상태를 지정한 개체와 버전으로 만듭니다.</summary>
    /// <param name="npcId">검사할 개체 ID입니다.</param>
    /// <param name="version">검사할 상태 버전입니다.</param>
    /// <returns>검사용 불변 상태입니다.</returns>
    private static NpcContextSnapshot Context(string npcId = "npc-a", long version = 4)
    {
        return new NpcContextSnapshot(npcId, "spirit-a", "도깨비", version);
    }

    /// <summary>식별자와 버전 불일치 검사용 입력을 만듭니다.</summary>
    /// <param name="sessionId">검사할 세션 ID입니다.</param>
    /// <param name="npcId">검사할 개체 ID입니다.</param>
    /// <param name="nodeId">검사할 입력 노드 ID입니다.</param>
    /// <param name="actionId">검사할 행동 ID입니다.</param>
    /// <param name="version">화면이 본 기대 상태 버전입니다.</param>
    /// <returns>검사용 입력입니다.</returns>
    private static NpcInteractionSelection Selection(string sessionId = "session-a", string npcId = "npc-a",
        string nodeId = "root", string actionId = "care", long version = 4)
    {
        return new NpcInteractionSelection(sessionId, "interaction-a", npcId, nodeId, actionId, version);
    }

    /// <summary>검사용 기본 대사와 감정 및 동작을 만듭니다.</summary>
    /// <returns>검사용 기본 반응입니다.</returns>
    private static NpcReaction Reaction() { return new NpcReaction("고마워!", "relieved", "nod"); }

    /// <summary>다음 선택지가 있는 관리 감사 계획을 만듭니다.</summary>
    /// <returns>검사용 출력 계획입니다.</returns>
    private static NpcResponsePlan Plan()
    {
        return new NpcResponsePlan("care-thanks", "감사", Reaction(), new[] { "오염도가 낮아졌다." },
            "root", new[] { new NpcInteractionChoice("greet", "인사") });
    }

    /// <summary>검사용 효과 후 상태와 총 시간 예산을 묶어 요청을 만듭니다.</summary>
    /// <param name="context">검사할 상태입니다. null이면 버전 5 상태를 사용합니다.</param>
    /// <returns>검사용 생성 요청입니다.</returns>
    private static NpcGenerationRequest Request(NpcContextSnapshot context = null)
    {
        return new NpcGenerationRequest("request-a", Selection(), context ?? Context(version: 5), Plan(), 1000);
    }

    /// <summary>고정된 현재 대상 정보로 최종 응답의 수락 여부를 검사합니다.</summary>
    /// <param name="request">대상 요청입니다.</param>
    /// <param name="result">검사할 생성 결과입니다.</param>
    /// <param name="response">수락한 응답이며 거부 시 null입니다.</param>
    /// <returns>현재 요청의 결과가 수락되었으면 true입니다.</returns>
    private static bool Resolve(NpcGenerationRequest request, NpcGenerationResult result, out NpcInteractionResponse response)
    {
        return NpcInteractionResponse.TryResolve(request, result, "request-a", "session-a", "interaction-a", "npc-a", 5, out response);
    }

    /// <summary>부적합 선택지가 페이지와 계획 모두에서 거부되는지 검사합니다.</summary>
    /// <param name="choices">거부되어야 하는 선택지 목록입니다.</param>
    private static void RejectChoices(IEnumerable<NpcInteractionChoice> choices)
    {
        Expect<ArgumentException>(() => new NpcInteractionPage("session-a", "root", Context(), choices));
        Expect<ArgumentException>(() => new NpcResponsePlan("thanks", "감사", Reaction(), nextNodeId: "root", nextChoices: choices));
    }

    /// <summary>목록 항목 수 경계 검사용 사실 배열을 만듭니다.</summary>
    /// <param name="count">생성할 항목 수입니다.</param>
    /// <returns>동일한 사실을 지정 개수만큼 담은 배열입니다.</returns>
    private static string[] RepeatedText(int count)
    {
        var values = new string[count];
        for (int i = 0; i < count; i++) values[i] = "사실";
        return values;
    }
}
'@

$harnessPath = Join-Path ([IO.Path]::GetTempPath()) ('npc-contract-check-' + [guid]::NewGuid().ToString('N') + '.cs')
try {
    Set-Content -LiteralPath $harnessPath -Value $harnessSource -Encoding utf8
    $reportDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($ReportPath))
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $documentationPath = Join-Path $reportDirectory 'contracts-documentation.xml'
    # /doc는 XML 주석 진단을 활성화합니다. Add-Type의 메모리 컴파일은 XML 파일을 내보내지 않습니다.
    Add-Type -Path (@($contractSources.FullName) + @($harnessPath)) -CompilerOptions @(
        ('/doc:"' + $documentationPath + '"'),
        '/warnaserror:1591,1570,1572,1573,1574,1587'
    )
    $results = [NpcInteractionPortableVerification]::Run()
    $lines = @(
        'ALL PORTABLE CONTRACT CHECKS PASSED'
        "Pure C# scenario groups: $($results.Count)"
        "PowerShell version: $($PSVersionTable.PSVersion)"
        'XML documentation: public member coverage and XML/parameter/reference warnings checked.'
        'Not executed: Unity NUnit EditMode, Player, IL2CPP, actual inference, Android.'
    ) + @($results)
    Set-Content -LiteralPath $ReportPath -Value $lines -Encoding utf8
    Write-Output "순수 C# 계약 시나리오 $($results.Count)개 통과. Unity/Android 검증은 실행하지 않았습니다. 결과: $ReportPath"
}
finally {
    if (Test-Path -LiteralPath $harnessPath) { Remove-Item -LiteralPath $harnessPath -Force }
}
