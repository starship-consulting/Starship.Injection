using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Serialization.Formatters;
using System.Threading;
using System.Threading.Tasks;
using EasyHook;
using Starship.Core.Extensions;
using Starship.Core.GPU.OpenGL;
using Starship.Win32.Extensions;

namespace Starship.Injection {

    public class InjectionEntryPoint : IEntryPoint {

        public InjectionEntryPoint(RemoteHooking.IContext context, string channelName) {
            LastImageUpdate = DateTime.Now;

            Server = RemoteHooking.IpcConnectClient<ImageStreamProxy>(channelName);
            Settings = Server.GetSettings();

            CreateCallbackChannel(channelName);
        }

        private void CreateCallbackChannel(string channelName) {
            var properties = new Hashtable();
            properties["name"] = channelName;
            properties["portName"] = channelName + Guid.NewGuid().ToString("N");

            var provider = new BinaryServerFormatterSinkProvider();
            provider.TypeFilterLevel = TypeFilterLevel.Full;

            ClientServerChannel = new IpcServerChannel(properties, provider);
            ChannelServices.RegisterChannel(ClientServerChannel, false);
        }

        private void OnSessionClosed() {
            IsClosed = true;
            StopCheckAlive = 1;
            ResetEvent.Set();
        }

        private void StartCheckHostIsAliveThread() {
            CheckAlive = new Task(() => {
                try {
                    while (Interlocked.Read(ref StopCheckAlive) == 0) {
                        Thread.Sleep(1000);
                        Settings = Server.GetSettings();
                    }
                }
                catch {
                    ResetEvent.Set();
                }
            });

            CheckAlive.Start();
        }

        private void StopCheckHostIsAliveThread() {
            Interlocked.Increment(ref StopCheckAlive);
        }

        public void Run(RemoteHooking.IContext context, string channelName) {
            ClientCallbackProxy.SessionClosed += OnSessionClosed;

            ResetEvent = new ManualResetEvent(false);
            ResetEvent.Reset();

            Proxy = new ClientCallbackProxy();
            Server.GrantProxy(Proxy);

            try {
                BeginHook();

                RemoteHooking.WakeUpProcess();

                StartCheckHostIsAliveThread();
                ResetEvent.WaitOne();
                StopCheckHostIsAliveThread();
            }
            catch (Exception ex) {
                Server.Debug(ex.ToString());
            }
            finally {
                ChannelServices.UnregisterChannel(ClientServerChannel);
                EndHook();
                Thread.Sleep(250);
            }
        }

        private void BeginHook() {
            Server.Debug("Begin hook");
            Hook = LocalHook.Create(LocalHook.GetProcAddress("opengl32.dll", "wglSwapBuffers"), new SwapBuffersDelegate(OnSwapBuffers), this);
            Hook.ThreadACL.SetExclusiveACL(new[] {0});
            Server.Debug("End hook");
        }

        private void EndHook() {
            if (Hook != null) {
                LocalHook.Release();
                Hook.Dispose();
                Hook = null;
                Proxy = null;
            }
        }
        
        public IntPtr OnSwapBuffers(IntPtr hdc) {
            if (IsClosed || Settings.Width <= 0 || Settings.Height <= 0 || Settings.Interval <= 0) {
                return wglSwapBuffers(hdc);
            }

            try {
                var time = TimeSpan.FromMilliseconds(Settings.Interval);

                if (LastImageUpdate.HasElapsed(time)) {
                    LastImageUpdate = DateTime.Now;

                    using (var bmp = new Bitmap(Settings.Width, Settings.Height)) {
                        var data = bmp.LockBits(new Rectangle(0, 0, Settings.Width, Settings.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

                        glReadBuffer(GLBuffers.FRONT_AND_BACK);
                        glReadPixels(0, 0, Settings.Width, Settings.Height, OpenGL.PixelFormat.Bgr, OpenGL.PixelType.UnsignedByte, data.Scan0);
                        //glFinish();
                        bmp.UnlockBits(data);
                        bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
                        var bytes = bmp.ToBytes(ImageFormat.Bmp);
                        Server.SaveImage(bytes);
                    }
                }
            }
            catch (Exception) {
            }

            return wglSwapBuffers(hdc);
        }
        
        private ImageStreamProxy Server { get; set; }

        private ClientCallbackProxy Proxy { get; set; }

        private IpcServerChannel ClientServerChannel { get; set; }

        private bool IsClosed { get; set; }

        private ManualResetEvent ResetEvent { get; set; }

        private Task CheckAlive { get; set; }

        private long StopCheckAlive = 0;

        private LocalHook Hook { get; set; }

        private ImageCaptureSettings Settings { get; set; }

        private DateTime LastImageUpdate { get; set; }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate IntPtr SwapBuffersDelegate(IntPtr hdc);

        [DllImport("opengl32.dll", CharSet = CharSet.Unicode, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr wglSwapBuffers(IntPtr hdc);

        [DllImport("opengl32.dll", CharSet = CharSet.Unicode, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern void glReadPixels(int x, int y, int width, int height, OpenGL.PixelFormat format, OpenGL.PixelType type, IntPtr data);

        [DllImport("opengl32.dll")]
        private static extern void glReadBuffer(GLBuffers buffer);
    }
}