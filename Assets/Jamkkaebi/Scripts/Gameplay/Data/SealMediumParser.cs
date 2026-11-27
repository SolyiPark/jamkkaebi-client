using System;

namespace Jamkkaebi.Scripts.Gameplay.Data
{
    /// <summary>
    /// 서버의 봉인 매개체 문자열(RUST, FOG, SOOT, ROOT)을 <see cref="SealMedium"/>으로 바꾼다.
    /// </summary>
    public static class SealMediumParser
    {
        /// <summary>
        /// 서버 문자열을 봉인 매개체로 바꾼다. 서버 표기(대문자)와 정확히 같을 때만 받는다.
        /// </summary>
        /// <remarks>
        /// <c>Enum.TryParse</c>를 쓰지 않는 이유: "2" 같은 숫자 문자열도 성공으로 처리해 엉뚱한 값을 받아들이기 때문이다.
        /// </remarks>
        /// <param name="serverValue">서버 응답의 sealMedium 값.</param>
        /// <returns>대응하는 봉인 매개체.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="serverValue"/>가 null일 때.</exception>
        /// <exception cref="ArgumentException">알 수 없는 값일 때.</exception>
        public static SealMedium Parse(string serverValue)
        {
            if (serverValue == null)
            {
                throw new ArgumentNullException(nameof(serverValue));
            }

            return serverValue switch
            {
                "RUST" => SealMedium.Rust,
                "FOG" => SealMedium.Fog,
                "SOOT" => SealMedium.Soot,
                "ROOT" => SealMedium.Root,
                _ => throw new ArgumentException($"알 수 없는 봉인 매개체입니다. (입력값: {serverValue})", nameof(serverValue))
            };
        }
    }
}
