namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    public static class PrivateFunctionAccessHelper
    {
        private const BindingFlags EFlagsStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags EFlagsInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        #region Run Method

        /// <summary>
        /// Runs a method on a type, given its parameters. This is useful for
        /// calling private methods.
        /// </summary>
        /// <returns>The return value of the called method.</returns>
        public static object RunStaticMethod(Type t, string strMethod, object[] aobjParams)
        {
            return RunMethod(t, strMethod, null, aobjParams, EFlagsStatic);
        } //end of method

        public static object RunInstanceMethod(Type t, string strMethod, object objInstance, object[] aobjParams)
        {
            return RunMethod(t, strMethod, objInstance, aobjParams, EFlagsInstance);
        } //end of method

        public static T RunStaticAsyncMethod<T>(Type t, string strMethod, object[] aobjParams)
        {
            var obj = RunMethod(t, strMethod, null, aobjParams, EFlagsStatic);
            return ((Task<T>)obj).Result;
        } //end of method

        public static void RunStaticAsyncMethod(Type t, string strMethod, object[] aobjParams)
        {
            var obj = RunMethod(t, strMethod, null, aobjParams, EFlagsStatic);
            var task = obj as Task;
            if (task != null)
            {
                task.Wait();
            }
        } //end of method

        public static T RunInstanceAsyncMethod<T>(Type t, string strMethod, object objInstance, object[] aobjParams)
        {
            var obj = RunMethod(t, strMethod, objInstance, aobjParams, EFlagsInstance);
            return ((Task<T>)obj).Result;
        } //end of method

        public static void RunInstanceAsyncMethod(Type t, string strMethod, object objInstance, object[] aobjParams)
        {
            var obj = RunMethod(t, strMethod, objInstance, aobjParams, EFlagsInstance);
            var task = obj as Task;
            if (task != null)
            {
                task.Wait();
            }
        } //end of method
        
        private static object RunMethod(Type t, string strMethod, object objInstance, object[] aobjParams, BindingFlags eFlags)
        {
            var mis = t.GetMethods(eFlags).Where(mi => mi.Name == strMethod).ToArray();
            var m = mis.Length > 1
                ? mis.FirstOrDefault(
                    mi =>
                        mi.GetParameters().Length == aobjParams.Length &&
                        Enumerable.Range(0, mi.GetParameters().Length)
                            .All(
                                index =>
                                    // Cannot get type of null, confirm it is reference type.
                                    (aobjParams[index] == null && !mi.GetParameters()[index].ParameterType.IsValueType) ||
                                    // object can be casted
                                    mi.GetParameters()[index].ParameterType.IsInstanceOfType(aobjParams[index]) ||
                                    // Parameter is a ref and object can be casted to underlying type
                                    (mi.GetParameters()[index].ParameterType.GetElementType() != null &&
                                     mi.GetParameters()[index].ParameterType.GetElementType().IsInstanceOfType(aobjParams[index]))))
                : mis.FirstOrDefault();
            if (m == null)
            {
                throw new ArgumentException("There is no method '" + strMethod + "' for type '" + t + "'.");
            }

            var objRet = m.Invoke(objInstance, aobjParams);
            return objRet;
        } //end of method

        #endregion

        #region Get or Set Field

        public static Dictionary<string, object> GetFieldValues(object obj)
        {
            return GetFieldValues(obj.GetType());
        }

        public static Dictionary<string, object> GetFieldValues(Type type)
        {
            // FieldInfo[]
            var map = new Dictionary<string, object>();
            var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            foreach(var fieldInfo in fieldInfos) {
                if (fieldInfo.FieldType == typeof(string))
                {
                    var value = fieldInfo.GetValue(null);
                    map[fieldInfo.Name] = value;
                }
            }
            return map;
        }

        public static object GetPrivateField(
            Type parentType, string strProperty, object objInstance, bool isStaticProperty = false)
        {
            var f = parentType?.GetField(
                strProperty, isStaticProperty ? EFlagsStatic : EFlagsInstance);
            if (f == null)
            {
                throw new ArgumentException("There is no field '" + strProperty + "' for type '" + parentType + "'.");
            }

            return f.GetValue(objInstance);
        }

        public static void SetPrivateField(
            Type parentType, string strProperty, object objInstance, object value,
            bool isStaticProperty = false)
        {
            var f = parentType?.GetField(
                strProperty, isStaticProperty ? EFlagsStatic : EFlagsInstance);
            if (f == null)
            {
                throw new ArgumentException("There is no field '" + strProperty + "' for type '" + parentType + "'.");
            }

            f.SetValue(objInstance, value);
        }

        #endregion

        #region Get or Set Property

        public static object GetPrivateProperty(
            Type parentType, string strProperty, object objInstance, bool isStaticProperty = false)
        {
            var p = parentType?.GetProperty(
                strProperty, isStaticProperty ? EFlagsStatic : EFlagsInstance);
            if (p == null)
            {
                throw new ArgumentException("There is no property '" + strProperty + "' for type '" + parentType + "'.");
            }

            return p.GetValue(objInstance, null);
        }

        public static void SetPrivateProperty(Type t, string strProperty, object objInstance, object value, bool isStaticProperty = false)
        {
            var p = t?.GetProperty(strProperty, isStaticProperty ? EFlagsStatic : EFlagsInstance);
            if (p == null)
            {
                throw new ArgumentException("There is no property '" + strProperty + "' for type '" + t + "'.");
            }

            p.SetValue(objInstance, value, null);
        }

        #endregion

        #region constructor
        public static object RunConstructor(Type t, Type[] constructorParamTypes, object[] aobjParams)
        {
            var constructor = t?.GetConstructor(EFlagsInstance, null, constructorParamTypes, new ParameterModifier[0]);
            if (constructor == null)
            {
                throw new ArgumentException("There is no constructor for type '" + t + "' with params '" +
                                            string.Join(",", constructorParamTypes.Select(type => type.ToString())) +
                                            ".");
            }
            return constructor.Invoke(aobjParams);
        } //end of method
        #endregion

        #region CreateObject

        /// <summary>
        /// This uses reflection to allow initialization of objects with internal constructors
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static T CreateInstance<T>(params object[] args)
        {
            var type = typeof(T);
            var instance = type.Assembly.CreateInstance(
                type.FullName, false,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null, args, null, null);
            return (T)instance;
        }

        #endregion 
    } //end of class

} //end of namespace
