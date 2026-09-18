using System.Reflection;
using Game.Systems.LevelGeneration.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // Regression coverage for SCARAB-014 follow-up: LevelGenerationWindow's
    // CloneParameters() silently dropped SpareDoorCount/IsolatedPocketCount
    // because it hand-listed every GenerationParameters field, one of three
    // places (the others: Save/LoadPendingState's EditorPrefs round-trip) that
    // all had to be kept in sync by hand with GenerationParameters' own field
    // list. Fixed via a generic JsonUtility round-trip instead — this test
    // enumerates GenerationParameters' fields via reflection specifically so
    // it keeps covering the *next* new parameter too, without needing an edit.
    public class LevelGenerationWindowTests
    {
        [Test]
        public void CloneParameters_CopiesEveryGenerationParametersField()
        {
            var window = ScriptableObject.CreateInstance<LevelGenerationWindow>();
            try
            {
                var fields = typeof(GenerationParameters).GetFields(BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotEmpty(fields, "Sanity check: GenerationParameters should have public fields.");

                var original = new GenerationParameters();
                int distinctValue = 100;
                foreach (var field in fields)
                {
                    distinctValue += 7;
                    field.SetValue(original, distinctValue);
                }

                SetPrivateField(window, "_parameters", original);
                var clone = (GenerationParameters)InvokePrivateMethod(window, "CloneParameters");

                Assert.NotNull(clone, "CloneParameters() should never return null.");
                foreach (var field in fields)
                    Assert.AreEqual(field.GetValue(original), field.GetValue(clone),
                        $"CloneParameters() should copy {field.Name}.");
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"{target.GetType().Name} should have a private field named {fieldName}.");
            field.SetValue(target, value);
        }

        private static object InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method, $"{target.GetType().Name} should have a private method named {methodName}.");
            return method.Invoke(target, null);
        }
    }
}
