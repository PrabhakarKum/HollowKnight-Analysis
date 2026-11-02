using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowknight.Silksong
{
    // Where room scenes come from. AdditiveRoomLoader does the hide / activate / unload work; this only starts a
    // load. A seam so tests can build rooms in code and the editor can load scenes by path without Build Settings.
    public interface IRoomSceneSource
    {
        // False when the scene cannot be loaded by this name (checked first, so a typo is one clear error and not
        // an engine message).
        bool Exists(string sceneName);

        // Starts loading the scene additively. Returns the engine operation, or null when the scene was created
        // synchronously and is already live (code-built test rooms).
        AsyncOperation BeginLoad(string sceneName);
    }

    // Builds: scenes listed in Build Settings, loaded by name. (Adding rooms to Build Settings is the owner's job.)
    public sealed class BuildSettingsSceneSource : IRoomSceneSource
    {
        public bool Exists(string sceneName) => !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

        public AsyncOperation BeginLoad(string sceneName) => SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
    }

#if UNITY_EDITOR
    // Editor only: loads a scene file by asset path, so rooms work in play mode without being in Build Settings.
    public sealed class EditorPathSceneSource : IRoomSceneSource
    {
        readonly Func<string, string> resolver;

        // resolver maps a scene name ("Tut_01") to an asset path ("Assets/Scenes/Tut_01.unity"), or null.
        public EditorPathSceneSource(Func<string, string> sceneNameToPath)
        {
            resolver = sceneNameToPath;
        }

        string PathFor(string sceneName) => string.IsNullOrEmpty(sceneName) || resolver == null ? null : resolver(sceneName);

        public bool Exists(string sceneName)
        {
            string path = PathFor(sceneName);
            return !string.IsNullOrEmpty(path) && System.IO.File.Exists(path);
        }

        public AsyncOperation BeginLoad(string sceneName)
        {
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                PathFor(sceneName), new LoadSceneParameters(LoadSceneMode.Additive));
        }
    }
#endif
}
