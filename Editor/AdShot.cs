using System;

namespace Geuneda.AdCapture
{
    [Serializable]
    public sealed class AdShot
    {
        public string output;
        public int width;
        public int height;
        public int fps;
        public bool mute;
        public string hand;
        public float[] handTip;
        public float handSize;
        public AdStep[] steps;
    }

    [Serializable]
    public sealed class AdStep
    {
        public string op;
        public float seconds;
        public float scale;
        public float amplitude;
        public float[] focus;
        public string target;
        public string[] targets;
        public string name;
        public string[] args;
    }
}
