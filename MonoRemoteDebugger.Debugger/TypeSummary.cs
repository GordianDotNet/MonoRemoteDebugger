using System.Collections.Generic;
using System.Linq;
using Mono.Debugger.Soft;

namespace MonoRemoteDebugger.Debugger
{
    public class TypeSummary
    {
        private MethodMirror[] _methods;
        private Dictionary<string, MethodMirror> _anonymousFunctions;

        public TypeMirror TypeMirror { get; set; }

        public MethodMirror[] Methods
        {
            get
            {
                lock (this)
                {
                    if (_methods == null && TypeMirror != null)
                        _methods = TypeMirror.GetMethods();
                }

                return _methods;
            }
        }

        public Dictionary<string, MethodMirror> AnonymousFunctions
        {
            get
            {
                lock (this)
                {
                    if (_anonymousFunctions == null && TypeMirror != null)
                    {
                        // TODO: filter only AnonymousFunctions 
                        _anonymousFunctions = TypeMirror.GetNestedTypes().Where(x => x.Name[0] == '<').SelectMany(x => x.GetMethods().Where(y => y.Name[0] == '<')).ToDictionary(x => x.FullName);
                    }
                }

                return _anonymousFunctions;
            }
        }
    }
}