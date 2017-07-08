using System;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Mono.Debugger.Soft;
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
            dwFields = enum_MODULE_INFO_FIELDS.MIF_NONE;
            var assemblyName = AssemblyMirror.GetName();
            
            pinfo[0].m_bstrName = assemblyName.FullName;
            dwFields |= enum_MODULE_INFO_FIELDS.MIF_NAME;

            pinfo[0].m_bstrVersion = assemblyName.Version.ToString();
            dwFields |= enum_MODULE_INFO_FIELDS.MIF_VERSION;
            
            dwFields |= enum_MODULE_INFO_FIELDS.MIF_URL;
            pinfo[0].m_bstrUrl = AssemblyMirror.Location;
            
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

        public AD7Thread(AD7Engine engine, ThreadMirror thread)
        {
            _engine = engine;            
            ThreadMirror = thread;
        }

        public int CanSetNextStatement(IDebugStackFrame2 pStackFrame, IDebugCodeContext2 pCodeContext)
        {
            return VSConstants.S_FALSE;
        }

        public int EnumFrameInfo(enum_FRAMEINFO_FLAGS dwFieldSpec, uint nRadix, out IEnumDebugFrameInfo2 ppEnum)
        {
            StackFrame[] stackFrames = ThreadMirror.GetFrames();
            ppEnum = new AD7FrameInfoEnum(stackFrames.Select(x => new AD7StackFrame(_engine, this, x).GetFrameInfo(dwFieldSpec)).ToArray());
            return VSConstants.S_OK;
        }

        public int GetLogicalThread(IDebugStackFrame2 pStackFrame, out IDebugLogicalThread2 ppLogicalThread)
        {
            throw new NotImplementedException();
        }

        public int GetName(out string pbstrName)
        {
            pbstrName = $"{ThreadMirror.Name} [{ThreadMirror.ThreadId}]";
            return VSConstants.S_OK;
        }

        public int GetProgram(out IDebugProgram2 ppProgram)
        {
            ppProgram = _engine;
            return VSConstants.S_OK;
        }

        public int GetThreadId(out uint pdwThreadId)
        {
            pdwThreadId = (uint)ThreadMirror.ThreadId;
            return VSConstants.S_OK;
        }

        public int GetThreadProperties(enum_THREADPROPERTY_FIELDS dwFields, THREADPROPERTIES[] ptp)
        {
            return VSConstants.S_OK;
        }

        public int Resume(out uint pdwSuspendCount)
        {
            pdwSuspendCount = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int SetNextStatement(IDebugStackFrame2 pStackFrame, IDebugCodeContext2 pCodeContext)
        {
            return VSConstants.S_FALSE;
        }

        public int SetThreadName(string pszName)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int Suspend(out uint pdwSuspendCount)
        {
            pdwSuspendCount = 0;
            return VSConstants.E_NOTIMPL;
        }
    }
}