using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using EchoesOfTheVoid.Core.Combat.Gambits;

namespace EchoesOfTheVoid.Core.Roster {
  public static class RosterCloneUtility {
    public static GambitProfileData CloneGambitProfile(IGambitRuleSource source) {
      if (source == null) {
        return new GambitProfileData();
      }

      if (source is GambitProfileData dataProfile) {
        return DeepClone(dataProfile) ?? new GambitProfileData();
      }

      var intermediate = new GambitProfileData(null, source.DisplayName, source.Rules);
      return DeepClone(intermediate) ?? new GambitProfileData();
    }

    public static T DeepClone<T>(T source) where T : class {
      return DeepClone((object)source) as T;
    }

    public static object DeepClone(object source) {
      if (source == null) {
        return null;
      }

      Type type = source.GetType();
      if (type.IsPrimitive || type.IsEnum || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type)) {
        return source;
      }

      if (type.IsArray) {
        Array array = (Array)source;
        Array clone = Array.CreateInstance(type.GetElementType(), array.Length);
        for (int i = 0; i < array.Length; i++) {
          clone.SetValue(DeepClone(array.GetValue(i)), i);
        }

        return clone;
      }

      if (source is IList list) {
        IList cloneList = (IList)Activator.CreateInstance(type);
        foreach (object item in list) {
          cloneList.Add(DeepClone(item));
        }

        return cloneList;
      }

      object instance = Activator.CreateInstance(type);
      FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      foreach (FieldInfo field in fields) {
        object value = field.GetValue(source);
        object clonedValue = DeepClone(value);
        field.SetValue(instance, clonedValue);
      }

      return instance;
    }
  }
}
