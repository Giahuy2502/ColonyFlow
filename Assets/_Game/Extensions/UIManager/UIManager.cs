using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class UIManager : Singleton<UIManager>
    {
        [SerializeField] private Transform canvasParent;
        [SerializeField] private List<UICanvas> canvasPrefabs = new List<UICanvas>();

        private readonly Dictionary<Type, UICanvas> prefabsByType =
            new Dictionary<Type, UICanvas>();
        private readonly Dictionary<Type, UICanvas> activeByType =
            new Dictionary<Type, UICanvas>();

        protected override void Awake()
        {
            base.Awake();
            if (!enabled)
                return;
            if (canvasParent == null)
                canvasParent = transform;

            UICanvas[] resourceCanvases = Resources.LoadAll<UICanvas>("UI");
            for (int i = 0; i < resourceCanvases.Length; i++)
            {
                UICanvas prefab = resourceCanvases[i];
                if (prefab != null)
                    prefabsByType[prefab.GetType()] = prefab;
            }

            for (int i = 0; i < canvasPrefabs.Count; i++)
            {
                UICanvas prefab = canvasPrefabs[i];
                if (prefab != null)
                    prefabsByType[prefab.GetType()] = prefab;
            }
        }

        public T Open<T>() where T : UICanvas
        {
            T canvas = GetUI<T>();
            if (canvas == null)
                return null;
            canvas.Setup();
            canvas.Open();
            return canvas;
        }

        public void Close<T>(float delay = 0f) where T : UICanvas
        {
            if (TryGetLoaded(out T canvas))
                canvas.Close(delay);
        }

        public bool IsOpened<T>() where T : UICanvas
        {
            return TryGetLoaded(out T canvas) && canvas.gameObject.activeSelf;
        }

        public T GetUI<T>() where T : UICanvas
        {
            if (TryGetLoaded(out T loaded))
                return loaded;
            if (!prefabsByType.TryGetValue(typeof(T), out UICanvas prefab) || prefab == null)
            {
                Debug.LogError($"UI prefab {typeof(T).Name} is not assigned to UIManager.", this);
                return null;
            }

            T canvas = Instantiate(prefab, canvasParent) as T;
            activeByType[typeof(T)] = canvas;
            return canvas;
        }

        public void CloseAll()
        {
            foreach (UICanvas canvas in activeByType.Values)
                if (canvas != null && canvas.gameObject.activeSelf)
                    canvas.CloseDirectly();
        }

        public bool TryGetLoaded<T>(out T canvas) where T : UICanvas
        {
            canvas = null;
            if (!activeByType.TryGetValue(typeof(T), out UICanvas loaded) || loaded == null)
                return false;
            canvas = loaded as T;
            return canvas != null;
        }
    }
}
