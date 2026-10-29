package com.example.jamkkaebi.friend;

import com.example.jamkkaebi.auth.domain.AuthProvider;
import com.example.jamkkaebi.auth.service.AuthService;
import com.example.jamkkaebi.auth.service.HandoffService;
import com.example.jamkkaebi.global.security.JwtProvider;
import com.example.jamkkaebi.global.security.JwtProperties;
import com.example.jamkkaebi.friend.domain.Friendship;
import com.example.jamkkaebi.friend.repository.FriendshipRepository;
import com.example.jamkkaebi.friend.service.ExhibitionSnapshotService;
import com.example.jamkkaebi.friend.spi.ExhibitionView;
import com.example.jamkkaebi.support.FakeExhibitionViewReader;
import com.example.jamkkaebi.support.MutableClock;
import com.example.jamkkaebi.friend.spi.ExhibitionViewReader;
import com.example.jamkkaebi.exhibition.service.ExhibitionViewProvider;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Primary;
import com.example.jamkkaebi.user.domain.UserActivityRecordedEvent;
import com.example.jamkkaebi.user.service.UserRegistrationService;
import com.example.jamkkaebi.relic.domain.Era;
import com.example.jamkkaebi.relic.domain.AwakeningStage;
import com.example.jamkkaebi.relic.domain.UserRelic;
import com.example.jamkkaebi.relic.domain.UserBuilding;
import com.example.jamkkaebi.relic.repository.UserRelicRepository;
import com.example.jamkkaebi.relic.repository.UserBuildingRepository;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.web.server.LocalServerPort;
import org.springframework.context.annotation.Import;
import tools.jackson.databind.ObjectMapper;
import java.nio.file.*;
import java.time.LocalDateTime;
import java.util.*;
import java.security.MessageDigest;

/** 로컬 H2에서 실제 HTTP 서버를 열어 Unity 계약 검증에만 사용한다. */
@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT, properties = "app.auth.handoff-ttl=10m")
@Import(UnityContractHarnessTest.HarnessConfig.class)
class UnityContractHarnessTest {
    static final Set<Long> mockOwners = java.util.concurrent.ConcurrentHashMap.newKeySet();
    @TestConfiguration
    static class HarnessConfig {
        @Bean @Primary MutableClock testClock() { return new MutableClock(LocalDateTime.of(2026,9,12,10,0)); }
        @Bean FakeExhibitionViewReader fakeView() { return new FakeExhibitionViewReader(); }
        @Bean @Primary ExhibitionViewReader contractView(FakeExhibitionViewReader fake, ExhibitionViewProvider real) {
            return userId -> mockOwners.contains(userId) ? fake.readPublicView(userId) : real.readPublicView(userId);
        }
    }
    @LocalServerPort int port;
    @Autowired UserRegistrationService registration;
    @Autowired FriendshipRepository friendships;
    @Autowired JwtProvider jwt;
    @Autowired HandoffService handoff;
    @Autowired AuthService auth;
    @Autowired FakeExhibitionViewReader view;
    @Autowired ExhibitionSnapshotService snapshots;
    @Autowired ObjectMapper mapper;
    @Autowired UserRelicRepository relics;
    @Autowired UserBuildingRepository buildings;
    @Test void serveUnity() throws Exception {
        var a = registration.findOrRegister(AuthProvider.GOOGLE, "unity-a", "Unity검증자").user();
        var b = registration.findOrRegister(AuthProvider.GOOGLE, "unity-b", "칠칸전시").user();
        var c = registration.findOrRegister(AuthProvider.GOOGLE, "unity-c", "스무칸전시").user();
        var d = registration.findOrRegister(AuthProvider.GOOGLE, "unity-d", "요청대상").user();
        var e = registration.findOrRegister(AuthProvider.GOOGLE, "unity-e", "새친구").user();
        var f = registration.findOrRegister(AuthProvider.GOOGLE, "unity-layout", "배치송신검증").user();
        var g = registration.findOrRegister(AuthProvider.GOOGLE, "unity-layout-visitor", "배치방문검증").user();
        friendships.save(Friendship.between(f.getId(), g.getId()));
        mockOwners.add(b.getId()); mockOwners.add(c.getId());
        for (int id : new int[] {2,8}) {
            var owned = UserRelic.acquired(f.getId(), id, LocalDateTime.now());
            owned.clearFirstPhase(LocalDateTime.now());
            owned.clearFirstPhase(LocalDateTime.now());
            owned.clearFirstPhase(LocalDateTime.now());
            if (id == 8) owned.placeAt(4,4);
            relics.save(owned);
        }
        buildings.save(UserBuilding.awarded(f.getId(), 1, LocalDateTime.now()));
        friendships.save(Friendship.between(a.getId(), b.getId()));
        friendships.save(Friendship.between(a.getId(), c.getId()));
        var spirit = new ExhibitionView.SpiritView(8, Era.JOSEON, "테스트", "유물", "별이", AwakeningStage.AWAKENED_PLUS, new ExhibitionView.Position(6,6));
        var building = new ExhibitionView.BuildingView(9, Era.JOSEON, "건물", 2, new ExhibitionView.Position(0,0));
        view.show(b.getId(), new ExhibitionView(new ExhibitionView.Grid(7,7), List.of(spirit), List.of(building)));
        view.show(c.getId(), new ExhibitionView(new ExhibitionView.Grid(20,20), List.of(spirit), List.of(building)));
        snapshots.captureOnActivity(new UserActivityRecordedEvent(b.getId(), LocalDateTime.now()));
        snapshots.captureOnActivity(new UserActivityRecordedEvent(c.getId(), LocalDateTime.now()));
        String verifier = "unity-local-contract-verifier-at-least-32-characters";
        String hash = Base64.getEncoder().encodeToString(MessageDigest.getInstance("SHA-256").digest(verifier.getBytes(java.nio.charset.StandardCharsets.UTF_8)));
        var tokens = auth.exchange(handoff.issue(a.getId(), hash, false), verifier, "contract-test");
        var expired = new JwtProvider(new JwtProperties("jamkkaebi-test-secret-key-please-change-me-32bytes", -300000L, 2592000000L));
        var config = new LinkedHashMap<String,Object>();
        config.put("baseUrl", "http://127.0.0.1:" + port);
        config.put("layoutAccessToken", jwt.createAccessToken(f.getFriendCode()));
        config.put("layoutCode", f.getFriendCode());
        config.put("layoutVisitorAccessToken", jwt.createAccessToken(g.getFriendCode()));
        config.put("accessToken", tokens.accessToken()); config.put("refreshToken", tokens.refreshToken());
        config.put("expiredAccessToken", expired.createAccessToken(a.getFriendCode()));
        config.put("myCode", a.getFriendCode()); config.put("friendCode", b.getFriendCode()); config.put("largeGridCode", c.getFriendCode());
        config.put("targetCode", d.getFriendCode()); config.put("otherToken", jwt.createAccessToken(d.getFriendCode()));
        config.put("extraCode", e.getFriendCode()); config.put("verifier", verifier);
        config.put("handoff", handoff.issue(e.getId(), hash, false));
        Files.writeString(Path.of("contract-session.json"), mapper.writeValueAsString(config));
        long deadline = System.currentTimeMillis() + 1800000;
        while (!Files.exists(Path.of("contract-stop")) && System.currentTimeMillis() < deadline) Thread.sleep(500);
        Files.deleteIfExists(Path.of("contract-session.json"));
    }
}
