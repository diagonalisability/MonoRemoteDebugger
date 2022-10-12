using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using MonoRemoteDebugger.SharedLib;
using MonoRemoteDebugger.Debugger;
using MonoRemoteDebugger.Debugger.VisualStudio;
using MonoRemoteDebugger.VSExtension.MonoClient;
using NLog;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using Task = System.Threading.Tasks.Task;
using Microsoft.MIDebugEngine;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace MonoRemoteDebugger.VSExtension
{
    internal class MonoVisualStudioExtension
    {
        private readonly DTE _dte;
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public MonoVisualStudioExtension(DTE dTE)
        {
            _dte = dTE;
        }

        internal void BuildSolution()
        {
            var sb = (SolutionBuild2) _dte.Solution.SolutionBuild;
            sb.Build(true);
        }

        private Project GetStartupProject()
        {
            var sb = (SolutionBuild2) _dte.Solution.SolutionBuild;
            string project = ((Array) sb.StartupProjects).Cast<string>().First();

            try
            {
                var projects = Projects(_dte.Solution);
                foreach (var p in projects)
                {
                    if (p.UniqueName == project)
                        return p;
                }
                //startupProject = _dte.Solution.Item(project);
            }
            catch (ArgumentException aex)
            {
                throw new ArgumentException($"The parameter '{project}' is incorrect.", aex);
            }

            throw new ArgumentException($"The parameter '{project}' is incorrect.");
        }

        public static IList<Project> Projects(Solution solution)
        {
            Projects projects = solution.Projects;
            List<Project> list = new List<Project>();
            var item = projects.GetEnumerator();
            while (item.MoveNext())
            {
                var project = item.Current as Project;
                if (project == null)
                {
                    continue;
                }

                if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder)
                {
                    list.AddRange(GetSolutionFolderProjects(project));
                }
                else
                {
                    list.Add(project);
                }
            }

            return list;
        }

        private static IEnumerable<Project> GetSolutionFolderProjects(Project solutionFolder)
        {
            List<Project> list = new List<Project>();
            for (var i = 1; i <= solutionFolder.ProjectItems.Count; i++)
            {
                var subProject = solutionFolder.ProjectItems.Item(i).SubProject;
                if (subProject == null)
                {
                    continue;
                }

                // If this is another solution folder, do a recursive call, otherwise add
                if (subProject.Kind == ProjectKinds.vsProjectKindSolutionFolder)
                {
                    list.AddRange(GetSolutionFolderProjects(subProject));
                }
                else
                {
                    list.Add(subProject);
                }
            }
            return list;
        }

        internal string GetAssemblyPath(Project vsProject)
        {
            string fullPath = vsProject.Properties.Item("FullPath").Value.ToString();
            string outputPath =
                vsProject.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value.ToString();
            string outputDir = Path.Combine(fullPath, outputPath);
            string outputFileName = vsProject.Properties.Item("OutputFileName").Value.ToString();
            string assemblyPath = Path.Combine(outputDir, outputFileName);
            return assemblyPath;
        }

        internal string GetArguments(Project vsProject)
        {
            try
            {
                //TODO: Need to support Common Project System https://github.com/Microsoft/VSProjectSystem/blob/master/doc/automation/finding_CPS_in_a_VS_project.md
                return vsProject.ConfigurationManager.ActiveConfiguration.Properties.Item("StartArguments").Value.ToString();
            }
            catch { }

            return null;
        }

        internal async Task AttachDebuggerAsync(string ipAddress, bool ShouldUploadBinariesToDebuggingServer, int timeout=10000)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Project startupProject = GetStartupProject();
            string path = GetAssemblyPath(startupProject);
            string arguments = GetArguments(startupProject);
            string targetExe = Path.GetFileName(path);
            string outputDirectory = Path.GetDirectoryName(path);
            string appHash = ComputeHash(path);

            bool isWeb = ((object[])startupProject.ExtenderNames).Any(x => x.ToString() == "WebApplication");
            ApplicationType appType = isWeb ? ApplicationType.Webapplication : ApplicationType.Desktopapplication;
            if (appType == ApplicationType.Webapplication)
                outputDirectory += @"\..\..\";

            var client = new DebugClient(appType, targetExe, arguments, outputDirectory, appHash);

            DebugSession session = null;
            if (ShouldUploadBinariesToDebuggingServer)
            {
                session = await client.ConnectToServerAsync(ipAddress);
                var debugSessionStarted = await session.RestartDebuggingAsync(timeout);
                if (!debugSessionStarted)
                {
                    await session.TransferFilesAsync();
                    await session.WaitForAnswerAsync(timeout);
                }
            }

            IntPtr pInfo = GetDebugInfo(ipAddress, targetExe, outputDirectory);
            var sp = new ServiceProvider((IServiceProvider) _dte);
            try
            {
                var dbg = (IVsDebugger) sp.GetService(typeof (SVsShellDebugger));
                int hr = dbg.LaunchDebugTargets(1, pInfo);
                Marshal.ThrowExceptionForHR(hr);

                if (ShouldUploadBinariesToDebuggingServer)
                    DebuggedProcess.Instance.AssociateDebugSession(session);
            }
            catch(Exception ex)
            {
                logger.Error(ex);
                string msg;
                var sh = (IVsUIShell) sp.GetService(typeof (SVsUIShell));
                sh.GetErrorInfo(out msg);

                if (!string.IsNullOrWhiteSpace(msg))
                {
                    logger.Error(msg);
                }
                throw;
            }
            finally
            {
                if (pInfo != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pInfo);
            }
        }

        public static string ComputeHash(string file)
        {
            using (FileStream stream = File.OpenRead(file))
            {
                var sha = new SHA256Managed();
                byte[] checksum = sha.ComputeHash(stream);
                return BitConverter.ToString(checksum).Replace("-", string.Empty);
            }
        }

        private IntPtr GetDebugInfo(string args, string targetExe, string outputDirectory)
        {
            var info = new VsDebugTargetInfo();
            info.cbSize = (uint) Marshal.SizeOf(info);
            info.dlo = DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;

            info.bstrExe = Path.Combine(outputDirectory, targetExe);
            info.bstrCurDir = outputDirectory;
            info.bstrArg = args; // no command line parameters
            info.bstrRemoteMachine = null; // debug locally
            info.grfLaunch = (uint) __VSDBGLAUNCHFLAGS.DBGLAUNCH_StopDebuggingOnEnd;
            info.fSendStdoutToOutputWindow = 0;
            info.clsidCustom = AD7Guids.EngineGuid;
            info.grfLaunch = 0;

            IntPtr pInfo = Marshal.AllocCoTaskMem((int) info.cbSize);
            Marshal.StructureToPtr(info, pInfo, false);
            return pInfo;
        }
    }
}