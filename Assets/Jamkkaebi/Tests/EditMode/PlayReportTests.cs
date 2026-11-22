using System;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    /// <summary>미니게임 결과 보고(서버 PurificationResultRequest)와 시작 설정의 검사를 확인합니다.</summary>
    public class PlayReportTests
    {
        private static PlayReport Success(int taps = 5, int scouts = 1, int damage = 20, int bonus = 2) =>
            new PlayReport(PlayResult.Success, null, taps, scouts, damage, bonus);

        private static PlayReport Failure(FailReason reason = FailReason.DamageFull) =>
            new PlayReport(PlayResult.Failure, reason, 7, 0, 100, 0);

        // ---------- PlayReport 생성 ----------

        /// <summary>성공 보고는 실패 사유 없이 값을 그대로 담는지 검사합니다.</summary>
        [Test]
        public void Success_StoresValues()
        {
            PlayReport report = Success(5, 1, 20, 2);

            Assert.AreEqual(PlayResult.Success, report.Result);
            Assert.IsNull(report.FailReason);
            Assert.AreEqual(5, report.TapsUsed);
            Assert.AreEqual(1, report.ScoutsUsed);
            Assert.AreEqual(20, report.Damage);
            Assert.AreEqual(2, report.BonusJeongseong);
        }

        /// <summary>실패 보고는 실패 사유를 담는지 검사합니다.</summary>
        [TestCase(FailReason.DamageFull)]
        [TestCase(FailReason.TapsExhausted)]
        [TestCase(FailReason.GaveUp)]
        public void Failure_StoresReason(FailReason reason)
        {
            Assert.AreEqual(reason, Failure(reason).FailReason);
        }

        /// <summary>성공인데 실패 사유가 있으면 거부하는지 검사합니다(서버 validateCombination).</summary>
        [Test]
        public void Success_WithFailReason_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new PlayReport(PlayResult.Success, FailReason.GaveUp, 1, 0, 0, 0));
        }

        /// <summary>실패인데 실패 사유가 없으면 거부하는지 검사합니다(서버 validateCombination).</summary>
        [Test]
        public void Failure_WithoutFailReason_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new PlayReport(PlayResult.Failure, null, 1, 0, 0, 0));
        }

        /// <summary>정의되지 않은 결과·실패 사유를 거부하는지 검사합니다.</summary>
        [Test]
        public void UndefinedEnums_Throw()
        {
            Assert.Throws<ArgumentException>(() => new PlayReport((PlayResult)99, null, 0, 0, 0, 0));
            Assert.Throws<ArgumentException>(() => new PlayReport(PlayResult.Failure, (FailReason)99, 0, 0, 0, 0));
        }

        /// <summary>음수 횟수·정성, 0~100 밖의 손상도를 거부하는지 검사합니다.</summary>
        [Test]
        public void OutOfRangeNumbers_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Success(taps: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Success(scouts: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Success(bonus: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Success(damage: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Success(damage: PlayReport.MaxDamage + 1));
        }

        /// <summary>손상도 0과 100은 받아들이는지 검사합니다(경계값).</summary>
        [Test]
        public void DamageBoundaries_Accepted()
        {
            Assert.AreEqual(0, Success(damage: 0).Damage);
            Assert.AreEqual(PlayReport.MaxDamage, Success(damage: PlayReport.MaxDamage).Damage);
        }

        // ---------- SameAs ----------

        /// <summary>모든 값이 같으면 같은 보고로 보는지 검사합니다.</summary>
        [Test]
        public void SameAs_AllEqual_True()
        {
            Assert.IsTrue(Success().SameAs(Success()));
            Assert.IsTrue(Failure(FailReason.GaveUp).SameAs(Failure(FailReason.GaveUp)));
        }

        /// <summary>값이 하나라도 다르면 다른 보고로 보는지 검사합니다(결과만 같아도 다름).</summary>
        [Test]
        public void SameAs_AnyDifference_False()
        {
            PlayReport baseline = Success(5, 1, 20, 2);

            Assert.IsFalse(baseline.SameAs(Success(6, 1, 20, 2)));
            Assert.IsFalse(baseline.SameAs(Success(5, 2, 20, 2)));
            Assert.IsFalse(baseline.SameAs(Success(5, 1, 21, 2)));
            Assert.IsFalse(baseline.SameAs(Success(5, 1, 20, 3)));
            Assert.IsFalse(Failure(FailReason.DamageFull).SameAs(Failure(FailReason.TapsExhausted)));
            Assert.IsFalse(baseline.SameAs(Failure()));
        }

        /// <summary>null과 비교하면 예외 없이 false인지 검사합니다.</summary>
        [Test]
        public void SameAs_Null_False()
        {
            Assert.IsFalse(Success().SameAs(null));
        }

        // ---------- PlaySetup ----------

        /// <summary>시작 설정이 값을 그대로 담고, 음수 시드도 받아들이는지 검사합니다(서버 시드는 Java long).</summary>
        [Test]
        public void PlaySetup_StoresValues()
        {
            PlaySetup setup = new PlaySetup(7, Phase.Recall, MinigameDifficulty.Hard, SealMedium.Fog, -1234567890123L);

            Assert.AreEqual(7, setup.RelicId);
            Assert.AreEqual(Phase.Recall, setup.Phase);
            Assert.AreEqual(MinigameDifficulty.Hard, setup.Difficulty);
            Assert.AreEqual(SealMedium.Fog, setup.SealMedium);
            Assert.AreEqual(-1234567890123L, setup.Seed);
        }

        /// <summary>정의되지 않은 페이즈·난이도·봉인 매개체를 거부하는지 검사합니다. 난이도 기본값 0도 정의되지 않은 값입니다.</summary>
        [Test]
        public void PlaySetup_UndefinedEnums_Throw()
        {
            Assert.Throws<ArgumentException>(() => new PlaySetup(1, (Phase)99, MinigameDifficulty.Normal, SealMedium.Rust, 1));
            Assert.Throws<ArgumentException>(() => new PlaySetup(1, Phase.Unseal, default(MinigameDifficulty), SealMedium.Rust, 1));
            Assert.Throws<ArgumentException>(() => new PlaySetup(1, Phase.Unseal, MinigameDifficulty.Normal, (SealMedium)99, 1));
        }

        /// <summary>relicId가 1보다 작으면 거부하고, 1은 받아들이는지 검사합니다(경계값).</summary>
        [Test]
        public void PlaySetup_RelicIdBelowOne_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PlaySetup(0, Phase.Unseal, MinigameDifficulty.Normal, SealMedium.Rust, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PlaySetup(-1, Phase.Unseal, MinigameDifficulty.Normal, SealMedium.Rust, 1));
            Assert.AreEqual(1, new PlaySetup(1, Phase.Unseal, MinigameDifficulty.Normal, SealMedium.Rust, 1).RelicId);
        }
    }
}
