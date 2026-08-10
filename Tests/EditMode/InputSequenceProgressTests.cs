using NUnit.Framework;
using QuietStatic.Toolkit.Minigames;

namespace QuietStatic.Tests.EditMode
{
    public sealed class InputSequenceProgressTests
    {
        [Test]
        public void CorrectInputs_AdvanceAndCompleteSequence()
        {
            var progress = new InputSequenceProgress();
            progress.Start();

            Assert.That(
                progress.Submit(true, 2, WrongInputResponse.ResetSequence),
                Is.EqualTo(InputSequenceResult.Correct));
            Assert.That(progress.CurrentIndex, Is.EqualTo(1));

            Assert.That(
                progress.Submit(true, 2, WrongInputResponse.ResetSequence),
                Is.EqualTo(InputSequenceResult.Completed));
            Assert.That(progress.IsActive, Is.False);
        }

        [Test]
        public void WrongInput_CanResetSequence()
        {
            var progress = new InputSequenceProgress();
            progress.Start();
            progress.Submit(true, 3, WrongInputResponse.ResetSequence);

            Assert.That(
                progress.Submit(false, 3, WrongInputResponse.ResetSequence),
                Is.EqualTo(InputSequenceResult.Reset));
            Assert.That(progress.CurrentIndex, Is.Zero);
            Assert.That(progress.IsActive, Is.True);
        }

        [Test]
        public void WrongInput_CanFailSequence()
        {
            var progress = new InputSequenceProgress();
            progress.Start();

            Assert.That(
                progress.Submit(false, 3, WrongInputResponse.FailMinigame),
                Is.EqualTo(InputSequenceResult.Failed));
            Assert.That(progress.IsActive, Is.False);
        }

        [Test]
        public void WrongInput_CanBeIgnored()
        {
            var progress = new InputSequenceProgress();
            progress.Start();
            progress.Submit(true, 3, WrongInputResponse.Ignore);

            Assert.That(
                progress.Submit(false, 3, WrongInputResponse.Ignore),
                Is.EqualTo(InputSequenceResult.Incorrect));
            Assert.That(progress.CurrentIndex, Is.EqualTo(1));
            Assert.That(progress.IsActive, Is.True);
        }
    }
}
