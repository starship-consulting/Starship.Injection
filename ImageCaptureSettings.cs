using System;

namespace Starship.Injection {

    [Serializable]
    public class ImageCaptureSettings {
        public int Width { get; set; }

        public int Height { get; set; }

        public int Interval { get; set; }
    }
}