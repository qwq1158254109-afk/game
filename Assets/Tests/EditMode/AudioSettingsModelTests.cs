using NUnit.Framework;
using StudentGrowth;

namespace StudentGrowth.Tests
{
    public class AudioSettingsModelTests
    {
        [Test]
        public void MusicAndSfxVolumesAreIndependent()
        {
            AudioSettingsModel model = new AudioSettingsModel();

            model.SetVolume(GameAudioChannel.Music, 0.25f);

            Assert.AreEqual(1f, model.MasterVolume);
            Assert.AreEqual(0.25f, model.MusicVolume);
            Assert.AreEqual(1f, model.SfxVolume);
        }

        [Test]
        public void VolumeValuesAreClampedToNormalizedRange()
        {
            AudioSettingsModel model = new AudioSettingsModel();

            model.SetVolume(GameAudioChannel.Master, 2f);
            model.SetVolume(GameAudioChannel.Music, -1f);
            model.SetVolume(GameAudioChannel.Sfx, 2f);

            Assert.AreEqual(1f, model.MasterVolume);
            Assert.AreEqual(0f, model.MusicVolume);
            Assert.AreEqual(1f, model.SfxVolume);
        }

        [Test]
        public void NonfiniteAndInvalidChannelsCannotChangeModel()
        {
            AudioSettingsModel model = new AudioSettingsModel();
            Assert.IsFalse(model.SetVolume(GameAudioChannel.Master, float.NaN));
            Assert.IsFalse(model.SetVolume(GameAudioChannel.Music, float.PositiveInfinity));
            Assert.IsFalse(model.SetVolume((GameAudioChannel)999, 0.2f));
            Assert.AreEqual(1f, model.MasterVolume); Assert.AreEqual(1f, model.MusicVolume); Assert.AreEqual(1f, model.SfxVolume);
        }
    }
}
