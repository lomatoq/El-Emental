using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Elemental.Authoring.Editor.Fire
{
    // Version-specific Unity graph classes are internal. Call the installed package's
    // actual model API; never construct or patch its serialized representation.
    internal static class FireGraphApi
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        public static Type Type(string name)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            { var t = a.GetType(name, false); if (t != null) return t; }
            throw new InvalidOperationException("Installed Unity package type missing: " + name);
        }
        public static object New(string name, params object[] args)
        {
            var t = Type(name);
            if (typeof(ScriptableObject).IsAssignableFrom(t) && args.Length == 0) return ScriptableObject.CreateInstance(t);
            foreach (var c in t.GetConstructors(All))
                if (Bind(c.GetParameters(), args, out var bound)) return c.Invoke(bound);
            throw new MissingMethodException(name, ".ctor");
        }
        public static object Call(object obj, string name, params object[] args)
        {
            var type = obj is Type t ? t : obj.GetType();
            foreach (var method in type.GetMethods(All).Where(m => m.Name == name && !m.ContainsGenericParameters))
                if (Bind(method.GetParameters(), args, out var bound))
                {
                    try { return method.Invoke(obj is Type ? null : obj, bound); }
                    catch (TargetInvocationException e) { throw new InvalidOperationException(type.FullName + "." + name + " failed", e.InnerException); }
                }
            throw new MissingMethodException(type.FullName, name + "(" + string.Join(",",args.Select(a => a?.GetType().Name ?? "null")) + ")");
        }
        private static bool Bind(ParameterInfo[] parameters, object[] args, out object[] bound)
        {
            bound = null;
            if (args.Length > parameters.Length) return false;
            var result = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i >= args.Length) { if (!parameters[i].IsOptional && !parameters[i].IsOut) return false; result[i] = parameters[i].IsOut ? null : parameters[i].DefaultValue; continue; }
                var target = parameters[i].ParameterType;
                if (target.IsByRef) target = target.GetElementType();
                if (args[i] != null && !target.IsInstanceOfType(args[i])) return false;
                result[i] = args[i];
            }
            bound = result; return true;
        }
        public static object Get(object obj, string name)
        {
            var type = obj is Type t ? t : obj.GetType();
            for (; type != null; type = type.BaseType)
            {
                var p = type.GetProperty(name, All | BindingFlags.DeclaredOnly); if (p != null) return p.GetValue(obj is Type ? null : obj);
                var f = type.GetField(name, All | BindingFlags.DeclaredOnly); if (f != null) return f.GetValue(obj is Type ? null : obj);
            }
            throw new MissingMemberException(obj.ToString(), name);
        }
        public static void Set(object obj, string name, object value)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                var p = type.GetProperty(name, All | BindingFlags.DeclaredOnly); if (p?.SetMethod != null) { p.SetValue(obj, ConvertValue(p.PropertyType,value)); return; }
                var f = type.GetField(name, All | BindingFlags.DeclaredOnly); if (f != null) { f.SetValue(obj,ConvertValue(f.FieldType,value)); return; }
            }
            throw new MissingMemberException(obj.GetType().FullName, name);
        }
        private static object ConvertValue(Type type, object value) => type.IsEnum && value is string text ? System.Enum.Parse(type,text) : value;
        public static object Enum(string type, string name) => System.Enum.Parse(Type(type), name);
        public static Array Array(string type, params object[] values)
        { var result = System.Array.CreateInstance(Type(type),values.Length); for(int i=0;i<values.Length;i++) result.SetValue(values[i],i); return result; }
        public static object[] Items(object value) => ((IEnumerable)value).Cast<object>().ToArray();
        public static void Setting(object model, string name, object value)
        {
            var old = Call(model,"GetSettingValue",name);
            Call(model,"SetSettingValue",name,old != null ? ConvertValue(old.GetType(),value) : value);
        }
        public static object Slot(object container, string property, string name)
        {
            foreach (var slot in Items(Get(container,property)))
                if ((string)Get(Get(slot,"property"),"name") == name) return slot;
            throw new InvalidOperationException(container.GetType().Name + " has no " + property + " '" + name + "'. Available: " +
                string.Join(", ",Items(Get(container,property)).Select(s => Get(Get(s,"property"),"name"))));
        }
    }
}

