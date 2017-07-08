﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoRemoteDebugger.Debugger.DebugEngineHost
{
    public static class HostOutputWindowEx
    {
        // Use an extra class so that we have a seperate class which depends on VS interfaces
        private static class VsImpl
        {
            internal static void SetText(string outputMessage)
            {
                var outputWindow = (IVsOutputWindow)Package.GetGlobalService(typeof(SVsOutputWindow));
                if (outputWindow == null)
                {
                    return;
                }

                IVsOutputWindowPane pane;
                Guid guidDebugOutputPane = VSConstants.GUID_OutWindowDebugPane;
                var hr = outputWindow.GetPane(ref guidDebugOutputPane, out pane);
                if (hr < 0)
                {
                    return;
                }

                hr = pane.OutputString(outputMessage);
            }
        }

        /// <summary>
        /// Write text to the Debug VS Output window pane directly. This is used to write information before the session create event.
        /// </summary>
        /// <param name="outputMessage"></param>
        public static void WriteLaunchError(string outputMessage)
        {
            try
            {
                VsImpl.SetText(outputMessage);
            }
            catch (Exception)
            {
            }
        }
    }
}
