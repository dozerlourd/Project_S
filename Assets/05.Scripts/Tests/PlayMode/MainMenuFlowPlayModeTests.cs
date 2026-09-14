using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectS.Tests.PlayMode
{
    public sealed class MainMenuFlowPlayModeTests
    {
        private static readonly Type MainMenuType = GetGameplayType("ProjectS.UI.RtsMainMenu");
        private static readonly Type SceneFlowType = GetGameplayType("ProjectS.RtsSceneFlow");

        [UnityTest]
        public IEnumerator MainMenu_UsesConfiguredSceneAndResourceVisuals()
        {
            var menuObject = new GameObject("MainMenuTest");
            var menu = menuObject.AddComponent(MainMenuType);
            yield return null;

            Assert.That(GetProperty(menu, "GameSceneName"), Is.EqualTo("MapCreate_Scene"));
            Assert.That(GetProperty(menu, "BackgroundTexture"), Is.Not.Null);
            Assert.That(GetProperty(menu, "ButtonTexture"), Is.Not.Null);
            Assert.That((bool)InvokeStatic(SceneFlowType, "CanLoadScene", "MainMenu"), Is.True);
            Assert.That((bool)InvokeStatic(SceneFlowType, "CanLoadScene", "MapCreate_Scene"), Is.True);

            Object.Destroy(menuObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenu_LoadingGuardPreventsDuplicateActionsAndQuitUsesSharedPolicy()
        {
            var menuObject = new GameObject("MainMenuGuardTest");
            var menu = menuObject.AddComponent(MainMenuType);
            var quitRequested = false;
            Action onQuit = () => quitRequested = true;
            var quitEvent = SceneFlowType.GetEvent("QuitRequested", BindingFlags.Public | BindingFlags.Static);
            Assert.That(quitEvent, Is.Not.Null);

            quitEvent.AddEventHandler(null, onQuit);
            try
            {
                Assert.That((bool)Invoke(menu, "TryRequestQuit"), Is.True);
                Assert.That(quitRequested, Is.True);

                SetField(menu, "isLoading", true);
                Assert.That((bool)Invoke(menu, "TryStartMatch"), Is.False);
                Assert.That((bool)Invoke(menu, "TryRequestQuit"), Is.False);
            }
            finally
            {
                quitEvent.RemoveEventHandler(null, onQuit);
                Object.Destroy(menuObject);
            }

            yield return null;
        }

        private static Type GetGameplayType(string fullName)
        {
            var type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Gameplay type '{fullName}' was not found.");
            return type;
        }

        private static object GetProperty(Component component, string propertyName)
        {
            return component.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(component);
        }

        private static object Invoke(Component component, string methodName, params object[] arguments)
        {
            return component.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(component, arguments);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            return type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public)
                ?.Invoke(null, arguments);
        }

        private static void SetField(Component component, string fieldName, object value)
        {
            component.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(component, value);
        }
    }
}
