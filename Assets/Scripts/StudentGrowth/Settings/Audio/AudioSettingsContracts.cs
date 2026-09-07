using System;

namespace StudentGrowth
{
    public enum GameAudioChannel
    {
        Music = 0,
        Sfx = 1,
        Master = 2
    }

    public interface IAudioSettingsProvider
    {
        event Action<GameAudioChannel, float> VolumeChanged;

        float GetVolume(GameAudioChannel channel);
    }
}
