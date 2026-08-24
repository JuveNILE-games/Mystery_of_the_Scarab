using Game.Net.Abstractions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class PlayerNetworkStateInterpolatorTests
    {
        private static PlayerNetworkState StateAt(float x)
        {
            return new PlayerNetworkState
            {
                Position = new Vector3(x, 0f, 0f),
                Rotation = Quaternion.identity,
            };
        }

        [Test]
        public void RenderTimeAtPrevSample_ReturnsPrevPosition()
        {
            var prev = StateAt(0f);
            var latest = StateAt(10f);

            var result = PlayerNetworkStateInterpolator.Interpolate(0.0, prev, 1.0, latest, renderTime: 0.0);

            Assert.AreEqual(0f, result.Position.x, 1e-5f);
        }

        [Test]
        public void RenderTimeAtLatestSample_ReturnsLatestPosition()
        {
            var prev = StateAt(0f);
            var latest = StateAt(10f);

            var result = PlayerNetworkStateInterpolator.Interpolate(0.0, prev, 1.0, latest, renderTime: 1.0);

            Assert.AreEqual(10f, result.Position.x, 1e-5f);
        }

        [Test]
        public void RenderTimeAtMidpoint_ReturnsBlendedPosition()
        {
            var prev = StateAt(0f);
            var latest = StateAt(10f);

            var result = PlayerNetworkStateInterpolator.Interpolate(0.0, prev, 1.0, latest, renderTime: 0.5);

            Assert.AreEqual(5f, result.Position.x, 1e-4f);
        }

        [Test]
        public void RenderTimeBeforePrevSample_ClampsToPrev_NoExtrapolation()
        {
            var prev = StateAt(0f);
            var latest = StateAt(10f);

            // Simulates the moment right after a fresh sample lands but the fixed interpolation
            // delay hasn't caught up to it yet.
            var result = PlayerNetworkStateInterpolator.Interpolate(0.0, prev, 1.0, latest, renderTime: -0.5);

            Assert.AreEqual(0f, result.Position.x, 1e-5f);
        }

        [Test]
        public void RenderTimeAfterLatestSample_ClampsToLatest_NoOvershoot()
        {
            var prev = StateAt(0f);
            var latest = StateAt(10f);

            // Simulates the freeze-then-stall scenario this fix targets: no new sample has arrived
            // for a while, so renderTime runs past the latest known sample. Must hold there, not
            // extrapolate/overshoot past it.
            var result = PlayerNetworkStateInterpolator.Interpolate(0.0, prev, 1.0, latest, renderTime: 5.0);

            Assert.AreEqual(10f, result.Position.x, 1e-5f);
        }

        [Test]
        public void DuplicateOrFirstSample_ZeroSpan_ReturnsLatestWithoutDivideByZero()
        {
            var prev = StateAt(3f);
            var latest = StateAt(3f);

            var result = PlayerNetworkStateInterpolator.Interpolate(1.0, prev, 1.0, latest, renderTime: 1.0);

            Assert.AreEqual(3f, result.Position.x, 1e-5f);
            Assert.IsFalse(float.IsNaN(result.Position.x));
        }

        [Test]
        public void DiscreteFields_AlwaysComeFromLatestSample_NeverBlended()
        {
            var prev = StateAt(0f);
            prev.JumpPressed = false;
            prev.IsGrounded = true;

            var latest = StateAt(10f);
            latest.JumpPressed = true;
            latest.IsGrounded = false;

            var result = PlayerNetworkStateInterpolator.Interpolate(0.0, prev, 1.0, latest, renderTime: 0.5);

            Assert.IsTrue(result.JumpPressed, "Boolean/discrete fields should come from the latest sample, not be blended.");
            Assert.IsFalse(result.IsGrounded);
        }
    }
}
