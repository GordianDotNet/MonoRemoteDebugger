using System;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Mono.Debugger.Soft;
using MonoRemoteDebugger.Debugger;
using MonoRemoteDebugger.Debugger.VisualStudio;

namespace Microsoft.MIDebugEngine
{
    internal class AD7Assembly : IDebugModule2
    {
        private readonly AD7Engine _engine;

        public AssemblyMirror AssemblyMirror { get; private set; }

        public AD7Assembly(AD7Engine engine, AssemblyMirror assembly)
        {
            _engine = engine;
            AssemblyMirror = assembly;
        }

        #region IDebugModule2

        public int GetInfo(enum_MODULE_INFO_FIELDS dwFields, MODULE_INFO[] pinfo)
        {
            var assemblyName = AssemblyMirror.GetName();
            
            pinfo[0].m_bstrName = assemblyName.FullName;
            pinfo[0].dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_NAME;

            pinfo[0].m_bstrVersion = assemblyName.Version.ToString();
            pinfo[0].dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_VERSION;
            
            pinfo[0].m_bstrUrl = AssemblyMirror.Location;
            pinfo[0].dwValidFields |= enum_MODULE_INFO_FIELDS.MIF_URL;

            return VSConstants.S_OK;
        }

        public int ReloadSymbols_Deprecated(string pszUrlToSymbols, out string pbstrDebugMessage)
        {
            pbstrDebugMessage = null;
            return VSConstants.E_NOTIMPL;
        }

        #endregion
    }
    internal class AD7Thread : IDebugThread2
    {
        private readonly AD7Engine _engine;

        public ThreadMirror ThreadMirror { get; private set; }

        //private MethodMirror _threadPriorityGetMethodMirror;
        //private string _threadPriorityLastValue = "UNKNOWN";
        private string _lastLocation = string.Empty;

        public AD7Thread(AD7Engine engine, ThreadMirror thread)
        {
            _engine = engine;            
            ThreadMirror = thread;

            //_threadPriorityGetMethodMirror = thread?.Type.GetProperties().Where(x => x.Name == "Priority").Select(x => x.GetGetMethod()).FirstOrDefault();
        }

        public int CanSetNextStatement(IDebugStackFrame2 pStackFrame, IDebugCodeContext2 pCodeContext)
        {
            DebugHelper.TraceEnteringMethod();
            return VSConstants.S_FALSE;
        }

        public int EnumFrameInfo(enum_FRAMEINFO_FLAGS dwFieldSpec, uint nRadix, out IEnumDebugFrameInfo2 ppEnum)
        {
            DebugHelper.TraceEnteringMethod();
            StackFrame[] stackFrames = ThreadMirror.GetFrames();
            ppEnum = new AD7FrameInfoEnum(stackFrames.Select(x => new AD7StackFrame(_engine, this, x).GetFrameInfo(dwFieldSpec)).ToArray());
            return VSConstants.S_OK;
        }

        public int GetLogicalThread(IDebugStackFrame2 pStackFrame, out IDebugLogicalThread2 ppLogicalThread)
        {
            DebugHelper.TraceEnteringMethod();
            ppLogicalThread = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetName(out string pbstrName)
        {
            DebugHelper.TraceEnteringMethod();
            pbstrName = ThreadMirror.Name;
            return VSConstants.S_OK;
        }

        public int GetProgram(out IDebugProgram2 ppProgram)
        {
            DebugHelper.TraceEnteringMethod();
            ppProgram = _engine;
            return VSConstants.S_OK;
        }

        public int GetThreadId(out uint pdwThreadId)
        {
            DebugHelper.TraceEnteringMethod();
            pdwThreadId = (uint)ThreadMirror.ThreadId;
            return VSConstants.S_OK;
        }

        public int GetThreadProperties(enum_THREADPROPERTY_FIELDS dwFields, THREADPROPERTIES[] ptp)
        {
            DebugHelper.TraceEnteringMethod();
            
            ptp[0].dwThreadId = (uint)ThreadMirror.ThreadId;
            ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_ID;

            ptp[0].bstrName = ThreadMirror.Name;
            ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_NAME;

            //try
            //{
            //    if (_engine.IsSuspended)
            //    {
            //        var priorityValue = ThreadMirror.InvokeMethod(ThreadMirror, _threadPriorityGetMethodMirror, Enumerable.Empty<Value>().ToList()) as EnumMirror;
            //        _threadPriorityLastValue = priorityValue.StringValue;
            //    }
            //}
            //catch
            //{

            //}
            
            //if (_engine.DebuggedProcess.IsRunning)
            //{
            //    ptp[0].bstrPriority = ThreadMirror.IsThreadPoolThread ? "ThreadPoolThread" : "No ThreadPoolThread"; //_threadPriorityLastValue;
            //    ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_PRIORITY;

            //    ptp[0].dwThreadState = (uint)ThreadMirror.ThreadState;
            //    ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_STATE;

            //    StackFrame stackFrame = ThreadMirror.GetFrames().FirstOrDefault();
            //    if (stackFrame != null)
            //    {
            //        _lastLocation = $"{stackFrame.FileName}!{stackFrame.Location.Method.Name} Line {stackFrame.Location.LineNumber}";
            //    }

            //    ptp[0].bstrLocation = _lastLocation;
            //    ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_LOCATION;
            //}            

            return VSConstants.S_OK;
        }

        public int Resume(out uint pdwSuspendCount)
        {
            DebugHelper.TraceEnteringMethod();
            pdwSuspendCount = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int SetNextStatement(IDebugStackFrame2 pStackFrame, IDebugCodeContext2 pCodeContext)
        {
            DebugHelper.TraceEnteringMethod();
            return VSConstants.S_FALSE;
        }

        public int SetThreadName(string pszName)
        {
            DebugHelper.TraceEnteringMethod();
            return VSConstants.E_NOTIMPL;
        }

        public int Suspend(out uint pdwSuspendCount)
        {
            DebugHelper.TraceEnteringMethod();
            pdwSuspendCount = 0;
            return VSConstants.E_NOTIMPL;
        }
    }
}