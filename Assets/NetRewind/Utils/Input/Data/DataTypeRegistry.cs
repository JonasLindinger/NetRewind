using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace NetRewind.Utils.Input.Data
{
    public static class DataTypeRegistry
    {
        private static bool _initialized;
        private static readonly Dictionary<Type, int> TypeToId = new();
        private static readonly List<Func<IData>> FactoriesById = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            InitializeIfNeeded();
        }

        public static void InitializeIfNeeded()
        {
            if (_initialized)
                return;

            _initialized = true;

            var allMarkedTypes = new List<Type>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (type == null)
                        continue;

                    var hasAttr = type.GetCustomAttribute<DataTypeAttribute>() != null;
                    var isIData = typeof(IData).IsAssignableFrom(type);

                    if (hasAttr)
                    {
                        // Rule 1: attribute only allowed on struct + IData
                        if (!type.IsValueType || !isIData)
                        {
                            throw new InvalidOperationException(
                                $"[DataType] can only be applied to structs implementing IData. " +
                                $"Type '{type.FullName}' violates this rule.");
                        }

                        allMarkedTypes.Add(type);
                    }

                    // Rule 2 is handled after the scan
                }
            }

            // Rule 2: every IData must have [DataType]
            var missingAttributeTypes = assemblies
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
                })
                .Where(t => t != null
                            && typeof(IData).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && t.IsValueType
                            && t.GetCustomAttribute<DataTypeAttribute>() == null)
                .ToList();

            if (missingAttributeTypes.Count > 0)
            {
                var list = string.Join("\n - ", missingAttributeTypes.Select(t => t.FullName));
                throw new InvalidOperationException(
                    "The following IData structs are missing the [DataType] attribute:\n - " + list);
            }

            // Now we know:
            // - Any [DataType] is on a struct that implements IData
            // - Every struct IData has [DataType]

            // Deterministic ID assignment
            allMarkedTypes.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

            for (int i = 0; i < allMarkedTypes.Count; i++)
            {
                var type = allMarkedTypes[i];
                int id = i;

                TypeToId[type] = id;
                FactoriesById.Add(() => (IData)Activator.CreateInstance(type));
            }
        }

        public static int GetId<T>() where T : IData
        {
            InitializeIfNeeded();

            var type = typeof(T);
            if (!TypeToId.TryGetValue(type, out var id))
                throw new InvalidOperationException(
                    $"Type {type.FullName} is not registered as an IData with [DataType].");
            return id;
        }

        public static IData Create(int id)
        {
            InitializeIfNeeded();

            if (id < 0 || id >= FactoriesById.Count)
                return null;

            return FactoriesById[id]();
        }
        
        public static int GetId(Type type)
        {
            InitializeIfNeeded();
      
            if (!TypeToId.TryGetValue(type, out var id))
                throw new InvalidOperationException($"Type {type.FullName} not registered.");
      
            return id;
        }
    }
    
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class DataTypeAttribute : Attribute
    {
        
    }
}