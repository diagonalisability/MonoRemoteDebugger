using System.Diagnostics;
using System.IO;

namespace MonoRemoteDebugger.SharedLib.Server
{
    internal class MonoDesktopProcess : MonoProcess
    {
        private readonly string _targetExe;

        public MonoDesktopProcess(string targetExe)
        {
            _targetExe = targetExe;
        }

        internal override Process Start(string workingDirectory)
        {
            string exeWithoutExtension = _targetExe.EndsWith(".exe") ? _targetExe.Substring(0, _targetExe.Length - 4) : _targetExe;
            bool isOSX = System.Environment.OSVersion.Platform == System.PlatformID.MacOSX;
            // can use System.Environment.Is64BitOperatingSystem here to check whether we should use a 32-bit binary
            string kickstartBinPath = Path.Combine(workingDirectory, exeWithoutExtension + ".bin." + (isOSX ? "osx" : "x86_64"));
            MakeExecutable(kickstartBinPath);
            ProcessStartInfo procInfo = GetProcessStartInfo(workingDirectory, kickstartBinPath);
            procInfo.Arguments = Arguments;
            procInfo.UseShellExecute = false;
            procInfo.EnvironmentVariables["MONO_BUNDLED_OPTIONS"] = GetMonoDebuggerArgs();
            _proc = Process.Start(procInfo);
            RaiseProcessStarted();
            return _proc;
        }

        // https://stackoverflow.com/a/47918132
        private static void MakeExecutable(string kickstartBinPath)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "chmod",
                    Arguments = "+x " + kickstartBinPath
                }
            };
            process.Start();
            process.WaitForExit();
        }
    }
}
