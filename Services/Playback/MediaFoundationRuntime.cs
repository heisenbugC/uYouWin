using System;
using System.Runtime.InteropServices;

namespace uYouWin.Services.Playback
{
    public sealed class MediaFoundationRuntime : IDisposable
    {
        private const uint MF_VERSION = 0x00020000;
        private const uint MFSTARTUP_LITE = 0x00000001;

        private bool _started;

        public void Start()
        {
            if (_started)
                return;

            var hr = NativeMethods.MFStartup(MF_VERSION, MFSTARTUP_LITE);
            if (hr != NativeMethods.S_OK)
            {
                throw new COMException("MFStartup failed.", hr);
            }

            _started = true;
        }

        public void Dispose()
        {
            if (!_started)
                return;

            var hr = NativeMethods.MFShutdown();
            if (hr != NativeMethods.S_OK)
            {
                throw new COMException("MFShutdown failed.", hr);
            }

            _started = false;
        }

        private static class NativeMethods
        {
            public const int S_OK = 0;

            [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = true)]
            public static extern int MFStartup(uint version, uint dwFlags);

            [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = true)]
            public static extern int MFShutdown();
        }
    }
}
