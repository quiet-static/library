using NUnit.Framework;
using QuietStatic.Toolkit.Timekeeping;
using UnityEngine;
namespace QuietStatic.Tests.EditMode
{
    public class GameClockTests
    {
        [Test] public void ClockSupportsDayRolloverRewindAndTwelveHourText()
        {
            var go=new GameObject("Clock test");
            try {
                var clock=go.AddComponent<GameClockManager>(); clock.SetTotalMinutes(1439); clock.AdvanceMinutes(1);
                Assert.AreEqual(1,clock.Day); Assert.AreEqual("12:00 AM",clock.DisplayTime);
                clock.SetTime(12,0); Assert.AreEqual("12:00 PM",clock.DisplayTime);
                clock.AdvanceMinutes(-1); Assert.AreEqual("11:59 AM",clock.DisplayTime);
                clock.SetTotalMinutes(-5); Assert.AreEqual(0,clock.TotalMinutes);
                clock.SetTotalMinutes(double.NaN); Assert.AreEqual(0,clock.TotalMinutes);
                clock.SecondsPerGameMinute=0; Assert.Greater(clock.SecondsPerGameMinute,0);
            } finally { Object.DestroyImmediate(go); }
        }
    }
}
