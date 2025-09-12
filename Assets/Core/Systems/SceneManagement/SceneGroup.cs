using System;
using System.Collections.Generic;
using System.Linq;
using Core.Systems.Navigation;
using Eflatun.SceneReference;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Systems.SceneManagement{
    [Serializable]
    public class SceneGroup{
        public string GroupName;
        public LoadSceneMode LoadMode = LoadSceneMode.Additive;
        public List<SceneData> Scenes;
        
        public SceneGroup(string groupName, SceneData scene){
            GroupName = groupName;
            
            Scenes = new List<SceneData> { scene };
        }
        
        public SceneGroup(string groupName, List<SceneData> scenes, LoadSceneMode mode = LoadSceneMode.Additive){
            GroupName = groupName;
            Scenes = scenes;
            LoadMode = mode;
        }

        public string FindSceneNameByType(SceneType sceneType){
            return Scenes.FirstOrDefault(scene => scene.SceneType == sceneType)?.Reference.Name;
        }
        
        public SceneData GetSceneDataByType(SceneType type)
        {
            return Scenes.FirstOrDefault(scene => scene.SceneType == type);
        }
        
        public bool IsPersistentScene(string sceneName){
            return Scenes.Any(scene => scene.Name == sceneName && scene.SceneType == SceneType.Persistent);
        }
    }

    [Serializable]
    public class SceneData{
        public SceneReference Reference;
        public string Name;
        public SceneType SceneType;

        public SceneData(SceneReference reference, string name){
            Reference = reference;
            Name = name;
            SceneType = SceneType.ActiveScene; // Default type, can be changed later
        }
        
        [Tooltip("Optional UI screen to open after this scene is loaded.")]
        public ScreenDefinition EntryScreen;
    }

    public enum SceneType{
        ActiveScene,
        MainMenu,
        UserInterface,
        HUD,
        Cinematic,
        Environment,
        Tooling,
        Persistent,
    }
}
