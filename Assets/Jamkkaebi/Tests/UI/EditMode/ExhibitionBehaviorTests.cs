using Jamkkaebi.Scripts.UI.Exhibition;
using Jamkkaebi.Scripts.UI.Friends;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Jamkkaebi.Tests.UI
{
    public sealed class ExhibitionBehaviorTests
    {
        [Test]
        public void HomeAndVisitPoliciesKeepTheirSeparatePermissions()
        {
            var home = AssetDatabase.LoadAssetAtPath<ExhibitionBehaviorPolicy>("Assets/Jamkkaebi/Configuration/HomeExhibitionBehavior.asset");
            var visit = AssetDatabase.LoadAssetAtPath<ExhibitionBehaviorPolicy>("Assets/Jamkkaebi/Configuration/VisitExhibitionBehavior.asset");
            Assert.IsNotNull(home); Assert.IsNotNull(visit);
            Assert.IsTrue(home.AllowObjectSelection && home.AllowLayoutEditing && home.PlayIntroduction && home.EnableTabParallax);
            Assert.IsTrue(visit.AllowObjectSelection && visit.EnableAmbientMovement && visit.LockNavigationDuringVisit);
            Assert.IsFalse(home.EnableSelectionFocus || visit.EnableSelectionFocus);
            Assert.IsFalse(visit.AllowLayoutEditing || visit.PlayIntroduction || visit.EnableTabParallax);
            Assert.IsFalse(new ExhibitionVisitSession().CanEdit);
        }
    }
}
