using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace uYouWin.Services.Playback
{
    internal sealed class MediaFoundationPlayer : VideoPlayerInterface
    {
        private readonly IMFMediaEngine _engine;
        private readonly object _sync = new object();
        private bool _disposed;

        public MediaFoundationPlayer()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                throw new PlatformNotSupportedException(
                    "Windows Media Foundation requires Windows 8 or later.");
            }

            NativeMethods.CoInitializeEx(IntPtr.Zero, 
                NativeMethods.COINIT_APARTMENTTHREADED);

            var startupHr = NativeMethods.MFStartup(NativeMethods.MF_API_VERSION, 0);
            if (startupHr != NativeMethods.S_OK)
            {
                throw new COMException("MFStartup failed.", startupHr);
            }

            _engine = CreateEngine();
        }

        public Task OpenAsync(Uri uri, CancellationToken cancellationToken)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            if (_disposed)
                throw new ObjectDisposedException(nameof(MediaFoundationPlayer));

            lock (_sync)
            {
                _engine.SetSource(uri.AbsoluteUri);
                _engine.Load();
            }

            return Task.CompletedTask;
        }

        public Task PlayAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MediaFoundationPlayer));

            lock (_sync)
            {
                _engine.Play();
            }

            return Task.CompletedTask;
        }

        public void Pause()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MediaFoundationPlayer));

            lock (_sync)
            {
                _engine.Pause();
            }
        }

        public void Stop()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MediaFoundationPlayer));

            lock (_sync)
            {
                _engine.Pause();
                _engine.SetCurrentTime(0d);
            }
        }

        public void Seek(double position)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MediaFoundationPlayer));

            lock (_sync)
            {
                _engine.SetCurrentTime(position);
            }
        }

        public double Position
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MediaFoundationPlayer));

                lock (_sync)
                {
                    return _engine.GetCurrentTime();
                }
            }
        }

        public double Duration
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MediaFoundationPlayer));

                lock (_sync)
                {
                    return _engine.GetDuration();
                }
            }
        }

        public bool IsPlaying
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MediaFoundationPlayer));

                lock (_sync)
                {
                    return !_engine.IsPaused();
                }
            }
        }

        private static IMFMediaEngine CreateEngine()
        {
            var factory = CreateFactory();
            var flags = (uint)MFMediaEngineCreateFlags.MF_MEDIA_ENGINE_REAL_TIME_MODE;
            var hr = factory.CreateInstance(flags, IntPtr.Zero, out var engine);
            if (hr != NativeMethods.S_OK)
            {
                throw new COMException("IMFMediaEngineClassFactory::CreateInstance failed.", hr);
            }

            return engine;
        }

        private static IMFMediaEngineClassFactory CreateFactory()
        {
            // These GUID values must match the exact COM metadata in mfmediaengine.h.
            // Keeping them explicit makes the native surface easy to replace with the
            // Windows SDK's definitive values when the final binding is wired up.
            var clsid = new Guid("B22C3339-87F3-4059-A0C5-037AA9707EAF");
            var iid = typeof(IMFMediaEngineClassFactory).GUID;

            var hr = NativeMethods.CoCreateInstance(
                ref clsid,
                IntPtr.Zero,
                NativeMethods.CLSCTX_INPROC_SERVER,
                ref iid,
                out var factoryPtr);

            if (hr != NativeMethods.S_OK)
            {
                throw new COMException("CoCreateInstance for MFMediaEngineClassFactory failed.", hr);
            }

            var factory = (IMFMediaEngineClassFactory)Marshal.GetObjectForIUnknown(factoryPtr);
            return factory;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            lock (_sync)
            {
                _engine?.Shutdown();
            }

            NativeMethods.MFShutdown();
            NativeMethods.CoUninitialize();
        }
    }

    [ComImport]
    [Guid("B22C3339-87F3-4059-A0C5-037AA9707EAF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFMediaEngineClassFactory
    {
        [PreserveSig]
        int CreateInstance(
            uint dwFlags,
            IntPtr pAttr,
            out IMFMediaEngine ppPlayer);
    }

    [ComImport]
    [Guid("B22C3339-87F3-4059-A0C5-037AA9707EAF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFMediaEngine
    {
        void SetSource([MarshalAs(UnmanagedType.BStr)] string pUrl);
        void Load();
        void Play();
        void Pause();
        void SetCurrentTime(double seekTime);
        double GetCurrentTime();
        double GetDuration();
        bool IsPaused();
        void Shutdown();
        void SetVolume(float volume);
        float GetVolume();
    }

    internal static class NativeMethods
    {
        public const int S_OK = 0;
        public const uint MF_API_VERSION = 0x00020000;
        public const int COINIT_APARTMENTTHREADED = 0x2;
        public const uint CLSCTX_INPROC_SERVER = 0x1;

        [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = true)]
        public static extern int MFStartup(uint version, uint dwFlags);

        [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = true)]
        public static extern int MFShutdown();

        [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
        public static extern int CoInitializeEx(IntPtr pvReserved, int dwCoInit);

        [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
        public static extern void CoUninitialize();

        [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
        public static extern int CoCreateInstance(
            ref Guid rclsid,
            IntPtr pUnkOuter,
            uint dwClsContext,
            ref Guid riid,
            out IntPtr ppv);
    }

    internal enum MFMediaEngineCreateFlags : uint
    {
        MF_MEDIA_ENGINE_AUDIOONLY = 0x1,
        MF_MEDIA_ENGINE_WAITFORSTABLE_STATE = 0x2,
        MF_MEDIA_ENGINE_FORCEMUTE = 0x4,
        MF_MEDIA_ENGINE_REAL_TIME_MODE = 0x8,
        MF_MEDIA_ENGINE_DISABLE_LOCAL_PLUGINS = 0x10,
        MF_MEDIA_ENGINE_CREATEFLAGS_MASK = 0x1F
    }
}
