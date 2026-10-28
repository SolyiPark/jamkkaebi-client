using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Data
{
    /// <summary>
    /// 서버 없이 에디터에서 미니게임을 돌릴 때 쓰는 난이도별 보드 설정 테이블.
    /// </summary>
    /// <remarks>
    /// 값은 서버 DifficultyParams 기본값을 옮겨 둔 임시값이다. 서버 연동 후에는 서버 응답의 보드 파라미터를 쓴다.
    /// 서버의 형상 안/밖 강화 타일 수는 클라이언트가 아직 구분하지 않으므로 합쳐서 넣는다.
    /// </remarks>
    [CreateAssetMenu(fileName = "MinigameDifficultyTable", menuName = "Jamkkaebi/Minigame Difficulty Table")]
    public class MinigameDifficultyTable : ScriptableObject
    {
        [SerializeField] private MinigameDifficultyEntry[] _entries;

        private Dictionary<MinigameDifficulty, MinigameBoardConfigData> _boardByDifficulty;

        /// <summary>
        /// 난이도에 해당하는 보드 설정을 돌려준다.
        /// </summary>
        /// <param name="difficulty">찾을 난이도.</param>
        /// <returns>그 난이도의 보드 설정.</returns>
        /// <exception cref="InvalidOperationException">
        /// 목록이 비었거나, 빈 슬롯이나 중복 난이도가 있거나, 해당 난이도가 등록되지 않았을 때.
        /// </exception>
        public MinigameBoardConfig GetConfig(MinigameDifficulty difficulty)
        {
            _boardByDifficulty ??= BuildLookup();

            if (!_boardByDifficulty.TryGetValue(difficulty, out MinigameBoardConfigData board))
            {
                throw new InvalidOperationException(
                    $"[MinigameDifficultyTable] '{name}'에 난이도 {difficulty}의 보드 설정이 등록되어 있지 않습니다.");
            }

            return board.ToConfig();
        }

        // Inspector에서 값을 바꾸면 다음 조회 때 룩업을 다시 만든다.
        private void OnValidate() => _boardByDifficulty = null;

        private Dictionary<MinigameDifficulty, MinigameBoardConfigData> BuildLookup()
        {
            if (_entries == null || _entries.Length == 0)
            {
                throw new InvalidOperationException(
                    $"[MinigameDifficultyTable] '{name}'의 난이도 목록이 비어 있습니다. Inspector에서 항목을 추가하세요.");
            }

            Dictionary<MinigameDifficulty, MinigameBoardConfigData> lookup =
                new Dictionary<MinigameDifficulty, MinigameBoardConfigData>();

            for (int i = 0; i < _entries.Length; i++)
            {
                MinigameDifficultyEntry entry = _entries[i];

                if (entry == null || entry.Board == null)
                {
                    throw new InvalidOperationException(
                        $"[MinigameDifficultyTable] '{name}'의 Element {i} 항목이 비어 있습니다.");
                }

                if (lookup.ContainsKey(entry.Difficulty))
                {
                    throw new InvalidOperationException(
                        $"[MinigameDifficultyTable] '{name}'에 난이도 {entry.Difficulty}가 중복 등록되어 있습니다. (Element {i})");
                }

                lookup.Add(entry.Difficulty, entry.Board);
            }

            return lookup;
        }
    }
}
