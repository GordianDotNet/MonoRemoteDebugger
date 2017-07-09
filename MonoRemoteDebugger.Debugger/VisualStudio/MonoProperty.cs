﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Mono.Debugger.Soft;
using Microsoft.MIDebugEngine;
using System.Reflection;

namespace MonoRemoteDebugger.Debugger.VisualStudio
{
    internal class MonoProperty : IDebugProperty2
    {
        private readonly StackFrame _stackFrame;
        private readonly Mirror _currentMirror;
        private readonly ObjectMirror _parentMirror;
        private readonly StructMirror _parentStructMirror;
        private readonly TypeMirror _arrayElementType;
        private readonly Value _arrayValue;
        private readonly string _arrayIndices;

        public MonoProperty(StackFrame frame, Mirror currentMirror)
        {
            _stackFrame = frame;
            _currentMirror = currentMirror;
        }

        public MonoProperty(StackFrame frame, Mirror currentMirror, ObjectMirror parentMirror)
            : this(frame, currentMirror)
        {
            _parentMirror = parentMirror;
        }

        public MonoProperty(StackFrame frame, Mirror currentMirror, StructMirror parentStructMirror)
            : this(frame, currentMirror)
        {
            _parentStructMirror = parentStructMirror;
        }

        public MonoProperty(StackFrame frame, Mirror currentMirror, TypeMirror arrayElementType, Value arrayValue, string arrayIndices)
            : this(frame, currentMirror)
        {
            _arrayElementType = arrayElementType;
            _arrayValue = arrayValue;
            _arrayIndices = arrayIndices;
        }

        public int EnumChildren(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, ref Guid guidFilter,
            enum_DBG_ATTRIB_FLAGS dwAttribFilter, string pszNameFilter, uint dwTimeout,
            out IEnumDebugPropertyInfo2 ppEnum)
        {
            string error = null;
            var attributeInfo = enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_NONE;
            Value value = _arrayValue ?? GetValue(_currentMirror, out attributeInfo, out error);

            //System.Diagnostics.Debug.WriteLine($"--- EnumChildren from Type {_currentMirror?.GetType().FullName} - _arrayValue: {_arrayValue != null} ValueInternalType: {value?.GetType().FullName}");
            
            if (value is ArrayMirror)
            {
                var array = ((ArrayMirror)value);
                if (array.Rank == 1)
                {
                    ppEnum = new AD7PropertyEnum(array.GetValues(0, array.Length).Select((x, i) => new MonoProperty(_stackFrame, _currentMirror, array.Type.GetElementType(), x, i.ToString()).GetDebugPropertyInfo(dwFields)).ToArray());
                }
                else
                {
                    // multidim array [,,]
                    DEBUG_PROPERTY_INFO[] arrayValues = CreateMultiDimArrayPropertyInfos(dwFields, array);
                    ppEnum = new AD7PropertyEnum(arrayValues);
                }
                return VSConstants.S_OK;
            }
            else if (value is StructMirror)
            {
                var obj = value as StructMirror;

                var properties = obj.Type.GetProperties().Cast<Mirror>();
                var fields = obj.Type.GetFields();
                var children = properties.Concat(fields).ToList();

                ppEnum = new AD7PropertyEnum(children.Select(x => new MonoProperty(_stackFrame, x, obj).GetDebugPropertyInfo(dwFields)).ToArray());
                
                return VSConstants.S_OK;
            }
            else if (value is ObjectMirror)
            {
                var obj = value as ObjectMirror;
                
                var properties = obj.Type.GetProperties().Cast<Mirror>();
                var fields = obj.Type.GetFields();
                
                var children = properties.Concat(fields).ToList();

                ppEnum = new AD7PropertyEnum(children.Select(x => new MonoProperty(_stackFrame, x, obj).GetDebugPropertyInfo(dwFields)).ToArray());
                return VSConstants.S_OK;
            }
            else
            {
                //TODO
            }

            //TODO 
            ppEnum = new AD7PropertyEnum(new DEBUG_PROPERTY_INFO[0]);
            return VSConstants.S_OK;
        }
        
        private Value GetValue(Mirror mirror, out enum_DBG_ATTRIB_FLAGS attributeInfo, out string errorMessage)
        {
            Value value = null;
            attributeInfo = enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_NONE;
            errorMessage = null;

            try
            {
                if (mirror is LocalVariable)
                {
                    var localMirror = mirror as LocalVariable;
                    value = _stackFrame.GetValue(localMirror);
                }
                else if (mirror is FieldInfoMirror)
                {
                    var fieldMirror = mirror as FieldInfoMirror;
                    attributeInfo = GetAttributeInfo(fieldMirror.Attributes);
                
                    if (fieldMirror.IsStatic)
                    {                    
                        value = fieldMirror.DeclaringType?.GetValue(fieldMirror);
                    }
                    else if (_parentStructMirror != null)
                    {
                        value = _parentStructMirror[fieldMirror.Name];
                    }
                    else
                    {
                        var obj = _parentMirror ?? _stackFrame.GetThis() as ObjectMirror;
                        if (obj != null)
                        {
                            value = obj.GetValue(fieldMirror);                        
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"FieldInfoMirror - not static - this unknown: {obj?.GetType().FullName}");
                            errorMessage = $"Field is not in scope!";
                        }                    
                    }
                }
                else if (mirror is PropertyInfoMirror)
                {
                    var propertyMirror = mirror as PropertyInfoMirror;                
                    var getMethod = propertyMirror.GetGetMethod(true);

                    if (getMethod == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"--- GetValue getMethod is NULL!!!: {propertyMirror?.GetType().Name} ");
                        errorMessage = $"Error: GetMethod is null!";
                    }
                    else
                    {
                        attributeInfo = GetAttributeInfo(getMethod.Attributes);

                        var getMethodParams = getMethod.GetParameters();
                        if (getMethodParams.Length > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"getMethodParams.Length = {getMethodParams.Length} - not supported!");
                            errorMessage = $"Index property is not supported!";                        
                        }
                        else if (getMethod.IsStatic)
                        {
                            value = propertyMirror.DeclaringType?.InvokeMethod(_stackFrame.Thread, getMethod, Enumerable.Empty<Value>().ToList());
                        }
                        else if (_parentStructMirror != null)
                        {
                            value = _parentStructMirror.InvokeMethod(_stackFrame.Thread, getMethod, Enumerable.Empty<Value>().ToList());
                        }
                        else
                        {
                            var obj = _parentMirror ?? _stackFrame.GetThis() as ObjectMirror;
                            if (obj != null)
                            {
                                value = obj.InvokeMethod(_stackFrame.Thread, getMethod, Enumerable.Empty<Value>().ToList());
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"PropertyInfoMirror - not static - this unknown: {obj?.GetType().FullName}");
                                errorMessage = $"Property is not in scope!";
                            }
                        }
                    }
                }
                else if (mirror is Value)
                {
                    value = mirror as Value;
                }
                else if (mirror != null)
                {
                    errorMessage = $"Error: Mirror is not supported ({mirror?.GetType().FullName})";
                }
                else
                {
                    errorMessage = $"Error: Mirror is null!";
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Value error: {ex.Message}";
            }
            return value;
        }
        
        public int GetDerivedMostProperty(out IDebugProperty2 ppDerivedMost)
        {
            throw new NotImplementedException();
        }

        public int GetExtendedInfo(ref Guid guidExtendedInfo, out object pExtendedInfo)
        {
            throw new NotImplementedException();
        }

        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes)
        {
            throw new NotImplementedException();
        }

        public int GetMemoryContext(out IDebugMemoryContext2 ppMemory)
        {
            throw new NotImplementedException();
        }

        public int GetParent(out IDebugProperty2 ppParent)
        {
            throw new NotImplementedException();
        }

        public int GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, uint dwTimeout,
            IDebugReference2[] rgpArgs, uint dwArgCount, DEBUG_PROPERTY_INFO[] pPropertyInfo)
        {
            rgpArgs = null;
            pPropertyInfo[0] = GetDebugPropertyInfo(dwFields);
            return VSConstants.S_OK;
        }

        public int GetReference(out IDebugReference2 ppReference)
        {
            throw new NotImplementedException();
        }

        public int GetSize(out uint pdwSize)
        {
            throw new NotImplementedException();
        }

        public int SetValueAsReference(IDebugReference2[] rgpArgs, uint dwArgCount, IDebugReference2 pValue,
            uint dwTimeout)
        {
            throw new NotImplementedException();
        }

        public int SetValueAsString(string pszValue, uint dwRadix, uint dwTimeout)
        {
            throw new NotImplementedException();
        }

        internal DEBUG_PROPERTY_INFO GetDebugPropertyInfo(enum_DEBUGPROP_INFO_FLAGS dwFields)
        {
            var propertyInfo = new DEBUG_PROPERTY_INFO();

            var mirrorInfo = GetMirrorInfo(_currentMirror) ?? new MirrorCommonInfo { FullName = "UNKNOWN-FullName", Name = "UNKNOWN-Name", Type = null };

            System.Diagnostics.Debug.WriteLine($"GetDebugPropertyInfo: {_currentMirror?.GetType().Name} - typeMirror: {_arrayElementType?.ToString()} childMirror: {_arrayValue?.GetType().FullName}");
                        
            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME) != 0)
            {
                propertyInfo.bstrFullName = mirrorInfo.Name;
                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME;
            }

            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME) != 0)
            {
                propertyInfo.bstrName = (_arrayIndices != null) ? $"[{_arrayIndices}]" : mirrorInfo.Name;
                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME;
            }

            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE) != 0)
            {
                propertyInfo.bstrType = mirrorInfo.Type?.FullName ?? "UNKNOWN-Type";
                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE;
            }

            var isExpandable = false;

            var attributeInfos = enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_NONE;

            string valueError = null;
            Value value = _arrayValue ?? GetValue(_currentMirror, out attributeInfos, out valueError);
            
            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE) != 0)
            {
                if (value is StringMirror)
                {
                    var obj = ((StringMirror)value);
                    propertyInfo.bstrValue = $"\"{obj.Value}\"";
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_RAW_STRING;
                }
                else if (value is ArrayMirror)
                {
                    var obj = ((ArrayMirror)value);
                    isExpandable = obj.Length > 0;
                    if (obj.Rank == 1)
                    {
                        propertyInfo.bstrValue = $"{{{obj.Type.GetElementType().Name}[{obj.Length}]}}";
                    }
                    else
                    {
                        // Support multi dim array [,,]
                        var dimUpperBound = new int[obj.Rank];
                        for (int dim = 0; dim < obj.Rank; dim++)
                        {
                            dimUpperBound[dim] = obj.GetLowerBound(dim) + obj.GetLength(dim);
                        }

                        propertyInfo.bstrValue = $"{_arrayElementType?.Name ?? obj.Type.GetElementType().Name}[{string.Join(",", dimUpperBound)}]";
                    }
                }
                else if (value is StructMirror)
                {
                    if (value is EnumMirror)
                    {
                        var obj = ((EnumMirror)value);
                        propertyInfo.bstrValue = $"{obj.StringValue}";
                    }
                    else
                    {
                        isExpandable = true;
                        var obj = ((StructMirror)value);
                        propertyInfo.bstrValue = $"{{{obj.Type.Namespace}.{obj.Type.Name}}}";
                    }
                }
                else if (value is ObjectMirror)
                {
                    isExpandable = true;
                    var obj = ((ObjectMirror)value);
                    propertyInfo.bstrValue = $"{{{obj.Type.Namespace}.{obj.Type.Name}}}";
                }
                else if (value is PrimitiveValue)
                {
                    var obj = ((PrimitiveValue)value);
                    propertyInfo.bstrValue = obj.Value?.ToString() ?? $"null";
                }
                else if (value != null)
                {
                    propertyInfo.bstrValue = $"Unsupported value has Type: {value?.GetType().FullName}";
                }
                else
                {
                    propertyInfo.bstrValue = valueError ?? $"Value evaluation error!";
                }

                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE;
            }

            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB) != 0)
            {
                propertyInfo.dwAttrib = enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_READONLY;

                if (isExpandable)
                {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_OBJ_IS_EXPANDABLE;
                }
                
                propertyInfo.dwAttrib |= attributeInfos;

                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB;
            }

            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP) != 0)
            {
                propertyInfo.pProperty = this;
                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP;
            }

            return propertyInfo;
        }
        
        private MirrorCommonInfo GetMirrorInfo(Mirror mirror)
        {
            MirrorCommonInfo info = null;
            
            //var methodMirror = mirror as MethodMirror;
            var propertyMirror = mirror as PropertyInfoMirror;
            var fieldMirror = mirror as FieldInfoMirror;
            var parameterMirror = mirror as ParameterInfoMirror;
            var localMirror = mirror as LocalVariable;
            var objectMirror = mirror as ObjectMirror;

            //if (methodMirror != null)
            //{
            //    childInfo = new PropertyChildInfo();
            //    childInfo.Name = methodMirror.Name;
            //    childInfo.FullName = methodMirror.FullName;
            //}
            //else

            if (propertyMirror != null)
            {
                info = new MirrorCommonInfo();
                info.Name = propertyMirror.Name;
                info.FullName = propertyMirror.PropertyType.FullName;
                info.Type = propertyMirror.PropertyType;
            }
            else if (fieldMirror != null)
            {
                info = new MirrorCommonInfo();
                info.Name = fieldMirror.Name;
                info.FullName = fieldMirror.FieldType.FullName;
                info.Type = fieldMirror.FieldType;
            }
            else if (parameterMirror != null)
            {
                info = new MirrorCommonInfo();
                info.Name = parameterMirror.Name;
                info.FullName = parameterMirror.ParameterType.FullName;
                info.Type = parameterMirror.ParameterType;
            }
            else if (localMirror != null)
            {
                info = new MirrorCommonInfo();
                info.Name = localMirror.Name;
                info.FullName = localMirror.Type.FullName;
                info.Type = localMirror.Type;
            }
            else if (objectMirror != null)
            {
                info = new MirrorCommonInfo();
                info.Name = "#ObjectMirror#-UNKNOWN-NAME";
                info.FullName = objectMirror.Type.FullName;
                info.Type = objectMirror.Type;
            }

            return info;
        }

        private bool IsExpandable(MirrorCommonInfo info)
        {
            return info != null && (info.Type.IsArray || !info.Type.IsPrimitive);
        }

        private DEBUG_PROPERTY_INFO[] CreateMultiDimArrayPropertyInfos(enum_DEBUGPROP_INFO_FLAGS dwFields, ArrayMirror array)
        {
            var arrayValues = new DEBUG_PROPERTY_INFO[array.Length];

            var dimIndex = new int[array.Rank];
            var dimLowerBound = new int[array.Rank];
            var dimUpperBound = new int[array.Rank];

            for (int dim = 0; dim < array.Rank; dim++)
            {
                dimIndex[dim] = array.GetLowerBound(dim);
                dimLowerBound[dim] = array.GetLowerBound(dim);
                dimUpperBound[dim] = array.GetLowerBound(dim) + array.GetLength(dim) - 1;
            }

            for (int i = 0; i < array.Length; i++)
            {
                var valueAtIndex = array.GetValues(i, 1).First();
                arrayValues[i] = new MonoProperty(_stackFrame, _currentMirror, array.Type.GetElementType(), valueAtIndex, string.Join(",", dimIndex)).GetDebugPropertyInfo(dwFields);

                for (int dim = array.Rank - 1; dim >= 0; dim--)
                {
                    if (dimIndex[dim] < dimUpperBound[dim])
                    {
                        dimIndex[dim]++;
                        break;
                    }
                    else
                    {
                        dimIndex[dim] = dimLowerBound[dim];
                    }
                }
            }

            return arrayValues;
        }

        private enum_DBG_ATTRIB_FLAGS GetAttributeInfo(FieldAttributes attributes)
        {
            enum_DBG_ATTRIB_FLAGS attributeInfo = enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_NONE;

            if (attributes.HasFlag(FieldAttributes.Private))
            {
                attributeInfo |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_PRIVATE;
            }
            if (attributes.HasFlag(FieldAttributes.Public))
            {
                attributeInfo |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_PUBLIC;
            }
            if (attributes.HasFlag(FieldAttributes.Family))
            {
                attributeInfo |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_PROTECTED;
            }
            if (attributes.HasFlag(FieldAttributes.Static))
            {
                attributeInfo |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_STORAGE_STATIC;
            }

            return attributeInfo;
        }

        private enum_DBG_ATTRIB_FLAGS GetAttributeInfo(MethodAttributes attributes)
        {
            enum_DBG_ATTRIB_FLAGS attributeInfo = enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_NONE;

            if (attributes.HasFlag(MethodAttributes.Private))
            {
                attributeInfo |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_PRIVATE;
            }
            if (attributes.HasFlag(MethodAttributes.Public))
            {
                attributeInfo |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_PUBLIC;
            }
            if (attributes.HasFlag(MethodAttributes.Family))
            {
                attributeInfo |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_PROTECTED;
            }
            if (attributes.HasFlag(MethodAttributes.Static))
            {
                attributeInfo |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_STORAGE_STATIC;
            }

            return attributeInfo;
        }
    }

    internal class MirrorCommonInfo
    {
        public string FullName { get; set; }
        public string Name { get; set; }
        public TypeMirror Type { get; set; }
    }
}