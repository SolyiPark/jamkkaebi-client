using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Npc.Interaction;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    /// <summary>공통 규약의 불변성, 경계값, 분기 보존 및 오래된 결과 차단을 검증하는 EditMode 테스트입니다.</summary>
    public sealed class NpcInteractionContractTests
    {
        /// <summary>미확인 상태와 알려진 0이 구분되고 null 목록은 빈 목록이 되는지 검사합니다.</summary>
        [Test]
        public void Snapshot_UnknownStateIsDistinctFromKnownZero()
        {
            NpcContextSnapshot unknown = Context();
            NpcContextSnapshot known = new NpcContextSnapshot("npc-a", "spirit-a", "도깨비", 4,
                careCount: 0, pollution: 0);

            Assert.IsNull(unknown.CareCount);
            Assert.IsNull(unknown.Pollution);
            Assert.AreEqual(0, unknown.Traits.Count);
            Assert.AreEqual(0, unknown.RecentContext.Count);
            Assert.AreEqual(0, known.CareCount);
            Assert.AreEqual(0f, known.Pollution);
        }

        /// <summary>원본 특성 및 기록 목록의 변경이 스냅샷에 전파되지 않고 읽기 전용 목록의 쓰기가 거부되는지 검사합니다.</summary>
        [Test]
        public void Snapshot_CopiesCallerListsAndRejectsWritesThroughListInterface()
        {
            var traits = new List<string> { "장난스러움" };
            var recent = new List<string> { "관리를 한 번 받았다." };
            var context = new NpcContextSnapshot("npc-a", "spirit-a", "도깨비", 4,
                traits, recentContext: recent);
            traits[0] = "수정된 특성";
            recent.Clear();

            CollectionAssert.AreEqual(new[] { "장난스러움" }, context.Traits);
            CollectionAssert.AreEqual(new[] { "관리를 한 번 받았다." }, context.RecentContext);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)context.Traits)[0] = "변경");
            Assert.Throws<NotSupportedException>(() => ((IList<string>)context.RecentContext).Clear());
        }

        /// <summary>페이지와 계획이 선택지 및 사실을 복사하고 외부 쓰기를 거부하는지 검사합니다.</summary>
        [Test]
        public void PageAndPlan_CopyChoicesAndFactsAndExposeReadOnlyLists()
        {
            var choices = new List<NpcInteractionChoice> { new NpcInteractionChoice("greet", "인사하기") };
            var facts = new List<string> { "관리를 받았다." };
            var page = new NpcInteractionPage("session-a", "root", Context(), choices);
            var plan = new NpcResponsePlan("thanks", "감사를 표현한다.", Reaction(), facts,
                "root", choices);
            choices.Clear();
            facts[0] = "다른 사실";

            Assert.AreEqual("greet", page.Choices[0].ActionId);
            Assert.AreEqual("greet", plan.NextChoices[0].ActionId);
            Assert.AreEqual("관리를 받았다.", plan.Facts[0]);
            Assert.Throws<NotSupportedException>(() => ((IList<NpcInteractionChoice>)page.Choices).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<NpcInteractionChoice>)plan.NextChoices).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<string>)plan.Facts)[0] = "변경");
        }

        /// <summary>범위 밖이거나 비유한 오염도가 거부되는지 검사합니다.</summary>
        /// <param name="pollution">유한한 0부터 1까지의 오염도이며 미확인은 null입니다.</param>
        [TestCase(-0.01f)]
        [TestCase(1.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Snapshot_InvalidPollutionIsRejected(float pollution)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NpcContextSnapshot(
                "npc-a", "spirit-a", "도깨비", 4, pollution: pollution));
        }

        /// <summary>음수 관리 횟수와 상태 및 기대 버전이 거부되는지 검사합니다.</summary>
        [Test]
        public void Snapshot_NegativeCareCountAndVersionAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NpcContextSnapshot(
                "npc-a", "spirit-a", "도깨비", 4, careCount: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Context(version: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Selection(version: -1));
        }

        /// <summary>세션, 노드, NPC, 버전이 맞는 활성 행동만 허용되는지 검사합니다.</summary>
        [Test]
        public void Page_OnlyMatchingEnabledSelectionIsAccepted()
        {
            var page = new NpcInteractionPage("session-a", "root", Context(), new[]
            {
                new NpcInteractionChoice("care", "관리하기"),
                new NpcInteractionChoice("gift", "선물하기", false, "선물이 없습니다.")
            });

            Assert.IsTrue(page.CanSelect(Selection()));
            Assert.IsFalse(page.CanSelect(null));
            Assert.IsFalse(page.CanSelect(Selection(actionId: "gift")));
            Assert.IsFalse(page.CanSelect(Selection(actionId: "unknown")));
            Assert.IsFalse(page.CanSelect(Selection(sessionId: "session-b")));
            Assert.IsFalse(page.CanSelect(Selection(nodeId: "another-node")));
            Assert.IsFalse(page.CanSelect(Selection(npcId: "npc-b")));
            Assert.IsFalse(page.CanSelect(Selection(version: 3)));
            Assert.IsFalse(page.CanSelect(Selection(version: 5)));
        }

        /// <summary>효과 후 상태는 같거나 증가한 버전을 허용하고 이전 버전 및 다른 NPC를 거부하는지 검사합니다.</summary>
        [Test]
        public void Request_AcceptsMatchingPostEffectStateButRejectsOlderOrOtherNpc()
        {
            NpcGenerationRequest changed = Request(context: Context(version: 5));
            NpcGenerationRequest unchanged = Request(context: Context(version: 4));

            Assert.AreEqual(5, changed.Context.StateVersion);
            Assert.AreEqual(4, unchanged.Context.StateVersion);
            Assert.AreEqual(NpcInteractionContract.Version, changed.ContractVersion);
            Assert.Throws<ArgumentException>(() => Request(context: Context(version: 3)));
            Assert.Throws<ArgumentException>(() => Request(context: Context(npcId: "npc-b")));
        }

        /// <summary>양수가 아닌 총 시간 예산이 거부되는지 검사합니다.</summary>
        /// <param name="budget">거부 대상인 양수가 아닌 시간 예산입니다.</param>
        [TestCase(0)]
        [TestCase(-1)]
        public void Request_NonPositiveTotalTimeBudgetIsRejected(int budget)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NpcGenerationRequest(
                "request-a", Selection(), Context(), Plan(), budget));
        }

        /// <summary>생성 성공이 대사만 바꾸고 게임이 선택한 분기, 감정, 동작, 다음 선택지를 보존하는지 검사합니다.</summary>
        [Test]
        public void Response_SuccessChangesOnlyDialogueAndKeepsGameChosenBranchAndExpression()
        {
            NpcGenerationRequest request = Request();
            bool resolved = NpcInteractionResponse.TryResolve(request,
                new NpcGenerationResult("request-a", NpcGenerationStatus.Succeeded, "덕분에 개운해!"),
                "request-a", "session-a", "interaction-a", "npc-a", 5, out NpcInteractionResponse response);

            Assert.IsTrue(resolved);
            Assert.AreEqual("request-a", response.RequestId);
            Assert.AreEqual("session-a", response.SessionId);
            Assert.AreEqual("interaction-a", response.InteractionId);
            Assert.AreEqual("npc-a", response.NpcId);
            Assert.AreEqual(5, response.StateVersion);
            Assert.AreEqual("덕분에 개운해!", response.Reaction.Dialogue);
            Assert.AreEqual(request.Plan.BranchId, response.BranchId);
            Assert.AreEqual(request.Plan.FallbackReaction.EmotionId, response.Reaction.EmotionId);
            Assert.AreEqual(request.Plan.FallbackReaction.GestureId, response.Reaction.GestureId);
            Assert.AreEqual(request.Plan.NextNodeId, response.NextNodeId);
            Assert.AreSame(request.Plan.NextChoices, response.NextChoices);
            Assert.AreEqual(NpcReactionSource.Generated, response.Source);
            Assert.AreEqual(NpcGenerationStatus.Succeeded, response.GenerationStatus);
        }

        /// <summary>각 유효한 실패가 같은 계획의 기본 반응과 다음 선택지를 그대로 사용하는지 검사합니다.</summary>
        /// <param name="status">정의된 생성 완료 상태입니다.</param>
        [TestCase(NpcGenerationStatus.TimedOut)]
        [TestCase(NpcGenerationStatus.ModelUnavailable)]
        [TestCase(NpcGenerationStatus.Failed)]
        [TestCase(NpcGenerationStatus.InvalidOutput)]
        [TestCase(NpcGenerationStatus.QueueFull)]
        public void Response_FailureUsesExactlyTheSelectedFallbackAndNextChoices(NpcGenerationStatus status)
        {
            NpcGenerationRequest request = Request();
            Assert.IsTrue(NpcInteractionResponse.TryResolve(request,
                new NpcGenerationResult("request-a", status), "request-a", "session-a", "interaction-a", "npc-a", 5,
                out NpcInteractionResponse response));

            Assert.AreSame(request.Plan.FallbackReaction, response.Reaction);
            Assert.AreEqual(request.Plan.BranchId, response.BranchId);
            Assert.AreEqual(request.Plan.NextNodeId, response.NextNodeId);
            Assert.AreSame(request.Plan.NextChoices, response.NextChoices);
            Assert.AreEqual(NpcReactionSource.Fallback, response.Source);
            Assert.AreEqual(status, response.GenerationStatus);
        }

        /// <summary>요청, 세션, 입력, NPC 또는 버전 불일치 결과에 표시할 응답이 없는지 검사합니다.</summary>
        /// <param name="requestId">어댑터가 부여한 생성 시도 ID입니다.</param>
        /// <param name="sessionId">현재 화면 세션 ID입니다.</param>
        /// <param name="interactionId">한 입력 의도의 ID입니다.</param>
        /// <param name="npcId">도깨비 인스턴스 ID입니다.</param>
        /// <param name="stateVersion">비교할 상호작용 의미 상태 버전입니다.</param>
        [TestCase("request-b", "session-a", "interaction-a", "npc-a", 5)]
        [TestCase("request-a", "session-b", "interaction-a", "npc-a", 5)]
        [TestCase("request-a", "session-a", "interaction-b", "npc-a", 5)]
        [TestCase("request-a", "session-a", "interaction-a", "npc-b", 5)]
        [TestCase("request-a", "session-a", "interaction-a", "npc-a", 4)]
        [TestCase("request-a", "session-a", "interaction-a", "npc-a", 6)]
        [TestCase("request-a", null, "interaction-a", "npc-a", 5)]
        [TestCase("request-a", "session-a", null, "npc-a", 5)]
        [TestCase("request-a", "session-a", "interaction-a", null, 5)]
        public void Response_StaleOrMismatchedCompletionHasNoVisibleResponse(string requestId,
            string sessionId, string interactionId, string npcId, long stateVersion)
        {
            Assert.IsFalse(NpcInteractionResponse.TryResolve(Request(),
                new NpcGenerationResult(requestId, NpcGenerationStatus.Succeeded, "늦은 대사"),
                "request-a", sessionId, interactionId, npcId, stateVersion, out NpcInteractionResponse response));
            Assert.IsNull(response);
        }

        /// <summary>취소 결과가 생성 대사와 기본 대사 모두를 표시하지 않는지 검사합니다.</summary>
        [Test]
        public void Response_CancelledRequestHasNoFallbackOrGeneratedReaction()
        {
            Assert.IsFalse(NpcInteractionResponse.TryResolve(Request(),
                new NpcGenerationResult("request-a", NpcGenerationStatus.Cancelled),
                "request-a", "session-a", "interaction-a", "npc-a", 5, out NpcInteractionResponse response));
            Assert.IsNull(response);
        }

        /// <summary>같은 상호작용이라도 교체되거나 닫힌 생성 요청의 결과가 거부되는지 검사합니다.</summary>
        /// <param name="activeRequestId">현재 활성 생성 시도 ID입니다. 종료 또는 교체된 요청을 차단합니다.</param>
        [TestCase("request-b")]
        [TestCase(null)]
        public void Response_ReplacedOrClosedRequestIsRejectedWithinSameInteraction(string activeRequestId)
        {
            Assert.IsFalse(NpcInteractionResponse.TryResolve(Request(),
                new NpcGenerationResult("request-a", NpcGenerationStatus.Succeeded, "이전 요청의 대사"),
                activeRequestId, "session-a", "interaction-a", "npc-a", 5, out NpcInteractionResponse response));
            Assert.IsNull(response);
        }

        /// <summary>오래된 상태의 실패 결과도 기본 대사를 표시하지 않는지 검사합니다.</summary>
        [Test]
        public void Response_FallbackForStaleStateIsAlsoSuppressed()
        {
            Assert.IsFalse(NpcInteractionResponse.TryResolve(Request(),
                new NpcGenerationResult("request-a", NpcGenerationStatus.TimedOut),
                "request-a", "session-a", "interaction-a", "npc-a", 6, out NpcInteractionResponse response));
            Assert.IsNull(response);
        }

        /// <summary>종료 계획의 응답에 다음 노드와 선택지가 생기지 않는지 검사합니다.</summary>
        [Test]
        public void Response_TerminalPlanPreservesEndOfInteraction()
        {
            var terminal = new NpcResponsePlan("end", "인사를 마친다.", Reaction());
            var request = new NpcGenerationRequest("request-a", Selection(), Context(version: 5), terminal, 1000);
            Assert.IsTrue(terminal.IsTerminal);
            Assert.IsTrue(NpcInteractionResponse.TryResolve(request,
                new NpcGenerationResult("request-a", NpcGenerationStatus.Succeeded, "다음에 보자!"),
                "request-a", "session-a", "interaction-a", "npc-a", 5, out NpcInteractionResponse response));
            Assert.IsNull(response.NextNodeId);
            Assert.AreEqual(0, response.NextChoices.Count);
        }

        /// <summary>빈 대사와 제어 문자 포함 대사가 반응 및 성공 결과에서 거부되는지 검사합니다.</summary>
        /// <param name="dialogue">한 줄 완성 대사입니다. 결과에서는 성공일 때만 지정하며 실패일 때 null이어야 합니다.</param>
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("두 줄\n대사")]
        [TestCase("탭\t문자")]
        [TestCase("제어\u0000문자")]
        public void Dialogue_EmptyOrControlCharactersAreRejected(string dialogue)
        {
            Assert.Throws<ArgumentException>(() => new NpcReaction(dialogue, "happy", "nod"));
            Assert.Throws<ArgumentException>(() => new NpcGenerationResult(
                "request-a", NpcGenerationStatus.Succeeded, dialogue));
        }

        /// <summary>최대 대사 길이 경계는 허용하고 초과 대사는 거부하는지 검사합니다.</summary>
        [Test]
        public void Dialogue_LimitAcceptsBoundaryAndRejectsLongerText()
        {
            string valid = new string('가', NpcInteractionContract.MaxDialogueLength);
            Assert.AreEqual(valid, new NpcReaction(valid, "happy", "nod").Dialogue);
            Assert.AreEqual(valid, new NpcGenerationResult("request-a", NpcGenerationStatus.Succeeded, valid).Dialogue);
            Assert.Throws<ArgumentException>(() => new NpcReaction(valid + "가", "happy", "nod"));
            Assert.Throws<ArgumentException>(() => new NpcGenerationResult(
                "request-a", NpcGenerationStatus.Succeeded, valid + "가"));
        }

        /// <summary>정의되지 않은 상태와 실패 결과의 미완성 대사가 거부되는지 검사합니다.</summary>
        [Test]
        public void Result_InvalidStatusAndPartialFailureDialogueAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NpcGenerationResult(
                "request-a", (NpcGenerationStatus)999));
            Assert.Throws<ArgumentException>(() => new NpcGenerationResult(
                "request-a", NpcGenerationStatus.TimedOut, "미완성"));
            Assert.Throws<ArgumentException>(() => new NpcGenerationResult(
                "request-a", NpcGenerationStatus.Cancelled, ""));
        }

        /// <summary>다음 노드와 선택지를 함께 지정하거나 함께 비워야 하는지 검사합니다.</summary>
        [Test]
        public void Plan_NextNodeAndChoicesMustBePresentTogether()
        {
            Assert.Throws<ArgumentException>(() => new NpcResponsePlan(
                "thanks", "감사", Reaction(), nextNodeId: "root"));
            Assert.Throws<ArgumentException>(() => new NpcResponsePlan(
                "thanks", "감사", Reaction(), nextChoices: new[] { new NpcInteractionChoice("greet", "인사") }));
            Assert.Throws<ArgumentException>(() => new NpcResponsePlan(
                "thanks", "감사", Reaction(), nextNodeId: "root", nextChoices: Array.Empty<NpcInteractionChoice>()));
        }

        /// <summary>페이지와 계획에서 null, 중복 행동 및 항목 수 초과를 거부하는지 검사합니다.</summary>
        [Test]
        public void Choices_NullDuplicateAndTooManyActionsAreRejectedInPagesAndPlans()
        {
            var first = new NpcInteractionChoice("care", "관리");
            AssertChoicesRejected(new[] { first, new NpcInteractionChoice("care", "다른 이름") });
            AssertChoicesRejected(new NpcInteractionChoice[] { null });
            var many = new List<NpcInteractionChoice>();
            for (int i = 0; i <= NpcInteractionContract.MaxChoices; i++)
                many.Add(new NpcInteractionChoice("action-" + i, "행동"));
            AssertChoicesRejected(many);
        }

        /// <summary>비활성 사유는 필수이고 활성 선택지에 불가 사유를 넣을 수 없는지 검사합니다.</summary>
        [Test]
        public void Choices_DisabledRequiresReasonAndEnabledCannotHaveReason()
        {
            Assert.Throws<ArgumentException>(() => new NpcInteractionChoice("gift", "선물", false));
            Assert.Throws<ArgumentException>(() => new NpcInteractionChoice("gift", "선물", true, "불가"));
            var valid = new NpcInteractionChoice("gift", "선물", false, "선물이 없습니다.");
            Assert.IsFalse(valid.IsEnabled);
            Assert.AreEqual("선물이 없습니다.", valid.UnavailableReason);
        }

        /// <summary>비어 있거나 허용하지 않는 문자를 포함한 NPC ID가 거부되는지 검사합니다.</summary>
        /// <param name="npcId">도깨비 인스턴스 ID입니다.</param>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("공백 ID")]
        [TestCase("npc/a")]
        [TestCase("npc a")]
        public void Identity_InvalidNpcIdIsRejected(string npcId)
        {
            Assert.Throws<ArgumentException>(() => Context(npcId));
        }

        /// <summary>특성, 기록, 사실 목록의 개수와 항목 형식 및 길이 제한을 검사합니다.</summary>
        [Test]
        public void BoundedContext_RejectsOversizedListsAndInvalidEntries()
        {
            Assert.Throws<ArgumentException>(() => new NpcContextSnapshot(
                "npc-a", "spirit-a", "도깨비", 4, traits: RepeatedText(NpcInteractionContract.MaxTraits + 1)));
            Assert.Throws<ArgumentException>(() => new NpcContextSnapshot(
                "npc-a", "spirit-a", "도깨비", 4, recentContext: RepeatedText(NpcInteractionContract.MaxRecentContextEntries + 1)));
            Assert.Throws<ArgumentException>(() => new NpcResponsePlan("thanks", "감사", Reaction(),
                facts: RepeatedText(NpcInteractionContract.MaxFacts + 1)));
            Assert.Throws<ArgumentException>(() => new NpcContextSnapshot(
                "npc-a", "spirit-a", "도깨비", 4, traits: new string[] { null }));
            Assert.Throws<ArgumentException>(() => new NpcContextSnapshot("npc-a", "spirit-a", "도깨비", 4,
                recentContext: new[] { new string('가', NpcInteractionContract.MaxContextEntryLength + 1) }));
        }

        /// <summary>필수 상태, 계획, 선택, 요청 및 결과의 null이 명시적으로 거부되는지 검사합니다.</summary>
        [Test]
        public void RequiredObjects_NullIsRejectedBeforeResolution()
        {
            Assert.Throws<ArgumentNullException>(() => new NpcInteractionPage("session-a", "root", null, null));
            Assert.Throws<ArgumentNullException>(() => new NpcResponsePlan("thanks", "감사", null));
            Assert.Throws<ArgumentNullException>(() => new NpcGenerationRequest("request-a", null, Context(), Plan(), 1000));
            Assert.Throws<ArgumentNullException>(() => new NpcGenerationRequest("request-a", Selection(), null, Plan(), 1000));
            Assert.Throws<ArgumentNullException>(() => new NpcGenerationRequest("request-a", Selection(), Context(), null, 1000));
            Assert.Throws<ArgumentNullException>(() => NpcInteractionResponse.TryResolve(null,
                new NpcGenerationResult("request-a", NpcGenerationStatus.Failed),
                "request-a", "session-a", "interaction-a", "npc-a", 5, out _));
            Assert.Throws<ArgumentNullException>(() => NpcInteractionResponse.TryResolve(Request(), null,
                "request-a", "session-a", "interaction-a", "npc-a", 5, out _));
        }

        /// <summary>테스트에서 사용할 NPC 상태를 지정된 개체와 버전으로 만듭니다.</summary>
        /// <param name="npcId">도깨비 인스턴스 ID입니다.</param>
        /// <param name="version">테스트할 0 이상 또는 거부 대상 상태 버전입니다.</param>
        /// <returns>테스트용 불변 상태입니다.</returns>
        private static NpcContextSnapshot Context(string npcId = "npc-a", long version = 4)
        {
            return new NpcContextSnapshot(npcId, "spirit-a", "도깨비", version);
        }

        /// <summary>테스트에서 사용할 입력을 지정한 세션, 개체, 노드, 행동, 버전으로 만듭니다.</summary>
        /// <param name="sessionId">현재 화면 세션 ID입니다.</param>
        /// <param name="npcId">도깨비 인스턴스 ID입니다.</param>
        /// <param name="nodeId">입력 트리 노드 ID입니다.</param>
        /// <param name="actionId">선택한 행동 ID입니다.</param>
        /// <param name="version">테스트할 0 이상 또는 거부 대상 상태 버전입니다.</param>
        /// <returns>테스트용 입력입니다.</returns>
        private static NpcInteractionSelection Selection(string sessionId = "session-a", string npcId = "npc-a",
            string nodeId = "root", string actionId = "care", long version = 4)
        {
            return new NpcInteractionSelection(sessionId, "interaction-a", npcId, nodeId, actionId, version);
        }

        /// <summary>테스트용 기본 대사와 감정 및 동작을 만듭니다.</summary>
        /// <returns>테스트용 기본 반응입니다.</returns>
        private static NpcReaction Reaction()
        {
            return new NpcReaction("고마워!", "relieved", "nod");
        }

        /// <summary>다음 선택지가 있는 관리 감사 계획을 테스트용으로 만듭니다.</summary>
        /// <returns>테스트용 출력 계획입니다.</returns>
        private static NpcResponsePlan Plan()
        {
            return new NpcResponsePlan("care-thanks", "관리받은 것에 감사한다.", Reaction(),
                new[] { "오염도가 낮아졌다." }, "root", new[] { new NpcInteractionChoice("greet", "인사하기") });
        }

        /// <summary>테스트용 효과 후 상태와 1000ms 예산을 가진 요청을 만듭니다.</summary>
        /// <param name="context">불변 NPC 상태입니다. 요청에서는 입력과 같은 NPC의 효과 적용 후 상태여야 합니다.</param>
        /// <returns>테스트용 생성 요청입니다.</returns>
        private static NpcGenerationRequest Request(NpcContextSnapshot context = null)
        {
            return new NpcGenerationRequest("request-a", Selection(), context ?? Context(version: 5), Plan(), 1000);
        }

        /// <summary>같은 부적합 선택지 목록이 페이지와 계획 모두에서 거부되는지 검사합니다.</summary>
        /// <param name="choices">현재 선택지입니다. null은 빈 목록이며 중복 행동 ID는 허용하지 않습니다.</param>
        private static void AssertChoicesRejected(IEnumerable<NpcInteractionChoice> choices)
        {
            Assert.Throws<ArgumentException>(() => new NpcInteractionPage("session-a", "root", Context(), choices));
            Assert.Throws<ArgumentException>(() => new NpcResponsePlan(
                "thanks", "감사", Reaction(), nextNodeId: "root", nextChoices: choices));
        }

        /// <summary>목록 개수 경계 검사용 동일한 사실 문자열 배열을 만듭니다.</summary>
        /// <param name="count">생성할 경계 검사 항목 수입니다.</param>
        /// <returns>지정 개수의 사실 배열입니다.</returns>
        private static string[] RepeatedText(int count)
        {
            var values = new string[count];
            for (int i = 0; i < count; i++) values[i] = "사실";
            return values;
        }
    }
}
