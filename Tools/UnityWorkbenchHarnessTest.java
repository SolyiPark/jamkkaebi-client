package com.example.jamkkaebi.workbench;

import com.example.jamkkaebi.auth.domain.AuthProvider;
import com.example.jamkkaebi.auth.service.AuthService;
import com.example.jamkkaebi.auth.service.HandoffService;
import com.example.jamkkaebi.global.security.JwtProvider;
import com.example.jamkkaebi.relic.domain.UserRelic;
import com.example.jamkkaebi.relic.repository.UserRelicRepository;
import com.example.jamkkaebi.user.domain.User;
import com.example.jamkkaebi.user.service.UserRegistrationService;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.web.server.LocalServerPort;
import tools.jackson.databind.ObjectMapper;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.security.MessageDigest;
import java.time.LocalDateTime;
import java.util.Base64;
import java.util.LinkedHashMap;

/**
 * Unity 작업대 계약 검증용 로컬 서버. 이 저장소에 커밋하지 않는다.
 * Unity 클라이언트의 Tools/serve-workbench-contract.sh가 잠시 복사해 실행하고 끝나면 지운다.
 *
 * <p>로컬 H2에서 실제 HTTP 서버를 열고 시계는 실제 시계를 쓴다(정화 최소 플레이 시간 판정 때문).
 * 사용자 셋을 만든다. box·spirit은 Unity EditMode HTTP 테스트가 상태를 바꾸는 사용자, play는 에디터 Play 확인용이다.
 * 상자 테스트 사용자는 유물이 하나도 없어야 첫 뽑기가 항상 새 유물이다(뽑기는 무작위라 가진 유물이 나오면 중복).
 */
@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT,
        properties = "app.purification.min-play-duration=1s")
class UnityWorkbenchHarnessTest {
    @LocalServerPort int port;
    @Autowired UserRegistrationService registration;
    @Autowired JwtProvider jwt;
    @Autowired HandoffService handoff;
    @Autowired AuthService auth;
    @Autowired UserRelicRepository relics;
    @Autowired ObjectMapper mapper;

    @Test
    void serveUnity() throws Exception {
        LocalDateTime now = LocalDateTime.now();

        // EditMode HTTP 테스트용: 유물이 없는 상자 사용자, 선명한 정령 하나(5번)만 가진 정령 사용자
        User box = registration.findOrRegister(AuthProvider.GOOGLE, "unity-workbench-box", "상자검증").user();
        User spirit = registration.findOrRegister(AuthProvider.GOOGLE, "unity-workbench-spirit", "정령검증").user();
        completed(spirit, 5, now.minusDays(1));

        // Play 확인용: 최초 정화 중 유물 하나(3번)와 선명·흐려짐·재봉인 정령(2·8·9번)
        User play = registration.findOrRegister(AuthProvider.GOOGLE, "unity-workbench-play", "작업대플레이").user();
        relics.save(UserRelic.acquired(play.getId(), 3, now.minusHours(1)));
        completed(play, 2, now.minusDays(3));
        completed(play, 8, now.minusDays(10));
        completed(play, 9, now.minusDays(20));

        // Play는 길어질 수 있어 refresh 토큰이 있는 실제 로그인 토큰을 쓴다.
        String verifier = "unity-local-workbench-verifier-at-least-32-characters";
        String hash = Base64.getEncoder().encodeToString(
                MessageDigest.getInstance("SHA-256").digest(verifier.getBytes(StandardCharsets.UTF_8)));
        var playTokens = auth.exchange(handoff.issue(play.getId(), hash, false), verifier, "unity-play");

        var config = new LinkedHashMap<String, Object>();
        config.put("baseUrl", "http://127.0.0.1:" + port);
        config.put("boxAccessToken", jwt.createAccessToken(box.getFriendCode()));
        config.put("spiritAccessToken", jwt.createAccessToken(spirit.getFriendCode()));
        config.put("spiritRelicId", 5);
        config.put("playAccessToken", playTokens.accessToken());
        config.put("playRefreshToken", playTokens.refreshToken());
        Path session = Path.of("workbench-session.json");
        Files.writeString(session, mapper.writeValueAsString(config));

        long deadline = System.currentTimeMillis() + 2 * 60 * 60 * 1000L;
        while (!Files.exists(Path.of("workbench-stop")) && System.currentTimeMillis() < deadline) {
            Thread.sleep(500);
        }
        Files.deleteIfExists(session);
    }

    // 최초 정화 세 페이즈를 마친 정령. 마지막 정화 시각이 선명도를 정한다(7일 흐려짐, 14일 재봉인).
    private void completed(User user, int relicId, LocalDateTime purifiedAt) {
        UserRelic owned = UserRelic.acquired(user.getId(), relicId, purifiedAt.minusHours(1));
        owned.clearFirstPlay(purifiedAt);
        owned.clearFirstPlay(purifiedAt);
        owned.clearFirstPlay(purifiedAt);
        relics.save(owned);
    }
}
