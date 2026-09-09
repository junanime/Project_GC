using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Vampire.EditorTools
{
    public static class ProjectGCSerializedFieldScanner
    {
        [MenuItem(
            "Tools/Project GC/Validation/Scan Duplicate Serialized Fields")]
        private static void ScanDuplicateSerializedFields()
        {
            int duplicateCount = 0;

            foreach (Type type in
                     TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (type == null ||
                    type.IsAbstract)
                {
                    continue;
                }

                FieldInfo[] declaredFields =
                    type.GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);

                for (int i = 0;
                     i < declaredFields.Length;
                     i++)
                {
                    FieldInfo field =
                        declaredFields[i];

                    if (!IsUnitySerializedField(field))
                    {
                        continue;
                    }

                    Type parentType =
                        type.BaseType;

                    while (parentType != null &&
                           parentType != typeof(object))
                    {
                        FieldInfo parentField =
                            parentType.GetField(
                                field.Name,
                                BindingFlags.Instance |
                                BindingFlags.Public |
                                BindingFlags.NonPublic |
                                BindingFlags.DeclaredOnly);

                        if (parentField != null &&
                            IsUnitySerializedField(parentField))
                        {
                            duplicateCount++;

                            Debug.LogError(
                                "[ProjectGC Duplicate Serialized Field] " +
                                $"Field={field.Name} | " +
                                $"Child={type.FullName} | " +
                                $"Parent={parentType.FullName}");

                            break;
                        }

                        parentType =
                            parentType.BaseType;
                    }
                }
            }

            if (duplicateCount <= 0)
            {
                Debug.Log(
                    "[ProjectGC Validation] " +
                    "중복 Serialized Field 없음");

                return;
            }

            Debug.LogWarning(
                "[ProjectGC Validation] " +
                $"중복 Serialized Field 검색 완료 | " +
                $"Duplicates={duplicateCount}");
        }


        private static bool IsUnitySerializedField(
            FieldInfo field)
        {
            if (field == null)
            {
                return false;
            }

            if (field.IsStatic ||
                field.IsLiteral ||
                field.IsInitOnly ||
                field.IsNotSerialized)
            {
                return false;
            }

            if (field.IsPublic)
            {
                return true;
            }

            if (Attribute.IsDefined(
                    field,
                    typeof(SerializeField),
                    true))
            {
                return true;
            }

            if (Attribute.IsDefined(
                    field,
                    typeof(SerializeReference),
                    true))
            {
                return true;
            }

            return false;
        }
    }
}