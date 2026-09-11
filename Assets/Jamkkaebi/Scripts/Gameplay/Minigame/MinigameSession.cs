using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
{
    public class MinigameSession
    {
        public MinigameGrid Grid { get; }                               // 미니게임 대상 그리드
        public SessionState State { get; private set; }                             // 게임 상태 (진행 중, 성공, 실패)

        public float DamageGauge                                        // 손상도 게이지
        {
            // (지금까지 열린 위협 타일의 개수)와 (페이즈별 허용 위협타일 개수)의 비율로 계산
            // damagePerThreat을 누적계산하면 합이 1이 되지 않는 문제가 있음
            // public float DamageGauge => Mathf.Clamp((float)_threatHtisRevealed / _allowedThreatHits);
            get { return Mathf.Clamp01((float)_threatHitsRevealed / _allowedThreatHits); }
        }

        private readonly int _allowedThreatHits;                        // 허용 위협타일 개수
        private int _threatHitsRevealed;                                // 지금까지 열린 위협 타일 개수
        public int RemainingDestructiveToolUses { get; private set; }   // 남은 타일 파괴 시도 횟수(공격형, 안전형 도구)
        public int RemainingScoutToolUses { get; private set; }         // 남은 정찰 도구 사용 횟수
        public float RemainingSeconds { get; private set; }             // 현재 남은시간
        
        public event Action<TileRevealResult> TileRevealAttempted;
        
        public MinigameSession(MinigamePhaseConfig config)
            : this(MinigameGridGenerator.Generate(config), config)
        {
        }

        public MinigameSession(MinigameGrid grid, MinigamePhaseConfig config)
        {
            Grid = grid;
            State = SessionState.InProgress;
            _allowedThreatHits = config.AllowedThreatHits;
            _threatHitsRevealed = 0;
            RemainingDestructiveToolUses = config.DestructiveToolLimit;
            RemainingScoutToolUses = config.ScoutToolLimit;
            RemainingSeconds = config.TimeLimit;
        }

        public void AdvanceTime(float deltaTime)
        {
            if (State != SessionState.InProgress)
            {
                return;
            }
            
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - deltaTime);
            EvaluateEndCondition();
        }

        public void EvaluateEndCondition()
        {
            if (State != SessionState.InProgress)
            {
                return;
            }
            
            bool allExcavated = Grid.AreAllPolyominoesExcavated();

            // 성공/실패 판단. 성공 검사를 우선하여, 실패 조건 충족과 성공 조건 충족이 동시에 발생하면 성공으로 판정
            if (allExcavated)
            {
                State = SessionState.Succeeded;
            }
            else if (RemainingSeconds <= 0 || DamageGauge >= 1.0f || RemainingDestructiveToolUses <= 0)
            {
                State = SessionState.Failed;
            }
        }

        private List<Vector2Int> GetAffectedCoordinates(Vector2Int origin, DestructiveToolType type)
        {
            List<Vector2Int> candidates = new List<Vector2Int>();

            switch (type)
            {
                case DestructiveToolType.Safe:
                    candidates.Add(origin);
                    break;
                case DestructiveToolType.Attack:
                    candidates.Add(origin);
                    candidates.Add(new Vector2Int(origin.x, origin.y-1));
                    candidates.Add(new Vector2Int(origin.x-1, origin.y));
                    candidates.Add(new Vector2Int(origin.x+1, origin.y));
                    candidates.Add(new Vector2Int(origin.x, origin.y+1));
                    break;
                default:
                    throw new ArgumentException($"{type}은 잘못된 파괴형 도구입니다.", nameof(type));
            }
            
            return candidates.Where(c => Grid.InBounds(c.x, c.y)).ToList();
        }

        public DestroyResult UseDestructiveTool(Vector2Int origin, DestructiveToolType type)
        {
            // 가드1: 세션이 진행중이 아닐 때 -> SessionNotInProgress 반환
            if (State != SessionState.InProgress)
            {
                return DestroyResult.SessionNotInProgress;
            }
            
            // 가드2: 도구 사용 횟수를 모두 소진했을 때 -> NoUsesRemaining 반환
            if (RemainingDestructiveToolUses <= 0)
            {
                return DestroyResult.NoUsesRemaining;
            }
            
            // 가드3: origin이 범위 바깥일 때
            if (!Grid.InBounds(origin.x, origin.y))
            {
                return DestroyResult.InvalidOrigin;
            }
            
            // 이번 시행에서 영향받는 좌표 구하기(InBound 검사만 완료됨, 이미 Reveal되었는지 여부는 다음 step에)
            List<Vector2Int> coordinates = GetAffectedCoordinates(origin, type);

            List<TileRevealResult> results = new  List<TileRevealResult>();
            
            // Helper 타일이 연쇄반응을 일으킬 수 있기 때문에 queue 사용
            Queue<Vector2Int> toProcess = new Queue<Vector2Int>(coordinates);

            while (toProcess.Count > 0)
            {
                // queue에서 좌표를 뽑아 RevealOutcome 만들고 results에 추가
                Vector2Int c = toProcess.Dequeue();
                Tile tile = Grid.GetTile(c.x, c.y);
                RevealOutcome outcome = tile.Reveal();
                results.Add(new TileRevealResult(c, tile.Content, outcome));

                // Helper 타일 개봉 분기
                // 가로와 세로 중 남은 타일이 더 많은 방향으로 queue에 등록
                // 남은 타일 개수가 같다면 가로를 우선으로
                if (tile.Content == TileContent.Helper && outcome == RevealOutcome.Revealed)
                {
                    IReadOnlyList<Vector2Int> row = Grid.GetRow(c.y);
                    IReadOnlyList<Vector2Int> column = Grid.GetColumn(c.x);
                    
                    int rowTileCount = row.Count(t => Grid.GetTile(t.x, t.y).IsRevealed == false);
                    int colTileCount = column.Count(t => Grid.GetTile(t.x, t.y).IsRevealed == false);
                    
                    if (rowTileCount >= colTileCount)
                    {
                        foreach (Vector2Int coord in row)
                        {
                            toProcess.Enqueue(coord);
                        }
                    }
                    else
                    {
                        foreach (Vector2Int coord in column)
                        {
                            toProcess.Enqueue(coord);
                        }
                    }
                }
            }
            
            // 가드4: 이번 시행에 영향받는 좌표의 타일이 이미 모두 열려있다면 -> NoValidTargets 반환
            if (results.All(r => r.Outcome == RevealOutcome.AlreadyRevealed))
            {
                return DestroyResult.NoValidTargets;
            }

            // 이번 시행으로 열린 위협 타일 개수를 세고 카운터에 누적
            int newThreatHits = results.Count(r =>
                r.Content == TileContent.Threat && r.Outcome == RevealOutcome.Revealed);
            _threatHitsRevealed += newThreatHits;
            
            // 도구 사용 횟수 차감
            RemainingDestructiveToolUses -= 1;
            
            EvaluateEndCondition();
            
            // TileRevealAttempted 이벤트 발행
            foreach (TileRevealResult r in results)
            {
                TileRevealAttempted?.Invoke(r);
            }
            
            return DestroyResult.Success;       // 정상 종료
        }

        public ScoutResult UseScoutTool(Vector2Int origin, out int threatCount)
        {
            threatCount = 0;
            
            // 가드1: 세션이 진행중이 아닐 때 -> SessionNotInProgress 반환
            if (State != SessionState.InProgress)
            {
                return ScoutResult.SessionNotInProgress;
            }
            
            // 가드2: 도구 사용 횟수를 모두 소진했을 때 -> NoUsesRemaining 반환
            if (RemainingScoutToolUses <= 0)
            {
                return ScoutResult.NoUsesRemaining;
            }
            
            // 가드3: origin이 범위 바깥일 때
            if (!Grid.InBounds(origin.x, origin.y))
            {
                return ScoutResult.InvalidOrigin;
            }

            List<Vector2Int> coordinates = GetSurroundingCoordinates(origin);
            
            threatCount = coordinates.Count(c => Grid.GetTile(c.x, c.y).Content == TileContent.Threat);

            RemainingScoutToolUses -= 1;
            
            return ScoutResult.Success;
        }

        private List<Vector2Int> GetSurroundingCoordinates(Vector2Int origin)
        {
            List<Vector2Int> candidates = new List<Vector2Int>();
            
            candidates.Add(new Vector2Int(origin.x-1, origin.y-1));
            candidates.Add(new Vector2Int(origin.x, origin.y-1));
            candidates.Add(new Vector2Int(origin.x+1, origin.y-1));
            candidates.Add(new Vector2Int(origin.x-1, origin.y));
            candidates.Add(new Vector2Int(origin.x+1, origin.y));
            candidates.Add(new Vector2Int(origin.x-1, origin.y+1));
            candidates.Add(new Vector2Int(origin.x, origin.y+1));
            candidates.Add(new Vector2Int(origin.x+1, origin.y+1));
            
            return candidates.Where(c => Grid.InBounds(c.x, c.y)).ToList();
        }
    }
}