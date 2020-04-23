// TODO: this file finds assemblies used in build system.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq.Expressions;
using UnityEngine;
using UnityEditor;
using Mono.Cecil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;

using Object = UnityEngine.Object;

namespace Capstones.UnityEditorEx
{
    public static class UnityAssemblyUtils
    {
        public static bool IsPluginOnEditor(this PluginImporter importer)
        {
            if (importer.GetCompatibleWithAnyPlatform())
            {
                if (importer.GetExcludeEditorFromAnyPlatform())
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                if (importer.GetCompatibleWithEditor())
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        public static bool IsPluginOnPlatform(this PluginImporter importer, BuildTarget platform)
        {
            if (importer.GetCompatibleWithAnyPlatform())
            {
                if (importer.GetExcludeFromAnyPlatform(platform))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                if (importer.GetCompatibleWithPlatform(platform))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        public static bool IsPluginOnPlatform(this PluginImporter importer, string platform)
        {
            if (importer.GetCompatibleWithAnyPlatform())
            {
                if (importer.GetExcludeFromAnyPlatform(platform))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                if (importer.GetCompatibleWithPlatform(platform))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private static Func<PluginImporter, bool> _Func_IsExplicitlyReferenced;
        public static bool IsExplicitlyReferenced(this PluginImporter importer)
        {
            if (_Func_IsExplicitlyReferenced == null)
            {
                var pi = typeof(PluginImporter).GetProperty("IsExplicitlyReferenced", BindingFlags.NonPublic | BindingFlags.Instance);
                var pimporter = Expression.Parameter(typeof(PluginImporter));
                var lambda = Expression.Lambda<Func<PluginImporter, bool>>(Expression.Property(pimporter, pi.GetMethod), pimporter);
                _Func_IsExplicitlyReferenced = lambda.Compile();
            }
            return _Func_IsExplicitlyReferenced(importer);
        }

        public static readonly Dictionary<string, Func<string, bool>> OptionalAssembliesAndPackages = new Dictionary<string, Func<string, bool>>()
        {
            { "com.unity.analytics", package => UnityEditor.Analytics.AnalyticsSettings.enabled },
        };
        public static bool IsPackageOrAssemblyEnabled(string item)
        {
            Func<string, bool> checkFunc;
            if (OptionalAssembliesAndPackages.TryGetValue(item, out checkFunc))
            {
                return checkFunc(item);
            }
            return true;
        }
        public static bool IsAssemblyEnabled(string assembly)
        {
            assembly = assembly.Replace('\\', '/');
            var asset = assembly;
            if (!assembly.StartsWith("Assets/", StringComparison.InvariantCultureIgnoreCase) && !assembly.StartsWith("Packages/", StringComparison.InvariantCultureIgnoreCase))
            {
                asset = UnityEditorEx.CapsModEditor.GetAssetNameFromPath(assembly);
            }
            if (asset == null)
            {
                return true;
            }
            var filename = System.IO.Path.GetFileName(assembly);
            if (!IsPackageOrAssemblyEnabled(filename))
            {
                return false;
            }
            var package = UnityEditorEx.CapsModEditor.GetAssetPackage(asset);
            if (!string.IsNullOrEmpty(package))
            {
                if (!IsPackageOrAssemblyEnabled(package))
                {
                    return false;
                }
            }
            return true;
        }
    }
}