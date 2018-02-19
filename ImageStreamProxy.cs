using System;

namespace Starship.Injection {
    public class ImageStreamProxy : MarshalByRefObject {

        public ImageCaptureSettings GetSettings() {
            return Settings;
        }

        public void GrantProxy(ClientCallbackProxy proxy) {
            if (ClientCallbackProxyGranted != null) {
                ClientCallbackProxyGranted(proxy);
            }
        }

        public void SaveImage(byte[] data) {
            if (ImageDataReceived != null) {
                ImageDataReceived(data);
            }
        }

        public void Debug(string message) {
            Console.WriteLine(message);

            if (DebugReceived != null) {
                DebugReceived(message);
            }
        }

        public static ImageCaptureSettings Settings = new ImageCaptureSettings();

        public static event Action<string> DebugReceived;

        public static event Action<byte[]> ImageDataReceived;

        public static event Action<ClientCallbackProxy> ClientCallbackProxyGranted;
    }
}