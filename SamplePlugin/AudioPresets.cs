using System;

namespace Soundy
{
    public enum AudioPreset
    {
        Neutral,
        ClubBass,
        EDM,
        Rock,
        Pop,
        OrchestralScore,
        VocalBoost,
        ChillLofi
    }

    public static class AudioPresetData
    {
        public static readonly string[] Names =
        {
            "Neutral",
            "Club / Bass",
            "EDM",
            "Rock",
            "Pop",
            "Orchestral / Score",
            "Vocal Boost",
            "Chill / Lo-Fi"
        };

        private static readonly float[][] Gains =
        {
            new float[] { 0f, 0f, 0f, 0f, 0f },
            new float[] { 4f, -1.5f, 0f, 0.5f, 1f },
            new float[] { 3f, -1f, 0f, 1.5f, 1.5f },
            new float[] { 2.5f, -1f, 0f, 2f, 2f },
            new float[] { 2f, -1f, 1f, 2f, 2f },
            new float[] { -1f, 0f, 0.5f, 1.5f, 2f },
            new float[] { -1f, -1f, 3f, 2f, 1f },
            new float[] { 0f, 2f, 0f, -1f, -2f }
        };

        public static float[] GetGains(AudioPreset preset) => Gains[(int)preset];
    }
}
