﻿using EnvDTE;
using Microsoft.VisualStudio.Debugger.Interop;
using Mono.Debugging.Soft;
using MonoRemoteDebugger.Debugger;
using MonoRemoteDebugger.Debugger.VisualStudio;
using MonoRemoteDebugger.SharedLib.Settings;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Runtime.Serialization.Formatters.Binary;

namespace Mono.Debugging.VisualStudio
{
    [Guid(AD7Guids.XamarinEngineString)]
    public class XamarinEngine : IDebugEngine2, IDebugEngineLaunch2
    {
        protected Engine _engine;

        protected SoftDebuggerSession _session;
        protected StartInfo _startInfo;

        public static Project StartupProject { set; get; }

        public XamarinEngine()
        {
            _engine = new Engine();
        }

        private string SerializeDebuggerOptions(string jsonDebugOptions)
        {
            DebugHelper.TraceEnteringMethod();
            var debugOptions = DebugOptions.DeserializeFromJson(jsonDebugOptions);
            
            _session = new SoftDebuggerSession();
            _session.TargetReady += (sender, eventArgs) =>
            {
                Debug.WriteLine("TargetReady!");
            };
            _session.ExceptionHandler = exception => true;
            _session.TargetExited += (sender, x) =>
            {
                Debug.WriteLine("TargetExited!");
            };
            _session.TargetUnhandledException += (sender, x) =>
            {
                Debug.WriteLine("TargetUnhandledException!");
            };
            _session.LogWriter = (stderr, text) => Debug.WriteLine(text);
            _session.OutputWriter = (stderr, text) => Debug.WriteLine(text);
            _session.TargetThreadStarted += (sender, x) => Debug.WriteLine("TargetThreadStarted!");
            _session.TargetThreadStopped += (sender, x) =>
            {
                Debug.WriteLine("TargetThreadStopped!");
            };
            _session.TargetStopped += (sender, x) => Debug.WriteLine(x.Type);
            _session.TargetStarted += (sender, x) => Debug.WriteLine("TargetStarted");
            _session.TargetSignaled += (sender, x) => Debug.WriteLine(x.Type);
            _session.TargetInterrupted += (sender, x) => Debug.WriteLine(x.Type);
            _session.TargetExceptionThrown += (sender, x) =>
            {
                Debug.WriteLine("TargetExceptionThrown!");
            };
            _session.TargetHitBreakpoint += (sender, x) =>
            {
                Debug.WriteLine("TargetHitBreakpoint!");
            };
            _session.TargetEvent += _session_TargetEvent;

            var startupProject = StartupProject;
            _startInfo = new StartInfo(
                new SoftDebuggerConnectArgs(debugOptions.TargetExeFileName, debugOptions.GetHostIP(), debugOptions.GetMonoDebugPort()),
                new DebuggingOptions() { EvaluationTimeout = 30000, MemberEvaluationTimeout = 30000, ModificationTimeout = 30000, SocketTimeout = 30000 },
                startupProject
                );

            SessionMarshalling sessionMarshalling = new SessionMarshalling(_session, _startInfo);
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                ObjRef oref = RemotingServices.Marshal(sessionMarshalling);
                bf.Serialize(ms, oref);
                return Convert.ToBase64String(ms.ToArray());
            }
        }
        
        private void _session_TargetEvent(object sender, Client.TargetEventArgs e)
        {
            DebugHelper.TraceEnteringMethod();
            Debug.WriteLine("TargetEvent: " + e.Type.ToString());
        }

        #region IDebugEngineLaunch2

        public /*override*/ int LaunchSuspended(string pszServer, IDebugPort2 pPort, string pszExe, string pszArgs, string pszDir, string bstrEnv, string pszOptions, enum_LAUNCH_FLAGS dwLaunchFlags, uint hStdInput, uint hStdOutput, uint hStdError, IDebugEventCallback2 pCallback, out IDebugProcess2 ppProcess)
        {
            DebugHelper.TraceEnteringMethod();
            var base64Options = SerializeDebuggerOptions(pszOptions);
            var result = _engine.LaunchSuspended(pszServer, pPort, pszExe, pszArgs, pszDir, bstrEnv, base64Options, dwLaunchFlags, hStdInput, hStdOutput, hStdError, pCallback, out ppProcess);
            
            return result;
        }

        private void _session_TargetStarted(object sender, EventArgs e)
        {
            DebugHelper.TraceEnteringMethod();
        }

        public /*override*/ int ResumeProcess(IDebugProcess2 pProcess)
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.ResumeProcess(pProcess);
        }

        public /*override*/ int CanTerminateProcess(IDebugProcess2 pProcess)
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.CanTerminateProcess(pProcess);
        }

        public /*override*/ int TerminateProcess(IDebugProcess2 pProcess)
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.TerminateProcess(pProcess);
        }

        #endregion

        #region IDebugEngine2

        public /*override*/ int EnumPrograms(out IEnumDebugPrograms2 ppEnum)
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.EnumPrograms(out ppEnum);
        }

        public /*override*/ int Attach(IDebugProgram2[] rgpPrograms, IDebugProgramNode2[] rgpProgramNodes, uint celtPrograms, IDebugEventCallback2 pCallback, enum_ATTACH_REASON dwReason)
        {
            DebugHelper.TraceEnteringMethod();

            try
            {
                _session.Run(_startInfo, _startInfo.SessionOptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message + " - " + ex.StackTrace);
            }

            return _engine.Attach(rgpPrograms, rgpProgramNodes, celtPrograms, pCallback, dwReason);
        }

        public /*override*/ int CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest, out IDebugPendingBreakpoint2 ppPendingBP)
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.CreatePendingBreakpoint(pBPRequest, out ppPendingBP);
        }

        public /*override*/ int SetException(EXCEPTION_INFO[] pException)
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.SetException(pException);
        }

        public /*override*/ int RemoveSetException(EXCEPTION_INFO[] pException)
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.RemoveSetException(pException);
        }

        public /*override*/ int RemoveAllSetExceptions(ref Guid guidType)
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.RemoveAllSetExceptions(ref guidType);
        }

        public /*override*/ int GetEngineId(out Guid pguidEngine)
        {
            DebugHelper.TraceEnteringMethod();
            var temp = _engine.GetEngineId(out pguidEngine);
            pguidEngine = new Guid(AD7Guids.XamarinEngineString);
            return 0;
        }

        public /*override*/ int DestroyProgram(IDebugProgram2 pProgram)
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.DestroyProgram(pProgram);
        }

        public /*override*/ int ContinueFromSynchronousEvent(IDebugEvent2 pEvent)
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.ContinueFromSynchronousEvent(pEvent);
        }

        public /*override*/ int SetLocale(ushort wLangID)
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.SetLocale(wLangID);
        }

        public /*override*/ int SetRegistryRoot(string pszRegistryRoot)
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.SetRegistryRoot(pszRegistryRoot);
        }

        public /*override*/ int SetMetric(string pszMetric, object varValue)
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.SetMetric(pszMetric, varValue);
        }

        public /*override*/ int CauseBreak()
        {
            DebugHelper.TraceEnteringMethod();
            return _engine.CauseBreak();
        }

        #endregion

    }
}
